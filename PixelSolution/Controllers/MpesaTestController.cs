using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PixelSolution.Services;
using System.Text.Json;

namespace PixelSolution.Controllers
{
    [Authorize(Roles = "Admin,Manager")]
    [Route("api/[controller]")]
    [ApiController]
    public class MpesaTestController : ControllerBase
    {
        private readonly IMpesaService _mpesaService;
        private readonly ILogger<MpesaTestController> _logger;

        public MpesaTestController(IMpesaService mpesaService, ILogger<MpesaTestController> logger)
        {
            _mpesaService = mpesaService;
            _logger = logger;
        }

        /// <summary>
        /// Test M-Pesa token generation and database caching
        /// </summary>
        [HttpGet("test-token")]
        public async Task<IActionResult> TestToken()
        {
            try
        {
                _logger.LogInformation("🧪 Testing M-Pesa token generation...");
                
                var token = await _mpesaService.GetAccessTokenAsync();
                
                if (!string.IsNullOrEmpty(token))
                {
                    return Ok(new
                    {
                        success = true,
                        message = "Token generated successfully",
                        tokenLength = token.Length,
                        tokenPreview = token.Substring(0, Math.Min(20, token.Length)) + "...",
                        timestamp = DateTime.UtcNow
                    });
                }
                
                return BadRequest(new
                {
                    success = false,
                    message = "Failed to generate token - empty response"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Token test failed");
                return BadRequest(new
                {
                    success = false,
                    message = ex.Message,
                    stackTrace = ex.StackTrace
                });
            }
        }

        /// <summary>
        /// Test Laravel-style STK Push using MpesaPaymentController
        /// </summary>
        [HttpPost("test-laravel-stk")]
        public async Task<IActionResult> TestLaravelStkPush([FromBody] StkTestRequest request)
        {
            try
            {
                _logger.LogInformation("🧪 Testing Laravel-style STK Push for {PhoneNumber}, Amount: {Amount}", 
                    request.PhoneNumber, request.Amount);

                // Validate phone number format
                if (!request.PhoneNumber.StartsWith("254") || request.PhoneNumber.Length != 12)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Phone number must be in format 254XXXXXXXXX (12 digits)"
                    });
                }

                // Validate amount
                if (request.Amount < 1 || request.Amount > 70000)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Amount must be between KSh 1 and KSh 70,000"
                    });
                }

                // Call the Laravel-style payment endpoint
                using var httpClient = new HttpClient();
                var paymentRequest = new
                {
                    phoneNumber = request.PhoneNumber,
                    amount = request.Amount,
                    accountReference = "TEST001",
                    transactionDesc = "Test Payment"
                };

                var json = System.Text.Json.JsonSerializer.Serialize(paymentRequest);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                // Add authorization header if needed
                var authHeader = HttpContext.Request.Headers["Authorization"].FirstOrDefault();
                if (!string.IsNullOrEmpty(authHeader))
                {
                    httpClient.DefaultRequestHeaders.Add("Authorization", authHeader);
                }

                var baseUrl = $"{HttpContext.Request.Scheme}://{HttpContext.Request.Host}";
                var response = await httpClient.PostAsync($"{baseUrl}/api/MpesaPayment/payments", content);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var result = System.Text.Json.JsonSerializer.Deserialize<object>(responseContent);
                    return Ok(new
                    {
                        success = true,
                        message = "Laravel-style STK Push completed successfully",
                        data = result,
                        timestamp = DateTime.UtcNow
                    });
                }
                else
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Laravel-style STK Push failed",
                        error = responseContent,
                        statusCode = response.StatusCode
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Laravel-style STK Push test failed");
                return BadRequest(new
                {
                    success = false,
                    message = ex.Message,
                    stackTrace = ex.StackTrace
                });
            }
        }

        /// <summary>
        /// Test STK Push (C2B) functionality - Original implementation
        /// </summary>
        [HttpPost("test-stk")]
        public async Task<IActionResult> TestStkPush([FromBody] StkTestRequest request)
        {
            try
            {
                _logger.LogInformation("🧪 Testing STK Push for {PhoneNumber}, Amount: {Amount}", 
                    request.PhoneNumber, request.Amount);

                // Validate phone number format
                if (!request.PhoneNumber.StartsWith("254") || request.PhoneNumber.Length != 12)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Phone number must be in format 254XXXXXXXXX (12 digits)"
                    });
                }

                // Validate amount
                if (request.Amount < 1 || request.Amount > 70000)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Amount must be between KSh 1 and KSh 70,000"
                    });
                }

                var response = await _mpesaService.InitiateStkPushAsync(
                    request.PhoneNumber,
                    request.Amount,
                    "TEST001",
                    "Test Payment"
                );

                return Ok(new
                {
                    success = true,
                    message = "STK Push initiated successfully",
                    responseCode = response.ResponseCode,
                    responseDescription = response.ResponseDescription,
                    checkoutRequestId = response.CheckoutRequestID,
                    merchantRequestId = response.MerchantRequestID,
                    customerMessage = response.CustomerMessage,
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ STK Push test failed");
                return BadRequest(new
                {
                    success = false,
                    message = ex.Message,
                    stackTrace = ex.StackTrace
                });
            }
        }

        /// <summary>
        /// Query STK Push status
        /// </summary>
        [HttpPost("query-stk")]
        public async Task<IActionResult> QueryStkStatus([FromBody] StkQueryRequest request)
        {
            try
            {
                _logger.LogInformation("🔍 Querying STK Push status for {CheckoutRequestId}", 
                    request.CheckoutRequestId);

                var response = await _mpesaService.QueryStkPushStatusAsync(request.CheckoutRequestId);

                return Ok(new
                {
                    success = true,
                    message = "STK Push status retrieved successfully",
                    data = response,
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ STK Push query failed");
                return BadRequest(new
                {
                    success = false,
                    message = ex.Message,
                    stackTrace = ex.StackTrace
                });
            }
        }

        /// <summary>
        /// Test multiple token requests to verify caching
        /// </summary>
        [HttpGet("test-token-caching")]
        public async Task<IActionResult> TestTokenCaching()
        {
            try
            {
                _logger.LogInformation("🧪 Testing token caching mechanism...");
                
                var results = new List<object>();
                
                // Make 3 consecutive token requests
                for (int i = 1; i <= 3; i++)
                {
                    var startTime = DateTime.UtcNow;
                    var token = await _mpesaService.GetAccessTokenAsync();
                    var endTime = DateTime.UtcNow;
                    var duration = (endTime - startTime).TotalMilliseconds;
                    
                    results.Add(new
                    {
                        requestNumber = i,
                        tokenLength = token?.Length ?? 0,
                        tokenPreview = token?.Substring(0, Math.Min(20, token?.Length ?? 0)) + "...",
                        durationMs = duration,
                        timestamp = startTime
                    });
                    
                    // Small delay between requests
                    await Task.Delay(100);
                }
                
                return Ok(new
                {
                    success = true,
                    message = "Token caching test completed",
                    results = results,
                    note = "First request should be slower (API call), subsequent requests should be faster (cached)"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Token caching test failed");
                return BadRequest(new
                {
                    success = false,
                    message = ex.Message,
                    stackTrace = ex.StackTrace
                });
            }
        }

        /// <summary>
        /// Simulate B2C transaction (for future implementation)
        /// </summary>
        [HttpPost("test-b2c")]
        public async Task<IActionResult> TestB2C([FromBody] B2CTestRequest request)
        {
            try
            {
                _logger.LogInformation("🧪 B2C Test - This feature is not yet implemented");
                
                // Placeholder for B2C implementation
                return Ok(new
                {
                    success = false,
                    message = "B2C functionality not yet implemented",
                    note = "This endpoint is prepared for future B2C implementation",
                    requestData = request
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ B2C test failed");
                return BadRequest(new
                {
                    success = false,
                    message = ex.Message
                });
            }
        }
    }

    // Request models for testing
    public class StkTestRequest
    {
        public string PhoneNumber { get; set; } = string.Empty;
        public decimal Amount { get; set; }
    }

    public class StkQueryRequest
    {
        public string CheckoutRequestId { get; set; } = string.Empty;
    }

    public class B2CTestRequest
    {
        public string PhoneNumber { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Remarks { get; set; } = string.Empty;
        public string Occasion { get; set; } = string.Empty;
    }
}
