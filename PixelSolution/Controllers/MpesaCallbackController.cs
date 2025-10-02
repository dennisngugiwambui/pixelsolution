using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PixelSolution.Data;
using PixelSolution.Models;
using PixelSolution.Services.Interfaces;
using System.Text.Json;

namespace PixelSolution.Controllers
{
    [ApiController]
    [Route("api/mpesa")]
    public class MpesaCallbackController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<MpesaCallbackController> _logger;
        private readonly IReceiptPrintingService _receiptPrintingService;

        public MpesaCallbackController(ApplicationDbContext context, ILogger<MpesaCallbackController> logger, IReceiptPrintingService receiptPrintingService)
        {
            _context = context;
            _logger = logger;
            _receiptPrintingService = receiptPrintingService;
        }

        [HttpPost("callback")]
        public async Task<IActionResult> MpesaCallback()
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Read raw request body to handle potential JSON issues
                using var reader = new StreamReader(Request.Body);
                var rawBody = await reader.ReadToEndAsync();
                
                _logger.LogInformation("🔔 MPESA Callback received - Raw Body: {RawBody}", rawBody);

                if (string.IsNullOrEmpty(rawBody))
                {
                    _logger.LogWarning("⚠️ Empty callback body received");
                    return Ok(new { ResultCode = 1, ResultDesc = "Empty callback body" });
                }

                JsonElement callbackData;
                try
                {
                    callbackData = JsonSerializer.Deserialize<JsonElement>(rawBody);
                }
                catch (JsonException jsonEx)
                {
                    _logger.LogError(jsonEx, "❌ JSON parsing error in callback: {RawBody}", rawBody);
                    return Ok(new { ResultCode = 1, ResultDesc = "Invalid JSON format" });
                }

                // Parse the callback data with error handling
                if (!callbackData.TryGetProperty("Body", out var body))
                {
                    _logger.LogError("❌ Missing 'Body' property in callback");
                    return Ok(new { ResultCode = 1, ResultDesc = "Invalid callback structure - missing Body" });
                }

                if (!body.TryGetProperty("stkCallback", out var stkCallback))
                {
                    _logger.LogError("❌ Missing 'stkCallback' property in Body");
                    return Ok(new { ResultCode = 1, ResultDesc = "Invalid callback structure - missing stkCallback" });
                }
                
                // Log the entire callback for debugging
                _logger.LogInformation("📥 Full callback data: {CallbackData}", callbackData.ToString());
                
                var merchantRequestId = stkCallback.TryGetProperty("MerchantRequestID", out var merchantProp) ? merchantProp.GetString() : null;
                var checkoutRequestId = stkCallback.TryGetProperty("CheckoutRequestID", out var checkoutProp) ? checkoutProp.GetString() : null;
                var resultCode = stkCallback.TryGetProperty("ResultCode", out var resultProp) ? resultProp.GetInt32() : -1;
                var resultDesc = stkCallback.TryGetProperty("ResultDesc", out var descProp) ? descProp.GetString() : "Unknown error";

                if (string.IsNullOrEmpty(checkoutRequestId))
                {
                    _logger.LogError("❌ Missing CheckoutRequestID in callback");
                    return Ok(new { ResultCode = 1, ResultDesc = "Missing CheckoutRequestID" });
                }

                _logger.LogInformation("📋 Callback Details - MerchantRequestID: {MerchantRequestID}, CheckoutRequestID: {CheckoutRequestID}, ResultCode: {ResultCode}, ResultDesc: {ResultDesc}",
                    merchantRequestId, checkoutRequestId, resultCode, resultDesc);

                // CRITICAL: Find the M-Pesa transaction record first
                var mpesaTransaction = await _context.MpesaTransactions
                    .Include(mt => mt.Sale)
                    .FirstOrDefaultAsync(mt => mt.CheckoutRequestId == checkoutRequestId);

