using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace PixelSolution.Services
{
    public class MpesaSettings
    {
        public string ConsumerKey { get; set; } = string.Empty;
        public string ConsumerSecret { get; set; } = string.Empty;
        public string Shortcode { get; set; } = string.Empty;
        public string Passkey { get; set; } = string.Empty;
        public string CallbackUrl { get; set; } = string.Empty;
        public string BaseUrl { get; set; } = "https://sandbox.safaricom.co.ke"; // Use sandbox for testing
        public bool IsSandbox { get; set; } = true;
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
    }

    public class MpesaService : IMpesaService
    {
        private readonly HttpClient _httpClient;
        private readonly MpesaSettings _settings;
        private readonly ILogger<MpesaService> _logger;

        public MpesaService(HttpClient httpClient, IOptions<MpesaSettings> settings, ILogger<MpesaService> logger)
        {
            _httpClient = httpClient;
            _settings = settings.Value;
            _logger = logger;
        }

        public async Task<string> GetAccessTokenAsync()
        {
            try
            {
                _logger.LogInformation("Getting MPESA access token...");
                _logger.LogInformation("MPESA Config - BaseUrl: {BaseUrl}, ConsumerKey: {ConsumerKey}, ConsumerSecret: {ConsumerSecret}", 
                    _settings.BaseUrl, _settings.ConsumerKey?.Substring(0, Math.Min(8, _settings.ConsumerKey.Length)) + "...", 
                    _settings.ConsumerSecret?.Substring(0, Math.Min(4, _settings.ConsumerSecret.Length)) + "...");

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
                    _logger.LogInformation("Access token obtained successfully");
                    var tokenResponse = JsonSerializer.Deserialize<MpesaTokenResponse>(content);
                    return tokenResponse?.access_token ?? string.Empty;
                }
                else
                {
                    _logger.LogError("Failed to get access token. Status: {StatusCode}, Response: {Response}", response.StatusCode, content);
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
                _logger.LogInformation("Initiating STK Push for {PhoneNumber}, Amount: {Amount}", phoneNumber, amount);

                var accessToken = await GetAccessTokenAsync();
                var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
                var password = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_settings.Shortcode}{_settings.Passkey}{timestamp}"));

                var stkRequest = new StkPushRequest
                {
                    BusinessShortCode = _settings.Shortcode,
                    Password = password,
                    Timestamp = timestamp,
                    Amount = amount.ToString("F0"),
                    PartyA = phoneNumber,
                    PartyB = _settings.Shortcode,
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
    }
}
