using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using PixelSolution.Data;
using PixelSolution.Models;

namespace PixelSolution.Services
{
    public class MpesaSettings
    {
        public string ConsumerKey { get; set; } = string.Empty;
        public string ConsumerSecret { get; set; } = string.Empty;
        public string Shortcode { get; set; } = string.Empty; // For STK Push (Paybill or Till)
        public string TillNumber { get; set; } = string.Empty; // For Buy Goods (Till) - QR codes
        public string Passkey { get; set; } = string.Empty;
        public string TransactionType { get; set; } = "CustomerPayBillOnline"; // CustomerPayBillOnline or CustomerBuyGoodsOnline
        public string CallbackUrl { get; set; } = string.Empty;
        public string ConfirmationUrl { get; set; } = string.Empty;
        public string ValidationUrl { get; set; } = string.Empty;
        public string BaseUrl { get; set; } = "https://api.safaricom.co.ke"; // Production URL
        public bool IsSandbox { get; set; } = false;
        public string PublicDomain { get; set; } = string.Empty; // Public domain for callbacks (ngrok or production)
    }

    public class MpesaTokenResponse
    {
        public string access_token { get; set; } = string.Empty;
        public string expires_in { get; set; } = string.Empty;
    }

    public class StkPushRequest
    {
        public string BusinessShortCode { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string Timestamp { get; set; } = string.Empty;
        public string TransactionType { get; set; } = "CustomerPayBillOnline";
        public string Amount { get; set; } = string.Empty;
        public string PartyA { get; set; } = string.Empty;
        public string PartyB { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string CallBackURL { get; set; } = string.Empty;
        public string AccountReference { get; set; } = string.Empty;
        public string TransactionDesc { get; set; } = string.Empty;
    }

    public class StkPushResponse
    {
        public string MerchantRequestID { get; set; } = string.Empty;
        public string CheckoutRequestID { get; set; } = string.Empty;
        public string ResponseCode { get; set; } = string.Empty;
        public string ResponseDescription { get; set; } = string.Empty;
        public string CustomerMessage { get; set; } = string.Empty;
    }

    public interface IMpesaService
    {
        Task<string> GetAccessTokenAsync();
        Task<StkPushResponse> InitiateStkPushAsync(string phoneNumber, decimal amount, string accountReference, string transactionDescription);
        Task<object> QueryStkPushStatusAsync(string checkoutRequestId);
        Task<object> GenerateQRCodeAsync(string merchantName, string refNo, decimal amount, string trxCode = "BG", string size = "300");
        Task<object> RegisterC2BUrlsAsync();
    }

    public class MpesaService : IMpesaService
    {
        private readonly HttpClient _httpClient;
        private readonly MpesaSettings _settings;
        private readonly ILogger<MpesaService> _logger;
        private readonly ApplicationDbContext _context;

        public MpesaService(HttpClient httpClient, IOptions<MpesaSettings> settings, ILogger<MpesaService> logger, ApplicationDbContext context)
        {
            _httpClient = httpClient;
            _settings = settings.Value;
            _logger = logger;
            _context = context;
        }

        public async Task<string> GetAccessTokenAsync()
        {
            try
            {
                _logger.LogInformation("🔍 Checking for valid MPESA access token in database...");
                _logger.LogInformation("Current UTC time: {UtcNow}, Checking for tokens expiring after: {CheckTime}", 
                    DateTime.UtcNow, DateTime.UtcNow.AddMinutes(5));

                // STEP 1: Check if we have a valid token in the database
                var existingToken = await _context.MpesaTokens
                    .Where(t => t.IsActive && t.ExpiresAt > DateTime.UtcNow.AddMinutes(5)) // 5 minute buffer
                    .OrderByDescending(t => t.CreatedAt)
                    .FirstOrDefaultAsync();

                if (existingToken != null)
                {
                    _logger.LogInformation("✅ Valid token found in database, created: {CreatedAt}, expires at: {ExpiresAt}", 
                        existingToken.CreatedAt, existingToken.ExpiresAt);
                    
                    // STEP 2: Validate the token is actually valid by checking its structure
                    if (!string.IsNullOrEmpty(existingToken.AccessToken) && existingToken.AccessToken.Length > 20)
                    {
                        _logger.LogInformation("✅ Token validated successfully, using cached token");
                        return existingToken.AccessToken;
                    }
                    else
                    {
                        _logger.LogWarning("⚠️ Cached token appears invalid, will generate new token");
                        // Deactivate the invalid token
                        existingToken.IsActive = false;
                        await _context.SaveChangesAsync();
                    }
                }

                // STEP 3: No valid token found or existing token is invalid, generate new one
                _logger.LogInformation("⚠️ No valid token found, generating new MPESA access token...");
                _logger.LogInformation("MPESA Config - BaseUrl: {BaseUrl}, Shortcode: {Shortcode}", 
                    _settings.BaseUrl, _settings.Shortcode);
                _logger.LogInformation("ConsumerKey (first 10 chars): {ConsumerKey}...", 
                    _settings.ConsumerKey?.Substring(0, Math.Min(10, _settings.ConsumerKey?.Length ?? 0)));
                _logger.LogInformation("ConsumerSecret (first 10 chars): {ConsumerSecret}...", 
                    _settings.ConsumerSecret?.Substring(0, Math.Min(10, _settings.ConsumerSecret?.Length ?? 0)));

                // Generate Base64 encoded credentials (consumer_key:consumer_secret)
                var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_settings.ConsumerKey}:{_settings.ConsumerSecret}"));
                var requestUrl = $"{_settings.BaseUrl}/oauth/v1/generate?grant_type=client_credentials";
                
                _logger.LogInformation("Token request URL: {Url}", requestUrl);
                _logger.LogInformation("Authorization header: Basic {Credentials}", credentials.Substring(0, Math.Min(20, credentials.Length)) + "...");
                
                var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
                request.Headers.Add("Authorization", $"Basic {credentials}");
                request.Headers.Add("Cache-Control", "no-cache");

                var response = await _httpClient.SendAsync(request);
                var content = await response.Content.ReadAsStringAsync();

                _logger.LogInformation("Token response: {Response}", content);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("✅ Access token obtained successfully from API");
                    var tokenResponse = JsonSerializer.Deserialize<MpesaTokenResponse>(content);
                    var accessToken = tokenResponse?.access_token ?? string.Empty;
                    
                    if (!string.IsNullOrEmpty(accessToken))
                    {
                        // Parse expires_in (usually in seconds) and calculate expiration time
                        var expiresInSeconds = int.TryParse(tokenResponse.expires_in, out var seconds) ? seconds : 3600; // Default 1 hour
                        var expiresAt = DateTime.UtcNow.AddSeconds(expiresInSeconds);

                        // Deactivate old tokens
                        var oldTokens = await _context.MpesaTokens
                            .Where(t => t.IsActive)
                            .ToListAsync();
                        
                        foreach (var oldToken in oldTokens)
                        {
                            oldToken.IsActive = false;
                        }

                        // Save new token to database
                        var newToken = new MpesaToken
                        {
                            AccessToken = accessToken,
                            ExpiresAt = expiresAt,
                            TokenType = "Bearer",
                            IsActive = true,
                            CreatedAt = DateTime.UtcNow
                        };

                        _context.MpesaTokens.Add(newToken);
                        await _context.SaveChangesAsync();

                        _logger.LogInformation("💾 Token saved to database, expires at: {ExpiresAt}", expiresAt);
                        return accessToken;
                    }
                    
                    throw new Exception("Empty access token received from MPESA API");
                }
                else
                {
                    _logger.LogError("❌ Failed to get access token. Status: {StatusCode}, Response: {Response}", response.StatusCode, content);
                    _logger.LogError("Request URL was: {RequestUrl}", requestUrl);
                    _logger.LogError("Authorization header: Basic {AuthHeader}", credentials.Substring(0, Math.Min(20, credentials.Length)) + "...");
                    
                    var errorMessage = "Failed to get MPESA access token";
                    if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        errorMessage = "MPESA authentication failed - check consumer key and secret";
                    }
                    else if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
                    {
                        errorMessage = "MPESA bad request - check API configuration";
                    }
                    
                    throw new Exception($"{errorMessage}: {response.StatusCode} | Response: {content} | URL: {requestUrl}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 Error getting MPESA access token");
                throw;
            }
        }