                if (mpesaTransaction == null)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError("❌ M-Pesa transaction not found for CheckoutRequestID: {CheckoutRequestID}", checkoutRequestId);
                    return Ok(new { ResultCode = 1, ResultDesc = "Transaction record not found" });
                }

                var sale = mpesaTransaction.Sale;
                if (sale == null)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError("❌ Sale record not found for M-Pesa transaction: {CheckoutRequestID}", checkoutRequestId);
                    return Ok(new { ResultCode = 1, ResultDesc = "Sale record not found" });
                }

                if (resultCode == 0) // Success
                {
                    string? mpesaReceiptNumber = null;
                    decimal? amountReceived = null;
                    string? transactionDate = null;
                    string? phoneNumber = null;

                    // Extract payment details if available
                    if (stkCallback.TryGetProperty("CallbackMetadata", out var metadata))
                    {
                        if (metadata.TryGetProperty("Item", out var itemsProperty))
                        {
                            foreach (var item in itemsProperty.EnumerateArray())
                            {
                                if (!item.TryGetProperty("Name", out var nameProperty) || 
                                    !item.TryGetProperty("Value", out var value))
                                {
                                    continue; // Skip items without Name or Value
                                }
                                
                                var name = nameProperty.GetString();

                            switch (name)
                            {
                                case "MpesaReceiptNumber":
                                    mpesaReceiptNumber = value.GetString();
                                    _logger.LogInformation("💳 MPESA Receipt: {Receipt}", mpesaReceiptNumber);
                                    break;
                                case "Amount":
                                    // Amount can be sent as number or string
                                    if (value.ValueKind == JsonValueKind.Number)
                                    {
                                        amountReceived = value.GetDecimal();
                                    }
                                    else if (value.ValueKind == JsonValueKind.String && decimal.TryParse(value.GetString(), out var amount))
                                    {
                                        amountReceived = amount;
                                    }
                                    _logger.LogInformation("💰 Amount received: {Amount}", amountReceived);
                                    break;
                                case "TransactionDate":
                                    // TransactionDate can be sent as number or string
                                    transactionDate = value.ValueKind == JsonValueKind.Number ? value.GetInt64().ToString() : value.GetString();
                                    break;
                                case "PhoneNumber":
                                    // PhoneNumber can be sent as number or string
                                    phoneNumber = value.ValueKind == JsonValueKind.Number ? value.GetInt64().ToString() : value.GetString();
                                    break;
                            }
                        }
                        }
                    }

                    // Update M-Pesa transaction record with completion details
                    mpesaTransaction.Status = "Completed";
                    mpesaTransaction.MpesaReceiptNumber = mpesaReceiptNumber;
                    mpesaTransaction.CompletedAt = DateTime.UtcNow;
                    if (amountReceived.HasValue)
                    {
                        mpesaTransaction.Amount = amountReceived.Value;
                    }

                    // Update Sale record - CRITICAL: Mark as completed and set amount paid
                    sale.Status = "Completed";
                    sale.AmountPaid = amountReceived ?? sale.TotalAmount;
                    sale.ChangeGiven = Math.Max(0, sale.AmountPaid - sale.TotalAmount);
                    sale.MpesaReceiptNumber = mpesaReceiptNumber; // Save M-Pesa receipt number

                    // Update product inventory - CRITICAL: Reduce stock for purchased items
                    var saleItems = await _context.SaleItems
                        .Include(si => si.Product)
                        .Where(si => si.SaleId == sale.SaleId)
                        .ToListAsync();

                    foreach (var saleItem in saleItems)
                    {
                        if (saleItem.Product != null)
                        {
                            // Reduce product stock
                            saleItem.Product.StockQuantity -= saleItem.Quantity;
                            
                            _logger.LogInformation("📦 Updated stock for Product {ProductId}: {ProductName} - Reduced by {Quantity}, New Stock: {NewStock}",
                                saleItem.ProductId, saleItem.Product.Name, saleItem.Quantity, saleItem.Product.StockQuantity);

                            // Prevent negative stock
                            if (saleItem.Product.StockQuantity < 0)
                            {
                                _logger.LogWarning("⚠️ Negative stock detected for Product {ProductId}: {ProductName} - Stock: {Stock}",
                                    saleItem.ProductId, saleItem.Product.Name, saleItem.Product.StockQuantity);
                            }
                        }
                    }

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    _logger.LogInformation("✅ Payment successful for sale: {SaleNumber} - Receipt: {Receipt} - Amount: KSh {Amount}",
                        sale.SaleNumber, mpesaReceiptNumber, amountReceived);

                    // Generate and print receipt for successful payment
                    try
                    {
                        var receiptPrinted = await _receiptPrintingService.PrintSalesReceiptAsync(sale.SaleId);
                        _logger.LogInformation("📄 Receipt generation result for M-Pesa payment: {ReceiptPrinted}", receiptPrinted);
                    }
                    catch (Exception receiptEx)
                    {
                        _logger.LogWarning(receiptEx, "⚠️ Receipt printing failed for M-Pesa payment but sale was successful: {ErrorMessage}", receiptEx.Message);
                        // Don't fail the callback if receipt printing fails
                    }
                }
                else
                {
                    // Payment failed - Update records but don't mark products as bought
                    mpesaTransaction.Status = "Failed";
                    mpesaTransaction.ErrorMessage = resultDesc;
                    
                    sale.Status = "Failed";
                    sale.AmountPaid = 0;

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    _logger.LogWarning("❌ Payment failed for sale: {SaleNumber}, Reason: {Reason} - Products NOT marked as bought",
                        sale.SaleNumber, resultDesc);
                }

                return Ok(new { ResultCode = 0, ResultDesc = "Success" });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "💥 Error processing MPESA callback - transaction rolled back");
                return Ok(new { ResultCode = 1, ResultDesc = "Error processing callback" });
            }
        }

        [HttpGet("test")]
        public IActionResult TestCallback()
        {
            _logger.LogInformation("🧪 MPESA Callback endpoint test successful");
            return Ok(new { message = "MPESA Callback endpoint is working", timestamp = DateTime.UtcNow });
        }

        [HttpPost("test-callback")]
        public async Task<IActionResult> TestMpesaCallback([FromBody] TestCallbackRequest request)
        {
            try
            {
                var testCallback = new
                {
                    Body = new
                    {
                        stkCallback = new
                        {
                            MerchantRequestID = request.MerchantRequestId ?? "test-merchant-123",
                            CheckoutRequestID = request.CheckoutRequestId ?? "ws_CO_test123456789",
                            ResultCode = request.ResultCode,
                            ResultDesc = request.ResultCode == 0 ? "The service request is processed successfully." : "Request cancelled by user",
                            CallbackMetadata = request.ResultCode == 0 ? new
                            {
                                Item = new[]
                                {
                                    new { Name = "Amount", Value = request.Amount?.ToString() ?? "100" },
                                    new { Name = "MpesaReceiptNumber", Value = request.MpesaReceiptNumber ?? "TEST123456789" },
                                    new { Name = "TransactionDate", Value = DateTime.Now.ToString("yyyyMMddHHmmss") },
                                    new { Name = "PhoneNumber", Value = request.PhoneNumber ?? "254758024400" }
                                }
                            } : null
                        }
                    }
                };

                var json = JsonSerializer.Serialize(testCallback);
                _logger.LogInformation("🧪 Simulating M-Pesa callback: {Json}", json);

                // Create a new request with the test data
                var testRequest = new DefaultHttpContext().Request;
                testRequest.Body = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
                testRequest.ContentType = "application/json";

                // Call the actual callback method
                var originalRequest = Request;
                try
                {
                    // Temporarily replace the request
                    var context = HttpContext;
                    var newContext = new DefaultHttpContext();
                    newContext.Request.Body = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
                    newContext.Request.ContentType = "application/json";
                    
                    // Reset the body position
                    newContext.Request.Body.Position = 0;
                    
                    // Update the controller context
                    ControllerContext.HttpContext = newContext;
                    
                    var result = await MpesaCallback();
                    
                    // Restore original context
                    ControllerContext.HttpContext = context;
                    
                    return Ok(new { 
                        message = "Test callback processed", 
                        result = result,
                        testData = testCallback 
                    });
                }
                finally
                {
                    // Ensure we restore the original request
                    ControllerContext.HttpContext.Request.Body = originalRequest.Body;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error in test callback");
                return BadRequest(new { error = ex.Message });
            }
        }

        public class TestCallbackRequest
        {
            public string? MerchantRequestId { get; set; }
            public string? CheckoutRequestId { get; set; }
            public int ResultCode { get; set; } = 0;
            public decimal? Amount { get; set; }
            public string? MpesaReceiptNumber { get; set; }
            public string? PhoneNumber { get; set; }
        }

        [HttpGet("c2b/test")]
        public IActionResult TestC2BEndpoint()
        {
            _logger.LogInformation("🧪 C2B Test endpoint accessed");
            return Ok(new { 
                message = "C2B endpoint is accessible", 
                timestamp = DateTime.Now,
                ngrokWorking = true 
            });
        }

        [HttpPost("c2b/validation")]
        public IActionResult C2BValidation()
        {
            try
            {
                using var reader = new StreamReader(Request.Body);
                var rawBody = reader.ReadToEndAsync().Result;
                
                _logger.LogInformation("📥 C2B Validation request received: {RawBody}", rawBody);
                _logger.LogInformation("🔍 C2B Validation Headers: {Headers}", string.Join(", ", Request.Headers.Select(h => $"{h.Key}: {h.Value}")));

                // For now, accept all transactions
                // You can add custom validation logic here
                return Ok(new
                {
                    ResultCode = 0,
                    ResultDesc = "Accepted"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error in C2B validation");
                return Ok(new
                {
                    ResultCode = 1,
                    ResultDesc = "Rejected"
                });
            }
        }

        [HttpPost("c2b/confirmation")]
        public async Task<IActionResult> C2BConfirmation()
        {
            try
            {
                using var reader = new StreamReader(Request.Body);
                var rawBody = await reader.ReadToEndAsync();
                
                _logger.LogInformation("📥 C2B Confirmation received: {RawBody}", rawBody);
                _logger.LogInformation("🔍 C2B Headers: {Headers}", string.Join(", ", Request.Headers.Select(h => $"{h.Key}: {h.Value}")));

                // Parse C2B payment data
                var c2bData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(rawBody);
                
                if (c2bData != null)
                {
                    // Extract transaction details
                    var transactionType = c2bData.ContainsKey("TransactionType") ? c2bData["TransactionType"].GetString() : "";
                    var transId = c2bData.ContainsKey("TransID") ? c2bData["TransID"].GetString() : "";
                    var transAmount = c2bData.ContainsKey("TransAmount") ? c2bData["TransAmount"].GetDecimal() : 0;
                    var businessShortCode = c2bData.ContainsKey("BusinessShortCode") ? c2bData["BusinessShortCode"].GetString() : "";
                    var billRefNumber = c2bData.ContainsKey("BillRefNumber") ? c2bData["BillRefNumber"].GetString() : "";
                    var msisdn = c2bData.ContainsKey("MSISDN") ? c2bData["MSISDN"].GetString() : "";
                    var firstName = c2bData.ContainsKey("FirstName") ? c2bData["FirstName"].GetString() : "";
                    var middleName = c2bData.ContainsKey("MiddleName") ? c2bData["MiddleName"].GetString() : "";
                    var lastName = c2bData.ContainsKey("LastName") ? c2bData["LastName"].GetString() : "";
                    
                    // Build customer full name
                    var customerName = $"{firstName} {middleName} {lastName}".Trim();
                    
                    _logger.LogInformation("💰 C2B Payment: TransID={TransID}, Amount={Amount}, Phone={Phone}, Customer={Customer}, Till={Till}", 
                        transId, transAmount, msisdn, customerName, businessShortCode);

                    // VALIDATE: Only accept payments to our till 6509715
                    if (businessShortCode != "6509715")
                    {
                        _logger.LogWarning("⚠️ Payment rejected - Wrong till number: {Till}. Expected: 6509715", businessShortCode);
                        return Ok(new
                        {
                            ResultCode = 0,
                            ResultDesc = "Payment received but for different till"
                        });
                    }

                    // Try to find pending sale by amount and phone number
                    var pendingSale = await _context.Sales
                        .Where(s => s.Status == "Pending" && 
                                   s.PaymentMethod == "M-Pesa" &&
                                   s.TotalAmount == transAmount &&
                                   (string.IsNullOrEmpty(s.CustomerPhone) || s.CustomerPhone.Contains(msisdn.Substring(msisdn.Length - 9))))
                        .OrderByDescending(s => s.SaleDate)
                        .FirstOrDefaultAsync();

                    // Record ALL transactions to till 6509715
                    _logger.LogInformation("💾 Recording transaction: Code={Code}, Amount={Amount}, Customer={Customer}", 
                        transId, transAmount, customerName);
                    
                    var unusedTransaction = new UnusedMpesaTransaction
                    {
                        TransactionCode = transId ?? "",
                        TillNumber = businessShortCode ?? "6509715",
                        Amount = transAmount,
                        PhoneNumber = msisdn ?? "",
                        CustomerName = customerName,
                        ReceivedAt = DateTime.UtcNow,
                        IsUsed = pendingSale != null,
                        SaleId = pendingSale?.SaleId
                    };

                    _context.UnusedMpesaTransactions.Add(unusedTransaction);

                    if (pendingSale != null)
                    {
                        // Link M-Pesa transaction to sale
                        pendingSale.MpesaReceiptNumber = transId;
                        pendingSale.Status = "Completed";
                        pendingSale.AmountPaid = transAmount;
                        
                        _logger.LogInformation("✅ C2B Payment linked to Sale #{SaleNumber}, TransID: {TransID}", 
                            pendingSale.SaleNumber, transId);
                    }
                    else
                    {
                        _logger.LogWarning("⚠️ No matching pending sale found for C2B payment. TransID: {TransID}, Amount: {Amount} - Saved for manual verification", 
                            transId, transAmount);
                    }

                    await _context.SaveChangesAsync();
                    _logger.LogInformation("✅ Transaction saved to database: {Code}", transId);
                }

                return Ok(new
                {
                    ResultCode = 0,
                    ResultDesc = "Success"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error in C2B confirmation");
                return Ok(new
                {
                    ResultCode = 1,
                    ResultDesc = "Failed"
                });
            }
        }
    }
}
