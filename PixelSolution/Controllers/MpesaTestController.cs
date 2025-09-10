using Microsoft.AspNetCore.Mvc;
using PixelSolution.Services;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace PixelSolution.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MpesaTestController : ControllerBase
    {
        private readonly IMpesaService _mpesaService;
        private readonly ILogger<MpesaTestController> _logger;

        public MpesaTestController(IMpesaService mpesaService, ILogger<MpesaTestController> logger)
        {
            _mpesaService = mpesaService;
            _logger = logger;
        }

        [HttpPost("test-stk")]
        public async Task<IActionResult> TestStkPush([FromBody] TestStkRequest request)
        {
            try
            {
                _logger.LogInformation("🧪 Testing STK Push for phone: {Phone}, amount: {Amount}", request.PhoneNumber, request.Amount);

                // Test token generation first
                var token = await _mpesaService.GetAccessTokenAsync();
                _logger.LogInformation("✅ Access token obtained successfully");

                // Test STK push
                var response = await _mpesaService.InitiateStkPushAsync(
                    request.PhoneNumber,
                    request.Amount,
                    "TEST001",
                    "Test Payment"
                );

                _logger.LogInformation("📱 STK Push Response: {Response}", JsonSerializer.Serialize(response));

                return Ok(new
                {
                    success = true,
                    message = "STK Push test completed",
                    response = response,
                    checkoutRequestId = response?.CheckoutRequestID,
                    merchantRequestId = response?.MerchantRequestID,
                    responseCode = response?.ResponseCode,
                    responseDescription = response?.ResponseDescription
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ STK Push test failed: {Message}", ex.Message);
                return BadRequest(new
                {
                    success = false,
                    message = ex.Message,
                    stackTrace = ex.StackTrace
                });
            }
        }

        [HttpGet("test-token")]
        public async Task<IActionResult> TestToken()
        {
            try
            {
                var token = await _mpesaService.GetAccessTokenAsync();
                return Ok(new
                {
                    success = true,
                    message = "Token generated successfully",
                    tokenLength = token?.Length ?? 0,
                    tokenPreview = token?.Substring(0, Math.Min(10, token?.Length ?? 0)) + "..."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Token test failed: {Message}", ex.Message);
                return BadRequest(new
                {
                    success = false,
                    message = ex.Message
                });
            }
        }
    }

    public class TestStkRequest
    {
        public string PhoneNumber { get; set; } = "";
        public decimal Amount { get; set; }
    }
}