        public async Task<StkPushResponse> InitiateStkPushAsync(string phoneNumber, decimal amount, string accountReference, string transactionDescription)
        {
            try
            {
                _logger.LogInformation("📱 Initiating STK Push for {PhoneNumber}, Amount: {Amount}", phoneNumber, amount);

                var accessToken = await GetAccessTokenAsync();
                _logger.LogInformation("🔑 Access token obtained (first 20 chars): {Token}...", 
                    accessToken?.Substring(0, Math.Min(20, accessToken?.Length ?? 0)));
                
                var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
                var password = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_settings.Shortcode}{_settings.Passkey}{timestamp}"));
                
                _logger.LogInformation("🔐 STK Push params - Shortcode: {Shortcode}, Timestamp: {Timestamp}", 
                    _settings.Shortcode, timestamp);

                var stkRequest = new StkPushRequest
                {
                    BusinessShortCode = _settings.Shortcode, // Store/HO number (3560959)
                    Password = password,
                    Timestamp = timestamp,
                    TransactionType = string.IsNullOrEmpty(_settings.TransactionType) ? "CustomerBuyGoodsOnline" : _settings.TransactionType, // Changed default
                    Amount = amount.ToString("F0"),
                    PartyA = phoneNumber,
                    PartyB = string.IsNullOrEmpty(_settings.TillNumber) ? _settings.Shortcode : _settings.TillNumber, // Use Till number (6509715)
                    PhoneNumber = phoneNumber,
                    CallBackURL = _settings.CallbackUrl,
                    AccountReference = accountReference.Length > 12 ? accountReference.Substring(0, 12) : accountReference, // Max 12 chars
                    TransactionDesc = transactionDescription.Length > 13 ? transactionDescription.Substring(0, 13) : transactionDescription // Max 13 chars
                };

                var json = JsonSerializer.Serialize(stkRequest);
                _logger.LogInformation("STK Push request: {Request}", json);

                var request = new HttpRequestMessage(HttpMethod.Post, $"{_settings.BaseUrl}/mpesa/stkpush/v1/processrequest");
                request.Headers.Add("Authorization", $"Bearer {accessToken}");
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.SendAsync(request);
                var content = await response.Content.ReadAsStringAsync();

                _logger.LogInformation("STK Push response: {Response}", content);

                if (response.IsSuccessStatusCode)
                {
                    var stkResponse = JsonSerializer.Deserialize<StkPushResponse>(content);
                    _logger.LogInformation("✅ STK Push initiated successfully: {CheckoutRequestID}", stkResponse?.CheckoutRequestID);
                    return stkResponse ?? new StkPushResponse();
                }
                else
                {
                    _logger.LogError("❌ STK Push failed: {StatusCode} - {Content}", response.StatusCode, content);
                    throw new Exception($"STK Push failed: {response.StatusCode} - {content}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 Error initiating STK Push");
                throw;
            }
        }

