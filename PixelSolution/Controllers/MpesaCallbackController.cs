using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using PixelSolution.Data;
using Microsoft.EntityFrameworkCore;

namespace PixelSolution.Controllers
{
    [ApiController]
    [Route("api/mpesa")]
    public class MpesaCallbackController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<MpesaCallbackController> _logger;

        public MpesaCallbackController(ApplicationDbContext context, ILogger<MpesaCallbackController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpPost("callback")]
        public async Task<IActionResult> MpesaCallback([FromBody] JsonElement callbackData)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                _logger.LogInformation("🔔 MPESA Callback received: {CallbackData}", callbackData.ToString());

                // Parse the callback data
                var body = callbackData.GetProperty("Body");
                var stkCallback = body.GetProperty("stkCallback");
                
                var merchantRequestId = stkCallback.GetProperty("MerchantRequestID").GetString();
                var checkoutRequestId = stkCallback.GetProperty("CheckoutRequestID").GetString();
                var resultCode = stkCallback.GetProperty("ResultCode").GetInt32();
                var resultDesc = stkCallback.GetProperty("ResultDesc").GetString();

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

                    // Update M-Pesa transaction record
                    mpesaTransaction.Status = "Completed";
                    mpesaTransaction.MpesaReceiptNumber = mpesaReceiptNumber;
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
                                saleItem.ProductId, saleItem.Product.ProductName, saleItem.Quantity, saleItem.Product.StockQuantity);

                            // Prevent negative stock
                            if (saleItem.Product.StockQuantity < 0)
                            {
                                _logger.LogWarning("⚠️ Negative stock detected for Product {ProductId}: {ProductName} - Stock: {Stock}",
                                    saleItem.ProductId, saleItem.Product.ProductName, saleItem.Product.StockQuantity);
                            }
                        }
                    }

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    _logger.LogInformation("✅ Payment successful for sale: {SaleNumber} - Receipt: {Receipt} - Amount: KSh {Amount}",
                        sale.SaleNumber, mpesaReceiptNumber, amountReceived);
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
    }
}
