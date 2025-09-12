using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using PixelSolution.Services;
using PixelSolution.Data;
using PixelSolution.Models;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.Json;

namespace PixelSolution.Controllers
{
    [Authorize(Roles = "Admin,Manager,Employee")]
    [Route("api/[controller]")]
    [ApiController]
    public class MpesaPaymentController : ControllerBase
    {
        private readonly MpesaSettings _settings;
        private readonly ILogger<MpesaPaymentController> _logger;
        private readonly ApplicationDbContext _context;

        public MpesaPaymentController(IOptions<MpesaSettings> settings, ILogger<MpesaPaymentController> logger, ApplicationDbContext context)
        {
            _settings = settings.Value;
            _logger = logger;
            _context = context;
        }

        /// <summary>
        /// Laravel-style payment route: Route::post("/payments", function(Request $request){...})->middleware(GetToken::class);
        /// </summary>
        [HttpPost("payments")]
        public async Task<IActionResult> ProcessPayment([FromBody] PaymentRequest request)
        {
            try
            {
                _logger.LogInformation("💳 Processing M-Pesa payment for {PhoneNumber}, Amount: KSh {Amount}", 
                    request.PhoneNumber, request.Amount);

                // Laravel equivalent: $token = $request->request->get('token');
                var token = HttpContext.Items["mpesa_token"]?.ToString();
                
                if (string.IsNullOrEmpty(token))
                {
                    return BadRequest(new { success = false, message = "M-Pesa token not found in request context" });
                }

                // Laravel equivalent: $passkey = "";
                var passkey = _settings.Passkey;
                
                // Laravel equivalent: $short_code = "My till as I had given you";
                var short_code = _settings.Shortcode;
                
                // Laravel equivalent: $timestamp = time now in year months, days, hours...("YmdHis");
                var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
                
                // Laravel equivalent: $password = base64_encode($short_code . $passkey . $timestamp);
                var password = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{short_code}{passkey}{timestamp}"));

                _logger.LogInformation("STK Push details - Shortcode: {Shortcode}, Timestamp: {Timestamp}", 
                    short_code, timestamp);

                var stkPushData = new
                {
                    BusinessShortCode = short_code,
                    Password = password,
                    Timestamp = timestamp,
                    TransactionType = "CustomerPayBillOnline", // Laravel: "write the reason"
                    Amount = request.Amount.ToString("F0"), // Laravel: "get the amount the user need to pay"
                    PartyA = request.PhoneNumber, // Laravel: "the cashier entered phone number"
                    PartyB = short_code, // Laravel: "the receiving till"
                    PhoneNumber = request.PhoneNumber,
                    CallBackURL = _settings.CallbackUrl, // Laravel: "get the callback url for the success or failed"
                    AccountReference = request.AccountReference ?? "Test",
                    TransactionDesc = request.TransactionDesc ?? "Test"
                };

                var json = JsonSerializer.Serialize(stkPushData);
                _logger.LogInformation("STK Push payload: {Payload}", json);

                using var httpClient = new HttpClient();
                var httpRequest = new HttpRequestMessage(HttpMethod.Post, 
                    "https://sandbox.safaricom.co.ke/mpesa/stkpush/v1/processrequest");
                
                // Laravel equivalent: 'Authorization' => 'Bearer ' . $token
                httpRequest.Headers.Add("Authorization", $"Bearer {token}");
                httpRequest.Content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await httpClient.SendAsync(httpRequest);
                var content = await response.Content.ReadAsStringAsync();

                _logger.LogInformation("STK Push response: {Response}", content);

                if (response.IsSuccessStatusCode)
                {
                    var stkResponse = JsonSerializer.Deserialize<StkPushResponse>(content);
                    
                    return Ok(new
                    {
                        success = true,
                        message = "STK Push initiated successfully",
                        data = new
                        {
                            responseCode = stkResponse?.ResponseCode,
                            responseDescription = stkResponse?.ResponseDescription,
                            checkoutRequestId = stkResponse?.CheckoutRequestID,
                            merchantRequestId = stkResponse?.MerchantRequestID,
                            customerMessage = stkResponse?.CustomerMessage
                        },
                        timestamp = DateTime.UtcNow
                    });
                }
                else
                {
                    _logger.LogError("❌ STK Push failed: {StatusCode} - {Content}", response.StatusCode, content);
                    return BadRequest(new
                    {
                        success = false,
                        message = $"STK Push failed: {response.StatusCode}",
                        error = content
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 Payment processing error");
                return StatusCode(500, new
                {
                    success = false,
                    message = "Payment processing failed",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Test endpoint to verify middleware token injection
        /// </summary>
        [HttpGet("test-token")]
        public IActionResult TestToken()
        {
            var token = HttpContext.Items["mpesa_token"]?.ToString();
            
            return Ok(new
            {
                success = !string.IsNullOrEmpty(token),
                message = string.IsNullOrEmpty(token) ? "No token found" : "Token found in context",
                tokenPreview = string.IsNullOrEmpty(token) ? null : token.Substring(0, Math.Min(20, token.Length)) + "...",
                timestamp = DateTime.UtcNow
            });
        }
    }

    // Request models
    public class PaymentRequest
    {
        public string PhoneNumber { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string? AccountReference { get; set; }
        public string? TransactionDesc { get; set; }
    }
}
