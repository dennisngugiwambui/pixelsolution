using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PixelSolution.Data;
using PixelSolution.Services.Interfaces;
using BCrypt.Net;

namespace PixelSolution.Controllers
{
    public class DebugPasswordController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuthService _authService;
        private readonly ILogger<DebugPasswordController> _logger;

        public DebugPasswordController(ApplicationDbContext context, IAuthService authService, ILogger<DebugPasswordController> logger)
        {
            _context = context;
            _authService = authService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> TestPassword()
        {
            var results = new List<object>();

            try
            {
                _logger.LogInformation("Starting password debug tests");

                // Test 1: Check database connection
                try
                {
                    var userCount = await _context.Users.CountAsync();
                    results.Add(new { 
                        Test = "Database Connection", 
                        Result = "PASSED", 
                        Message = $"Connected to database. Total users: {userCount}" 
                    });
                }
                catch (Exception ex)
                {
                    results.Add(new { 
                        Test = "Database Connection", 
                        Result = "FAILED", 
                        Message = $"Database connection failed: {ex.Message}" 
                    });
                    return Json(results);
                }

                // Test 2: Check if admin user exists
                var adminUser = await _context.Users
                    .FirstOrDefaultAsync(u => u.Email != null && u.Email.ToLower() == "dennisngugi219@gmail.com");

                if (adminUser == null)
                {
                    results.Add(new { Test = "Admin User Exists", Result = "FAILED", Message = "Admin user not found in database" });
                    
                    // List all users to help debug
                    var allUsers = await _context.Users.Select(u => new { u.Email, u.UserType }).ToListAsync();
                    results.Add(new { 
                        Test = "All Users", 
                        Result = "INFO", 
                        Message = $"Found users: {string.Join(", ", allUsers.Select(u => $"{u.Email} ({u.UserType})"))}" 
                    });
                    return Json(results);
                }

                results.Add(new { 
                    Test = "Admin User Exists", 
                    Result = "PASSED", 
                    Message = $"Found user: {adminUser.Email ?? "NULL"}, Status: {adminUser.Status ?? "NULL"}, UserType: {adminUser.UserType ?? "NULL"}" 
                });

                // Test 3: Check current password hash
                var hashInfo = string.IsNullOrEmpty(adminUser.PasswordHash) ? "NULL or EMPTY" : 
                    adminUser.PasswordHash.Length > 20 ? adminUser.PasswordHash.Substring(0, 20) + "..." : adminUser.PasswordHash;
                
                results.Add(new { 
                    Test = "Current Hash", 
                    Result = "INFO", 
                    Message = $"Hash: {hashInfo} (Length: {adminUser.PasswordHash?.Length ?? 0})" 
                });

                if (string.IsNullOrEmpty(adminUser.PasswordHash))
                {
                    results.Add(new { 
                        Test = "Hash Validation", 
                        Result = "FAILED", 
                        Message = "Password hash is null or empty in database" 
                    });
                    return Json(results);
                }

                // Test 4: Test BCrypt verification with Admin1234
                bool bcryptResult = false;
                string bcryptError = "";
                try
                {
                    bcryptResult = BCrypt.Net.BCrypt.Verify("Admin1234", adminUser.PasswordHash);
                    _logger.LogInformation($"BCrypt verification result: {bcryptResult}");
                }
                catch (Exception ex)
                {
                    bcryptError = ex.Message;
                    _logger.LogError(ex, "BCrypt verification failed");
                }

                results.Add(new { 
                    Test = "BCrypt Verify Admin1234", 
                    Result = bcryptResult ? "PASSED" : "FAILED", 
                    Message = bcryptResult ? "Password matches hash" : $"Password does not match hash. Error: {bcryptError}" 
                });

                // Test 5: Test AuthService validation
                bool authServiceResult = false;
                try
                {
                    authServiceResult = _authService.ValidatePassword("Admin1234", adminUser.PasswordHash);
                }
                catch (Exception ex)
                {
                    results.Add(new { 
                        Test = "AuthService ValidatePassword", 
                        Result = "ERROR", 
                        Message = $"AuthService error: {ex.Message}" 
                    });
                }
                
                if (authServiceResult || bcryptResult)
                {
                    results.Add(new { 
                        Test = "AuthService ValidatePassword", 
                        Result = authServiceResult ? "PASSED" : "FAILED", 
                        Message = authServiceResult ? "AuthService validates password" : "AuthService rejects password" 
                    });
                }

                // Test 6: Test full authentication
                try
                {
                    var authResult = await _authService.AuthenticateAsync("dennisngugi219@gmail.com", "Admin1234");
                    results.Add(new { 
                        Test = "Full Authentication", 
                        Result = authResult != null ? "PASSED" : "FAILED", 
                        Message = authResult != null ? $"Authentication successful for {authResult.Email}" : "Authentication failed" 
                    });
                }
                catch (Exception ex)
                {
                    results.Add(new { 
                        Test = "Full Authentication", 
                        Result = "ERROR", 
                        Message = $"Authentication error: {ex.Message}" 
                    });
                }

                // Test 7: Generate new hash and test it
                try
                {
                    string newHash = BCrypt.Net.BCrypt.HashPassword("Admin1234", 11);
                    bool newHashTest = BCrypt.Net.BCrypt.Verify("Admin1234", newHash);
                    results.Add(new { 
                        Test = "New Hash Generation", 
                        Result = newHashTest ? "PASSED" : "FAILED", 
                        Message = $"New hash generated and verified: {newHashTest}" 
                    });

                    // Test 8: Check if we need to update the hash
                    if (!bcryptResult && newHashTest)
                    {
                        results.Add(new { 
                            Test = "Hash Update Needed", 
                            Result = "WARNING", 
                            Message = "Current hash is invalid, but new hash generation works. Database needs update." 
                        });
                    }
                }
                catch (Exception ex)
                {
                    results.Add(new { 
                        Test = "New Hash Generation", 
                        Result = "ERROR", 
                        Message = $"Hash generation error: {ex.Message}" 
                    });
                }

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Overall test error");
                results.Add(new { 
                    Test = "Overall Test", 
                    Result = "ERROR", 
                    Message = $"Exception: {ex.Message}\nStack: {ex.StackTrace}" 
                });
            }

            return Json(results);
        }

        [HttpPost]
        public async Task<IActionResult> FixPassword()
        {
            try
            {
                _logger.LogInformation("Starting password fix for admin user");

                var adminUser = await _context.Users
                    .FirstOrDefaultAsync(u => u.Email != null && u.Email.ToLower() == "dennisngugi219@gmail.com");

                if (adminUser == null)
                {
                    _logger.LogWarning("Admin user not found during password fix");
                    return Json(new { Success = false, Message = "Admin user not found" });
                }

                _logger.LogInformation($"Found admin user: {adminUser.Email}, current hash length: {adminUser.PasswordHash?.Length ?? 0}");

                // Generate new hash with salt rounds 11 (same as your provided hash)
                string newHash = BCrypt.Net.BCrypt.HashPassword("Admin1234", 11);
                _logger.LogInformation($"Generated new hash: {newHash.Substring(0, 20)}...");
                
                // Update the user
                adminUser.PasswordHash = newHash;
                adminUser.UpdatedAt = DateTime.UtcNow;
                adminUser.Status = "Active"; // Ensure user is active
                
                await _context.SaveChangesAsync();
                _logger.LogInformation("Password hash updated in database");

                // Test the new hash
                bool testResult = BCrypt.Net.BCrypt.Verify("Admin1234", newHash);
                _logger.LogInformation($"Hash verification test: {testResult}");

                // Test full authentication
                var authTest = await _authService.AuthenticateAsync("dennisngugi219@gmail.com", "Admin1234");
                bool authSuccess = authTest != null;
                _logger.LogInformation($"Full authentication test: {authSuccess}");

                return Json(new { 
                    Success = true, 
                    Message = $"Password updated successfully! Hash verification: {testResult}, Authentication test: {authSuccess}. You can now login with dennisngugi219@gmail.com and Admin1234" 
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fixing password");
                return Json(new { Success = false, Message = $"Error: {ex.Message}\nStack: {ex.StackTrace}" });
            }
        }

        [HttpGet]
        public IActionResult DebugPage()
        {
            return View();
        }
    }
}
