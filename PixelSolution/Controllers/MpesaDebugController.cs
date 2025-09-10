using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using PixelSolution.Models;
using PixelSolution.Services;
using System.Text;
using System.Text.Json;

namespace PixelSolution.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MpesaDebugController : ControllerBase
    {
        private readonly MpesaSettings _settings;
        private readonly HttpClient _httpClient;
        private readonly ILogger<MpesaDebugController> _logger;

        public MpesaDebugController(IOptions<MpesaSettings> settings, ILogger<MpesaDebugController> logger)
        {
            _settings = settings.Value;
            _httpClient = new HttpClient();
            _logger = logger;
        }

        [HttpGet("test-endpoints")]
        public async Task<IActionResult> TestEndpoints()
        {
            var results = new List<object>();
            
            // Test different endpoint combinations
            var endpoints = new[]
            {
                "https://sandbox.safaricom.co.ke/oauth/v1/generate?grant_type=client_credentials",
                "https://api.safaricom.co.ke/oauth/v1/generate?grant_type=client_credentials",
                "https://sandbox.safaricom.co.ke/oauth/v1/generate",
                "https://api.safaricom.co.ke/oauth/v1/generate"
            };

            foreach (var endpoint in endpoints)
            {
                try
                {
                    _logger.LogInformation("Testing endpoint: {Endpoint}", endpoint);
                    
                    var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_settings.ConsumerKey}:{_settings.ConsumerSecret}"));
                    
                    var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
                    request.Headers.Add("Authorization", $"Basic {credentials}");
                    request.Headers.Add("Cache-Control", "no-cache");

                    var response = await _httpClient.SendAsync(request);
                    var content = await response.Content.ReadAsStringAsync();

                    results.Add(new
                    {
                        endpoint = endpoint,
                        statusCode = (int)response.StatusCode,
                        statusDescription = response.StatusCode.ToString(),
                        success = response.IsSuccessStatusCode,
                        response = content,
                        headers = response.Headers.ToDictionary(h => h.Key, h => string.Join(", ", h.Value))
                    });
                }
                catch (Exception ex)
                {
                    results.Add(new
                    {
                        endpoint = endpoint,
                        error = ex.Message,
                        success = false
                    });
                }
            }

            return Ok(new
            {
                consumerKeyPreview = _settings.ConsumerKey?.Substring(0, Math.Min(10, _settings.ConsumerKey?.Length ?? 0)) + "...",
                consumerSecretPreview = _settings.ConsumerSecret?.Substring(0, Math.Min(4, _settings.ConsumerSecret?.Length ?? 0)) + "...",
                shortcode = _settings.Shortcode,
                results = results
            });
        }

        [HttpPost("test-raw-token")]
        public async Task<IActionResult> TestRawToken()
        {
            try
            {
                var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_settings.ConsumerKey}:{_settings.ConsumerSecret}"));
                
                _logger.LogInformation("Raw credentials: {Credentials}", credentials);
                _logger.LogInformation("Consumer Key: {Key}", _settings.ConsumerKey);
                _logger.LogInformation("Consumer Secret: {Secret}", _settings.ConsumerSecret?.Substring(0, 4) + "...");

                using var client = new HttpClient();
                var request = new HttpRequestMessage(HttpMethod.Get, "https://sandbox.safaricom.co.ke/oauth/v1/generate?grant_type=client_credentials");
                request.Headers.Add("Authorization", $"Basic {credentials}");

                var response = await client.SendAsync(request);
                var content = await response.Content.ReadAsStringAsync();

                return Ok(new
                {
                    statusCode = (int)response.StatusCode,
                    statusDescription = response.StatusCode.ToString(),
                    content = content,
                    success = response.IsSuccessStatusCode,
                    requestUrl = "https://sandbox.safaricom.co.ke/oauth/v1/generate?grant_type=client_credentials",
                    authHeader = $"Basic {credentials.Substring(0, Math.Min(20, credentials.Length))}..."
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    error = ex.Message,
                    stackTrace = ex.StackTrace
                });
            }
        }
    }
}
