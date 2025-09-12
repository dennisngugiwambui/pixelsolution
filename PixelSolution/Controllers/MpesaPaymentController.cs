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
        /// Laravel-style payment route with database transaction handling
        /// Route::post("/payments", function(Request $request){...})->middleware(GetToken::class);
        /// </summary>
        [HttpPost("payments")]
        public async Task<IActionResult> ProcessPayment([FromBody] PaymentRequest request)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
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
                    
                    if (stkResponse != null && !string.IsNullOrEmpty(stkResponse.CheckoutRequestID))
                    {
                        // CRITICAL: Create Sale record first (required for transaction tracking)
                        var sale = new Sale
                        {
                            SaleNumber = $"SALE-{DateTime.Now:yyyyMMddHHmmss}",
                            UserId = GetCurrentUserId(),
                            CashierName = GetCurrentUserName(),
                            CustomerName = request.CustomerName ?? "M-Pesa Customer",
                            CustomerPhone = request.PhoneNumber,
                            CustomerEmail = request.CustomerEmail ?? "",
                            PaymentMethod = "MPesa",
                            TotalAmount = request.Amount,
                            AmountPaid = 0, // Will be updated on callback success
                            ChangeGiven = 0,
                            Status = "Pending", // Will be updated via callback
                            SaleDate = DateTime.UtcNow
                        };

                        _context.Sales.Add(sale);
                        await _context.SaveChangesAsync(); // Save to get SaleId

                        _logger.LogInformation("✅ Sale record created: {SaleNumber} (ID: {SaleId})", sale.SaleNumber, sale.SaleId);

                        // CRITICAL: Save M-Pesa transaction record immediately after STK push success
                        var mpesaTransaction = new MpesaTransaction
                        {
                            SaleId = sale.SaleId,
                            CheckoutRequestId = stkResponse.CheckoutRequestID,
                            MerchantRequestId = stkResponse.MerchantRequestID,
                            PhoneNumber = request.PhoneNumber,
                            Amount = request.Amount,
                            Status = "Pending", // Will be updated via callback
                            CreatedAt = DateTime.UtcNow
                        };

                        _context.MpesaTransactions.Add(mpesaTransaction);
                        await _context.SaveChangesAsync();

                        _logger.LogInformation("💾 M-Pesa transaction saved to database: CheckoutRequestID: {CheckoutRequestID}", 
                            stkResponse.CheckoutRequestID);

                        // Add sale items if provided
                        if (request.SaleItems != null && request.SaleItems.Any())
                        {
                            foreach (var item in request.SaleItems)
                            {
                                var saleItem = new SaleItem
                                {
                                    SaleId = sale.SaleId,
                                    ProductId = item.ProductId,
                                    Quantity = item.Quantity,
                                    UnitPrice = item.UnitPrice,
                                    TotalPrice = item.TotalPrice
                                };
                                _context.SaleItems.Add(saleItem);
                            }
                            await _context.SaveChangesAsync();
                            _logger.LogInformation("📦 Added {ItemCount} sale items to transaction", request.SaleItems.Count);
                        }

                        // Commit transaction - all database operations successful
                        await transaction.CommitAsync();

                        return Ok(new
                        {
                            success = true,
                            message = "STK Push initiated and transaction saved successfully",
                            data = new
                            {
                                saleId = sale.SaleId,
                                saleNumber = sale.SaleNumber,
                                responseCode = stkResponse.ResponseCode,
                                responseDescription = stkResponse.ResponseDescription,
                                checkoutRequestId = stkResponse.CheckoutRequestID,
                                merchantRequestId = stkResponse.MerchantRequestID,
                                customerMessage = stkResponse.CustomerMessage,
                                transactionStatus = "Pending - Awaiting customer payment"
                            },
                            timestamp = DateTime.UtcNow
                        });
                    }
                    else
                    {
                        await transaction.RollbackAsync();
                        _logger.LogError("❌ Invalid STK Push response - missing CheckoutRequestID");
                        return BadRequest(new
                        {
                            success = false,
                            message = "Invalid STK Push response from M-Pesa API",
                            error = "Missing CheckoutRequestID in response"
                        });
                    }
                }
                else
                {
                    await transaction.RollbackAsync();
                    _logger.LogError("❌ STK Push failed: {StatusCode} - {Content}", response.StatusCode, content);
                    return BadRequest(new
                    {
                        success = false,
                        message = $"STK Push failed: {response.StatusCode}",
                        error = content,
                        note = "No database records created due to STK Push failure"
                    });
                }
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "💥 Payment processing error - transaction rolled back");
                return StatusCode(500, new
                {
                    success = false,
                    message = "Payment processing failed",
                    error = ex.Message,
                    note = "All database operations have been rolled back"
                });
            }
        }

        private int GetCurrentUserId()
        {
            // Get current user ID from claims or context
            var userIdClaim = User.FindFirst("UserId")?.Value;
            if (int.TryParse(userIdClaim, out int userId))
            {
                return userId;
            }
            
            // Fallback: try to get from email
            var email = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;
            if (!string.IsNullOrEmpty(email))
            {
                var user = _context.Users.FirstOrDefault(u => u.Email == email);
                return user?.UserId ?? 1; // Default to admin if not found
            }
            
            return 1; // Default fallback
        }

        private string GetCurrentUserName()
        {
            var name = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value;
            if (!string.IsNullOrEmpty(name))
            {
                return name;
            }
            
            var email = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;
            return email ?? "System User";
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
