using Microsoft.AspNetCore.Http;
using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace PixelSolution.Middleware
{
    /// <summary>
    /// Comprehensive security middleware to protect against various attacks
    /// </summary>
    public class SecurityMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<SecurityMiddleware> _logger;
        
        // Rate limiting storage
        private static readonly ConcurrentDictionary<string, RateLimitInfo> _rateLimitStore = new();
        private static readonly ConcurrentDictionary<string, FailedLoginInfo> _failedLogins = new();
        
        // Configuration
        private const int MAX_REQUESTS_PER_MINUTE = 10;
        private const int MAX_STK_REQUESTS_PER_MINUTE = 3;
        private const int MAX_LOGIN_ATTEMPTS = 5;
        private const int LOCKOUT_DURATION_MINUTES = 30;
        
        public SecurityMiddleware(RequestDelegate next, ILogger<SecurityMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var clientIp = GetClientIpAddress(context);
            var path = context.Request.Path.Value?.ToLower() ?? "";
            
            try
            {
                // 1. HTTPS Enforcement (except for development)
                if (!context.Request.IsHttps && !IsLocalRequest(context))
                {
                    _logger.LogWarning("⚠️ Non-HTTPS request detected from {IP} to {Path}", clientIp, path);
                    context.Response.Redirect($"https://{context.Request.Host}{context.Request.Path}{context.Request.QueryString}");
                    return;
                }
                
                // 2. Security Headers
                AddSecurityHeaders(context);
                
                // 3. SQL Injection Detection
                if (ContainsSqlInjection(context))
                {
                    _logger.LogWarning("🚨 SQL Injection attempt detected from {IP} to {Path}", clientIp, path);
                    await BlockRequest(context, "Invalid request detected");
                    return;
                }
                
                // 4. XSS Protection
                if (ContainsXssAttempt(context))
                {
                    _logger.LogWarning("🚨 XSS attempt detected from {IP} to {Path}", clientIp, path);
                    await BlockRequest(context, "Invalid request detected");
                    return;
                }
                
                // 5. Rate Limiting for Login
                if (path.Contains("/auth/login"))
                {
                    if (IsAccountLocked(clientIp))
                    {
                        _logger.LogWarning("🔒 Locked account attempted login from {IP}", clientIp);
                        await BlockRequest(context, "Account temporarily locked due to multiple failed attempts");
                        return;
                    }
                    
                    if (!CheckRateLimit(clientIp, "login", MAX_LOGIN_ATTEMPTS))
                    {
                        _logger.LogWarning("⚠️ Rate limit exceeded for login from {IP}", clientIp);
                        await BlockRequest(context, "Too many login attempts. Please try again later.");
                        return;
                    }
                }
                
                // 6. Rate Limiting for STK Push
                if (path.Contains("/sales/processale") || path.Contains("/mpesa") || path.Contains("/stk"))
                {
                    if (!CheckRateLimit(clientIp, "stkpush", MAX_STK_REQUESTS_PER_MINUTE))
                    {
                        _logger.LogWarning("⚠️ Rate limit exceeded for STK Push from {IP}", clientIp);
                        await BlockRequest(context, "Too many payment requests. Please try again in a minute.");
                        return;
                    }
                }
                
                // 7. General Rate Limiting
                if (!CheckRateLimit(clientIp, "general", MAX_REQUESTS_PER_MINUTE * 2))
                {
                    _logger.LogWarning("⚠️ General rate limit exceeded from {IP}", clientIp);
                    await BlockRequest(context, "Too many requests. Please slow down.");
                    return;
                }
                
                // 8. Request Size Validation (prevent DOS)
                if (context.Request.ContentLength > 10 * 1024 * 1024) // 10MB limit
                {
                    _logger.LogWarning("⚠️ Large request detected from {IP}: {Size}MB", clientIp, context.Request.ContentLength / 1024 / 1024);
                    await BlockRequest(context, "Request too large");
                    return;
                }
                
                // 9. Validate User-Agent (block suspicious bots)
                var userAgent = context.Request.Headers["User-Agent"].ToString();
                if (string.IsNullOrEmpty(userAgent) || IsSuspiciousUserAgent(userAgent))
                {
                    _logger.LogWarning("⚠️ Suspicious user agent from {IP}: {UserAgent}", clientIp, userAgent);
                    await BlockRequest(context, "Invalid request");
                    return;
                }
                
                // Continue to next middleware
                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Security middleware error for {IP} to {Path}", clientIp, path);
                throw;
            }
        }

        private void AddSecurityHeaders(HttpContext context)
        {
            var headers = context.Response.Headers;
            
            // Prevent clickjacking
            headers["X-Frame-Options"] = "DENY";
            
            // XSS Protection
            headers["X-XSS-Protection"] = "1; mode=block";
            
            // Content Type Options
            headers["X-Content-Type-Options"] = "nosniff";
            
            // Referrer Policy
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            
            // Content Security Policy
            headers["Content-Security-Policy"] = 
                "default-src 'self'; " +
                "script-src 'self' 'unsafe-inline' 'unsafe-eval' https://cdn.jsdelivr.net https://cdnjs.cloudflare.com; " +
                "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com https://cdnjs.cloudflare.com; " +
                "font-src 'self' https://fonts.gstatic.com https://cdnjs.cloudflare.com; " +
                "img-src 'self' data: https:; " +
                "connect-src 'self' https://api.safaricom.co.ke; " +
                "frame-ancestors 'none';";
            
            // HSTS (HTTP Strict Transport Security)
            if (context.Request.IsHttps)
            {
                headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";
            }
            
            // Permissions Policy
            headers["Permissions-Policy"] = "geolocation=(), microphone=(), camera=()";
        }

        private bool ContainsSqlInjection(HttpContext context)
        {
            var suspiciousPatterns = new[]
            {
                @"(\bOR\b|\bAND\b)\s+\d+\s*=\s*\d+",
                @"UNION\s+SELECT",
                @"DROP\s+TABLE",
                @"INSERT\s+INTO",
                @"DELETE\s+FROM",
                @"UPDATE\s+\w+\s+SET",
                @"EXEC(\s|\+)+(s|x)p\w+",
                @"--",
                @";.*--",
                @"xp_cmdshell",
                @"sp_executesql"
            };

            var queryString = context.Request.QueryString.Value ?? "";
            var path = context.Request.Path.Value ?? "";
            
            foreach (var pattern in suspiciousPatterns)
            {
                if (Regex.IsMatch(queryString, pattern, RegexOptions.IgnoreCase) ||
                    Regex.IsMatch(path, pattern, RegexOptions.IgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private bool ContainsXssAttempt(HttpContext context)
        {
            var suspiciousPatterns = new[]
            {
                @"<script[^>]*>.*?</script>",
                @"javascript:",
                @"onerror\s*=",
                @"onload\s*=",
                @"<iframe",
                @"<object",
                @"<embed",
                @"eval\(",
                @"alert\(",
                @"document\.cookie"
            };

            var queryString = context.Request.QueryString.Value ?? "";
            
            foreach (var pattern in suspiciousPatterns)
            {
                if (Regex.IsMatch(queryString, pattern, RegexOptions.IgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private bool CheckRateLimit(string clientIp, string category, int maxRequests)
        {
            var key = $"{clientIp}:{category}";
            var now = DateTime.UtcNow;
            
            var rateLimitInfo = _rateLimitStore.GetOrAdd(key, new RateLimitInfo());
            
            lock (rateLimitInfo)
            {
                // Remove old requests (older than 1 minute)
                rateLimitInfo.Requests.RemoveAll(r => (now - r).TotalMinutes > 1);
                
                // Check if limit exceeded
                if (rateLimitInfo.Requests.Count >= maxRequests)
                {
                    return false;
                }
                
                // Add current request
                rateLimitInfo.Requests.Add(now);
                return true;
            }
        }

        private bool IsAccountLocked(string clientIp)
        {
            if (_failedLogins.TryGetValue(clientIp, out var loginInfo))
            {
                if (loginInfo.LockoutUntil.HasValue && DateTime.UtcNow < loginInfo.LockoutUntil.Value)
                {
                    return true;
                }
            }
            return false;
        }

        public static void RecordFailedLogin(string clientIp)
        {
            var loginInfo = _failedLogins.GetOrAdd(clientIp, new FailedLoginInfo());
            
            lock (loginInfo)
            {
                loginInfo.FailedAttempts++;
                loginInfo.LastAttempt = DateTime.UtcNow;
                
                if (loginInfo.FailedAttempts >= MAX_LOGIN_ATTEMPTS)
                {
                    loginInfo.LockoutUntil = DateTime.UtcNow.AddMinutes(LOCKOUT_DURATION_MINUTES);
                }
            }
        }

        public static void RecordSuccessfulLogin(string clientIp)
        {
            _failedLogins.TryRemove(clientIp, out _);
        }

        private bool IsSuspiciousUserAgent(string userAgent)
        {
            var suspiciousAgents = new[]
            {
                "sqlmap",
                "nikto",
                "nmap",
                "masscan",
                "nessus",
                "openvas",
                "python-requests",
                "curl",
                "wget"
            };

            return suspiciousAgents.Any(agent => 
                userAgent.Contains(agent, StringComparison.OrdinalIgnoreCase));
        }

        private string GetClientIpAddress(HttpContext context)
        {
            var ipAddress = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (string.IsNullOrEmpty(ipAddress))
            {
                ipAddress = context.Request.Headers["X-Real-IP"].FirstOrDefault();
            }
            if (string.IsNullOrEmpty(ipAddress))
            {
                ipAddress = context.Connection.RemoteIpAddress?.ToString();
            }
            return ipAddress ?? "Unknown";
        }

        private bool IsLocalRequest(HttpContext context)
        {
            var ip = context.Connection.RemoteIpAddress;
            return ip != null && (IPAddress.IsLoopback(ip) || ip.ToString().StartsWith("192.168.") || ip.ToString().StartsWith("10."));
        }

        private async Task BlockRequest(HttpContext context, string message)
        {
            context.Response.StatusCode = 429; // Too Many Requests
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync($"{{\"error\": \"{message}\"}}");
        }

        private class RateLimitInfo
        {
            public List<DateTime> Requests { get; set; } = new List<DateTime>();
        }

        private class FailedLoginInfo
        {
            public int FailedAttempts { get; set; }
            public DateTime LastAttempt { get; set; }
            public DateTime? LockoutUntil { get; set; }
        }
    }
}
