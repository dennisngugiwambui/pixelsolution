using Microsoft.Extensions.Options;
using PixelSolution.Services;
using System.Text;

namespace PixelSolution.Middleware
{
    /// <summary>
    /// Laravel-style middleware for M-Pesa token generation
    /// Equivalent to GetToken::class middleware in Laravel
    /// </summary>
    public class MpesaTokenMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly MpesaSettings _settings;
        private readonly ILogger<MpesaTokenMiddleware> _logger;
        private readonly HttpClient _httpClient;

        public MpesaTokenMiddleware(RequestDelegate next, IOptions<MpesaSettings> settings, 
            ILogger<MpesaTokenMiddleware> logger, HttpClient httpClient)
        {
            _next = next;
            _settings = settings.Value;
            _logger = logger;
            _httpClient = httpClient;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                _logger.LogInformation("🔑 M-Pesa Token Middleware - Generating access token...");

                // Laravel equivalent: $consumer_key = "kALA3qqUCYJ8xmaEpQF0LyvDAOMBwOIlD31aWJVQ4RbISvA7";
                var consumer_key = _settings.ConsumerKey;
                
                // Laravel equivalent: $consumer_secret = "slLXYm43tJJnaOOFApaBxBvuN1bFMEVXlfaolAtiMJJJlISOWWpJLtGEG9hNafTJ";
                var consumer_secret = _settings.ConsumerSecret;
                
                // Laravel equivalent: $auth_key = base64_encode($consumer_key . ":" .$consumer_secret);
                var auth_key = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{consumer_key}:{consumer_secret}"));

                _logger.LogInformation("Auth Key (first 20 chars): {AuthKey}", auth_key.Substring(0, Math.Min(20, auth_key.Length)) + "...");

                // Laravel equivalent: Http::withHeaders(['Authorization' => 'Basic ' . $auth_key])
                var request = new HttpRequestMessage(HttpMethod.Get, 
                    "https://sandbox.safaricom.co.ke/oauth/v1/generate?grant_type=client_credentials");
                request.Headers.Add("Authorization", $"Basic {auth_key}");

                var response = await _httpClient.SendAsync(request);
                var content = await response.Content.ReadAsStringAsync();

                _logger.LogInformation("Token response: {Response}", content);

                if (response.IsSuccessStatusCode)
                {
                    var tokenResponse = System.Text.Json.JsonSerializer.Deserialize<MpesaTokenResponse>(content);
                    var accessToken = tokenResponse?.access_token ?? string.Empty;

                    if (!string.IsNullOrEmpty(accessToken))
                    {
                        // Laravel equivalent: $request->request->add(['token' => $response["access_token"]]);
                        context.Items["mpesa_token"] = accessToken;
                        _logger.LogInformation("✅ Token added to request context");
                    }
                    else
                    {
                        _logger.LogError("❌ Empty access token received");
                        context.Response.StatusCode = 500;
                        await context.Response.WriteAsync("Failed to generate M-Pesa token");
                        return;
                    }
                }
                else
                {
                    _logger.LogError("❌ Token generation failed: {StatusCode} - {Content}", response.StatusCode, content);
                    context.Response.StatusCode = 500;
                    await context.Response.WriteAsync($"M-Pesa token generation failed: {response.StatusCode}");
                    return;
                }

                // Laravel equivalent: return $next($request);
                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 M-Pesa Token Middleware error");
                context.Response.StatusCode = 500;
                await context.Response.WriteAsync("M-Pesa token middleware error");
            }
        }
    }
}
