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
                        var items = metadata.GetProperty("Item");
                        foreach (var item in items.EnumerateArray())
                        {
                            var name = item.GetProperty("Name").GetString();
                            var value = item.GetProperty("Value");

                            switch (name)
                            {
                                case "MpesaReceiptNumber":
                                    mpesaReceiptNumber = value.GetString();
                                    _logger.LogInformation("💳 MPESA Receipt: {Receipt}", mpesaReceiptNumber);
                                    break;
                                case "Amount":
                                    if (decimal.TryParse(value.GetString(), out var amount))
                                    {
                                        amountReceived = amount;
                                    }
                                    break;
                                case "TransactionDate":
                                    transactionDate = value.GetString();
                                    break;
                                case "PhoneNumber":
                                    phoneNumber = value.GetString();
                                    break;
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
    }
}
