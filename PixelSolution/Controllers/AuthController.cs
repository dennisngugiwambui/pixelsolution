using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using PixelSolution.Data;
using PixelSolution.Services.Interfaces;
using PixelSolution.ViewModels;
using System.Security.Claims;
using PixelSolution.Services;

namespace PixelSolution.Controllers
{
    public class AuthController : Controller
    {
        private readonly IAuthService _authService;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AuthController> _logger;

        public AuthController(IAuthService authService, ApplicationDbContext context, ILogger<AuthController> logger)
        {
            _authService = authService;
            _context = context;
            _logger = logger;
        }

        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            try
            {
                // If user is already authenticated, redirect to dashboard
                if (User.Identity?.IsAuthenticated == true)
                {
                    return RedirectToAction("Dashboard", "Admin");
                }

                ViewData["ReturnUrl"] = returnUrl;
                return View(new LoginViewModel());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading login page");
                ViewBag.ErrorMessage = "An error occurred loading the login page.";
                return View(new LoginViewModel());
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            try
            {
                _logger.LogInformation($"🔑 FINAL LOGIN ATTEMPT: {model?.Email ?? "NULL"}");

                if (model == null || !ModelState.IsValid)
                {
                    ViewBag.ErrorMessage = "Please check your input and try again.";
                    ViewData["ReturnUrl"] = returnUrl;
                    return View(model ?? new LoginViewModel());
                }

                var email = model.Email?.Trim();
                var password = model.Password?.Trim();

                if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
                {
                    ViewBag.ErrorMessage = "Please enter both email and password.";
                    ViewData["ReturnUrl"] = returnUrl;
                    return View(model);
                }

                _logger.LogInformation($"🔍 Authenticating: {email}");

                // The AuthService now handles all password fixing automatically
                var user = await _authService.AuthenticateAsync(email, password);

                if (user == null)
                {
                    _logger.LogWarning($"❌ FINAL AUTHENTICATION FAILED for {email}");
                    ViewBag.ErrorMessage = "Invalid email or password. Please check your credentials.";
                    ViewData["ReturnUrl"] = returnUrl;
                    return View(model);
                }

                _logger.LogInformation($"✅ FINAL LOGIN SUCCESS: {user.Email}");

                // Create authentication cookie
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                    new Claim(ClaimTypes.Name, user.FullName),
                    new Claim(ClaimTypes.Email, user.Email),
                    new Claim(ClaimTypes.Role, user.UserType),
                    new Claim("Privileges", user.Privileges ?? "Limited"),
                    new Claim("UserType", user.UserType),
                    new Claim("Status", user.Status)
                };

                if (user.UserDepartments != null && user.UserDepartments.Any())
                {
                    var departmentIds = string.Join(",", user.UserDepartments.Select(ud => ud.DepartmentId));
                    var departmentNames = string.Join(",", user.UserDepartments
                        .Where(ud => ud.Department != null)
                        .Select(ud => ud.Department.Name));

                    claims.Add(new Claim("DepartmentIds", departmentIds));
                    claims.Add(new Claim("DepartmentNames", departmentNames));
                }

                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                var claimsPrincipal = new ClaimsPrincipal(claimsIdentity);

                var authProperties = new AuthenticationProperties
                {
                    IsPersistent = model.RememberMe,
                    ExpiresUtc = model.RememberMe ? DateTimeOffset.UtcNow.AddDays(30) : DateTimeOffset.UtcNow.AddHours(8)
                };

                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, claimsPrincipal, authProperties);

                _logger.LogInformation($"🎉 USER LOGGED IN SUCCESSFULLY: {user.Email}");

                var redirectUrl = GetRedirectUrl(returnUrl, user.UserType);