        public async Task<object> QueryStkPushStatusAsync(string checkoutRequestId)
        {
            try
            {
                _logger.LogInformation("🔍 Querying STK Push status for: {CheckoutRequestID}", checkoutRequestId);

                var accessToken = await GetAccessTokenAsync();
                var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
                var password = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_settings.Shortcode}{_settings.Passkey}{timestamp}"));

                var queryRequest = new
                {
                    BusinessShortCode = _settings.Shortcode,
                    Password = password,
                    Timestamp = timestamp,
                    CheckoutRequestID = checkoutRequestId
                };

                var json = JsonSerializer.Serialize(queryRequest);
                var request = new HttpRequestMessage(HttpMethod.Post, $"{_settings.BaseUrl}/mpesa/stkpushquery/v1/query");
                request.Headers.Add("Authorization", $"Bearer {accessToken}");
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.SendAsync(request);
                var content = await response.Content.ReadAsStringAsync();

                _logger.LogInformation("📥 STK Push query response: {Response}", content);

                return JsonSerializer.Deserialize<object>(content) ?? new object();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 Error querying STK Push status");
                throw;
            }
        }

        public async Task<object> GenerateQRCodeAsync(string merchantName, string refNo, decimal amount, string trxCode = "BG", string size = "300")
        {
            try
            {
                _logger.LogInformation("📱 Generating QR Code for merchant: {MerchantName}, Amount: {Amount}", merchantName, amount);

                var accessToken = await GetAccessTokenAsync();

                var qrRequest = new
                {
                    MerchantName = merchantName,
                    RefNo = refNo,
                    Amount = amount,
                    TrxCode = trxCode, // BG for Buy Goods
                    CPI = string.IsNullOrEmpty(_settings.TillNumber) ? _settings.Shortcode : _settings.TillNumber, // Use Till Number for Buy Goods
                    Size = size
                };

                var json = JsonSerializer.Serialize(qrRequest);
                _logger.LogInformation("QR Code request: {Request}", json);

                var request = new HttpRequestMessage(HttpMethod.Post, $"{_settings.BaseUrl}/mpesa/qrcode/v1/generate");
                request.Headers.Add("Authorization", $"Bearer {accessToken}");
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.SendAsync(request);
                var content = await response.Content.ReadAsStringAsync();

                _logger.LogInformation("QR Code response: {Response}", content);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("✅ QR Code generated successfully");
                    return JsonSerializer.Deserialize<object>(content) ?? new object();
                }
                else
                {
                    _logger.LogError("❌ QR Code generation failed: {StatusCode} - {Content}", response.StatusCode, content);
                    throw new Exception($"QR Code generation failed: {response.StatusCode} - {content}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 Error generating QR Code");
                throw;
            }
        }

