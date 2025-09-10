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

                // Find the sale by checkout request ID using MpesaTransaction table
                var mpesaTransaction = await _context.MpesaTransactions
                    .Include(mt => mt.Sale)
                    .FirstOrDefaultAsync(mt => mt.CheckoutRequestId == checkoutRequestId);
                
                var sale = mpesaTransaction?.Sale;

                if (sale != null)
                {
                    if (resultCode == 0) // Success
                    {
                        // Payment successful
                        sale.Status = "Completed";
                        
                        // Extract payment details if available
                        if (stkCallback.TryGetProperty("CallbackMetadata", out var metadata))
                        {
                            var items = metadata.GetProperty("Item");
                            foreach (var item in items.EnumerateArray())
                            {
                                var name = item.GetProperty("Name").GetString();
                                if (name == "MpesaReceiptNumber")
                                {
                                    // You can store the MPESA receipt number if needed
                                    _logger.LogInformation("💳 MPESA Receipt: {Receipt}", item.GetProperty("Value").GetString());
                                }
                            }
                        }

                        _logger.LogInformation("✅ Payment successful for sale: {SaleNumber}", sale.SaleNumber);
                    }
                    else
                    {
                        // Payment failed
                        sale.Status = "Failed";
                        _logger.LogWarning("❌ Payment failed for sale: {SaleNumber}, Reason: {Reason}", sale.SaleNumber, resultDesc);
                    }

                    await _context.SaveChangesAsync();
                }
                else
                {
                    _logger.LogWarning("⚠️ Sale not found for CheckoutRequestID: {CheckoutRequestID}", checkoutRequestId);
                }

                return Ok(new { ResultCode = 0, ResultDesc = "Success" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 Error processing MPESA callback");
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
