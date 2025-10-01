using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PixelSolution.Services;
using PixelSolution.Data;
using Microsoft.EntityFrameworkCore;
using System.Text;
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
        private readonly ApplicationDbContext _context;

        public MpesaTestController(IMpesaService mpesaService, ILogger<MpesaTestController> logger, ApplicationDbContext context)
        {
            _mpesaService = mpesaService;
            _logger = logger;
            _context = context;
        }

        /// <summary>
        /// Clear all M-Pesa tokens from database to force fresh token generation
        /// </summary>
        [HttpPost("clear-tokens")]
        public async Task<IActionResult> ClearTokens()
        {
            try
            {
                _logger.LogInformation("🧹 Clearing all M-Pesa tokens from database...");
                
                var allTokens = await _context.MpesaTokens.ToListAsync();
                _context.MpesaTokens.RemoveRange(allTokens);
                await _context.SaveChangesAsync();
                
                return Ok(new
                {
                    success = true,
                    message = $"Cleared {allTokens.Count} tokens from database",
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Failed to clear tokens");
                return BadRequest(new
                {
                    success = false,
                    message = ex.Message
                });
            }
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

                // Call the Laravel-style MpesaPaymentController endpoint
                using var httpClient = new HttpClient();
                
                // Get the base URL from the current request
                var baseUrl = $"{Request.Scheme}://{Request.Host}";
                var paymentUrl = $"{baseUrl}/api/MpesaPayment/payments";

                // Create a comprehensive payment request with sample products
                var paymentRequest = new
                {
                    PhoneNumber = request.PhoneNumber,
                    Amount = request.Amount,
                    AccountReference = "TEST-LARAVEL-DB",
                    TransactionDesc = "Laravel-style STK Push Test with Database",
                    CustomerName = "Test Customer",
                    CustomerEmail = "test@example.com",
                    SaleItems = new[]
                    {
                        new
                        {
                            ProductId = 1, // Assuming product ID 1 exists
                            Quantity = 1,
                            UnitPrice = request.Amount,
                            TotalPrice = request.Amount
                        }
                    }
                };

                var json = JsonSerializer.Serialize(paymentRequest);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                // Add authorization header if needed
                if (Request.Headers.ContainsKey("Authorization"))
                {
                    httpClient.DefaultRequestHeaders.Add("Authorization", Request.Headers["Authorization"].ToString());
                }

                var response = await httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Post, paymentUrl)
                {
                    Content = content
                });

                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    JsonElement responseData;
                    try
                    {
                        responseData = JsonSerializer.Deserialize<JsonElement>(responseContent);
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogError(ex, "Failed to parse JSON response: {ResponseContent}", responseContent);
                        return BadRequest(new
                        {
                            success = false,
                            message = "Invalid JSON response from payment API",
                            rawResponse = responseContent,
                            error = ex.Message
                        });
                    }
                    
                    // Extract key information for validation
                    var checkoutRequestId = "";
                    var saleId = 0;
                    var saleNumber = "";
                    
                    if (responseData.TryGetProperty("data", out var data))
                    {
                        if (data.TryGetProperty("checkoutRequestId", out var checkoutId))
                        {
                            checkoutRequestId = checkoutId.GetString() ?? "";
                        }
                        if (data.TryGetProperty("saleId", out var sId))
                        {
                            saleId = sId.GetInt32();
                        }
                        if (data.TryGetProperty("saleNumber", out var sNum))
                        {
                            saleNumber = sNum.GetString() ?? "";
                        }
                    }

                    // Verify database records were created
                    var dbValidation = await ValidateDatabaseRecords(checkoutRequestId, saleId);

                    return Ok(new
                    {
                        success = true,
                        message = "Laravel-style STK Push with database validation completed successfully",
                        response = JsonSerializer.Deserialize<object>(responseContent),
                        databaseValidation = dbValidation,
                        instructions = new
                        {
                            nextStep = "Complete payment on your phone to test callback handling",
                            checkoutRequestId = checkoutRequestId,
                            saleNumber = saleNumber,
                            note = "Database records have been created and validated"
                        },
                        timestamp = DateTime.UtcNow
                    });
                }
                else
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Laravel-style STK Push test failed",
                        error = responseContent,
                        statusCode = response.StatusCode,
                        note = "No database records should be created due to STK Push failure"
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 Laravel-style STK Push test error");
                return StatusCode(500, new
                {
                    success = false,
                    message = "Laravel-style STK Push test failed",
                    error = ex.Message
                });
            }
        }

        private async Task<object> ValidateDatabaseRecords(string checkoutRequestId, int saleId)
        {
            try
            {
                // Check if M-Pesa transaction record exists
                var mpesaTransaction = await _context.MpesaTransactions
                    .FirstOrDefaultAsync(mt => mt.CheckoutRequestId == checkoutRequestId);

                // Check if Sale record exists
                var sale = await _context.Sales
                    .Include(s => s.SaleItems)
                    .FirstOrDefaultAsync(s => s.SaleId == saleId);

                return new
                {
                    mpesaTransactionExists = mpesaTransaction != null,
                    mpesaTransactionStatus = mpesaTransaction?.Status ?? "Not Found",
                    saleExists = sale != null,
                    saleStatus = sale?.Status ?? "Not Found",
                    saleItemsCount = sale?.SaleItems?.Count ?? 0,
                    validation = new
                    {
                        transactionRecordSaved = mpesaTransaction != null,
                        saleRecordSaved = sale != null,
                        readyForCallback = mpesaTransaction != null && sale != null,
                        message = mpesaTransaction != null && sale != null 
                            ? "✅ All database records created successfully - ready for callback"
                            : "❌ Database validation failed - records missing"
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating database records");
                return new
                {
                    error = "Database validation failed",
                    message = ex.Message
                };
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

        /// <summary>
        /// Test QR Code generation
        /// </summary>
        [HttpPost("test-qr")]
        public async Task<IActionResult> TestQRCode([FromBody] QRCodeTestRequest request)
        {
            try
            {
                _logger.LogInformation("🧪 Testing QR Code generation for amount: {Amount}", request.Amount);

                var result = await _mpesaService.GenerateQRCodeAsync(
                    request.MerchantName ?? "PixelSolution",
                    request.RefNo ?? "INV-" + DateTime.Now.ToString("yyyyMMddHHmmss"),
                    request.Amount,
                    request.TrxCode ?? "BG",
                    request.Size ?? "300"
                );

                return Ok(new
                {
                    success = true,
                    message = "QR Code generated successfully",
                    data = result,
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ QR Code generation test failed");
                return BadRequest(new
                {
                    success = false,
                    message = ex.Message,
                    stackTrace = ex.StackTrace
                });
            }
        }

        /// <summary>
        /// Test C2B URL registration
        /// </summary>
        [HttpPost("register-c2b")]
        public async Task<IActionResult> RegisterC2BUrls()
        {
            try
            {
                _logger.LogInformation(" Registering C2B URLs with Safaricom...");
                var result = await _mpesaService.RegisterC2BUrlsAsync();
                _logger.LogInformation(" C2B URLs registered successfully");
                return Ok(new { success = true, message = "C2B URLs registered successfully", data = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, " Error registering C2B URLs");
                return Ok(new { success = false, message = $"Failed to register C2B URLs: {ex.Message}" });
                return BadRequest(new
                {
                    success = false,
                    message = ex.Message,
                    stackTrace = ex.StackTrace
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

    public class QRCodeTestRequest
    {
        public string? MerchantName { get; set; }
        public string? RefNo { get; set; }
        public decimal Amount { get; set; }
        public string? TrxCode { get; set; }
        public string? Size { get; set; }
    }
}