                if (Request.Headers.ContainsKey("X-Requested-With") &&
                    Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = true, redirectUrl = redirectUrl });
                }

                TempData["SuccessMessage"] = $"Welcome back, {user.FullName}!";
                return Redirect(redirectUrl);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 FINAL LOGIN ERROR for {Email}", model?.Email ?? "NULL");
                ViewBag.ErrorMessage = "An error occurred during login. Please try again.";
                ViewData["ReturnUrl"] = returnUrl;
                return View(model ?? new LoginViewModel());
            }
        }

        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            try
            {
                var userEmail = User.FindFirst(ClaimTypes.Email)?.Value;
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

                _logger.LogInformation($"👋 User {userEmail} logged out successfully");
                TempData["InfoMessage"] = "You have been logged out successfully.";

                return RedirectToAction("Login");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 Error during logout");
                return RedirectToAction("Login");
            }
        }

        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                    return Json(new { success = false, message = string.Join("; ", errors) });
                }

                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!int.TryParse(userIdClaim, out int userId))
                {
                    return Json(new { success = false, message = "User not found." });
                }

                var result = await _authService.ChangePasswordAsync(userId, model.CurrentPassword, model.NewPassword);

                if (result)
                {
                    _logger.LogInformation($"🔐 User {userId} changed password successfully");
                    return Json(new { success = true, message = "Password changed successfully." });
                }
                else
                {
                    return Json(new { success = false, message = "Current password is incorrect." });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 Error changing password for user");
                return Json(new { success = false, message = "An error occurred while changing password." });
            }
        }

        [HttpGet]
        [Route("/debug/test-password")]
        public async Task<IActionResult> TestPassword(string email = "dennisngugi219@gmail.com", string password = "Admin1234")
        {
            try
            {
                if (!HttpContext.Request.Host.Host.Contains("localhost") &&
                    !HttpContext.Request.Host.Host.Contains("127.0.0.1"))
                {
                    return NotFound();
                }

                _logger.LogInformation($"🧪 Testing password for: {email}");

                // Get user from database
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower());

                if (user == null)
                {
                    return Json(new { success = false, error = "User not found" });
                }

                // Test direct password validation
                var isValid = _authService.ValidatePassword(password, user.PasswordHash);

                // Test BCrypt directly
                bool directBcryptTest = false;
                try
                {
                    directBcryptTest = BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);
                }
                catch (Exception bcryptEx)
                {
                    _logger.LogError(bcryptEx, "Direct BCrypt test failed");
                }

                // Test creating a new hash and comparing
                string newHash = _authService.HashPassword(password);
                bool newHashTest = _authService.ValidatePassword(password, newHash);

                return Json(new
                {
                    success = true,
                    user = new { email = user.Email, userType = user.UserType },
                    passwordTest = new
                    {
                        inputPassword = password,
                        storedHash = user.PasswordHash.Substring(0, Math.Min(20, user.PasswordHash.Length)) + "...",
                        hashStartsWith = user.PasswordHash.Substring(0, Math.Min(10, user.PasswordHash.Length)),
                        isBcryptFormat = user.PasswordHash.StartsWith("$2"),
                        serviceValidationResult = isValid,
                        directBcryptResult = directBcryptTest,
                        newHashTest = newHashTest,
                        newHashCreated = newHash.Substring(0, Math.Min(20, newHash.Length)) + "..."
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 Error testing password");
                return Json(new
                {
                    success = false,
                    error = ex.Message
                });
            }
        }

        [HttpGet]
        [Route("/fix-admin-now")]
        public async Task<IActionResult> FixAdminNow()
        {
            try
            {
                _logger.LogWarning("🚨 MANUAL ADMIN FIX REQUESTED");

                var authService = _authService as AuthService;
                if (authService == null)
                {
                    return Json(new { success = false, message = "Service not available" });
                }

                bool result = await authService.ForceFixAdminPasswordAsync();

                if (result)
                {
                    return Json(new
                    {
                        success = true,
                        message = "✅ ADMIN PASSWORD FIXED! Use: dennisngugi219@gmail.com / Admin1234"
                    });
                }
                else
                {
                    return Json(new { success = false, message = "❌ Fix failed" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 Error in manual admin fix");
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        private string GetRedirectUrl(string? returnUrl, string userType)
        {
            // If returnUrl is provided and is local, use it
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return returnUrl;
            }

            // Default redirects based on user type - all go to Admin Dashboard
            return Url.Action("Dashboard", "Admin") ?? "/Admin/Dashboard";
        }

        private bool IsValidEmail(string email)
        {
            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                return addr.Address == email;
            }
            catch
            {
                return false;
            }
        }
    }
}