        public async Task<object> RegisterC2BUrlsAsync()
        {
            try
            {
                _logger.LogInformation("📝 Registering C2B URLs for shortcode: {Shortcode}", _settings.Shortcode);

                var accessToken = await GetAccessTokenAsync();

                var registerRequest = new
                {
                    ShortCode = _settings.Shortcode,
                    ResponseType = "Completed", // or "Cancelled"
                    ConfirmationURL = _settings.ConfirmationUrl,
                    ValidationURL = _settings.ValidationUrl
                };

                var json = JsonSerializer.Serialize(registerRequest);
                _logger.LogInformation("C2B URL registration request: {Request}", json);

                var request = new HttpRequestMessage(HttpMethod.Post, $"{_settings.BaseUrl}/mpesa/c2b/v1/registerurl");
                request.Headers.Add("Authorization", $"Bearer {accessToken}");
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.SendAsync(request);
                var content = await response.Content.ReadAsStringAsync();

                _logger.LogInformation("C2B URL registration response: {Response}", content);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("✅ C2B URLs registered successfully");
                    return JsonSerializer.Deserialize<object>(content) ?? new object();
                }
                else
                {
                    _logger.LogError("❌ C2B URL registration failed: {StatusCode} - {Content}", response.StatusCode, content);
                    throw new Exception($"C2B URL registration failed: {response.StatusCode} - {content}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 Error registering C2B URLs");
                throw;
            }
        }
    }
}
