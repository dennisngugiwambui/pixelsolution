using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PixelSolution.Data;
using PixelSolution.Services.Interfaces;
using BCrypt.Net;

namespace PixelSolution.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PasswordResetController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuthService _authService;
        private readonly ILogger<PasswordResetController> _logger;

        public PasswordResetController(ApplicationDbContext context, IAuthService authService, ILogger<PasswordResetController> logger)
        {
            _context = context;
            _authService = authService;
            _logger = logger;
        }

        [HttpGet("check-database")]
        public async Task<IActionResult> CheckDatabase()
        {
            try
            {
                var users = await _context.Users.ToListAsync();
                var result = users.Select(u => new
                {
                    u.UserId,
                    u.Email,
                    u.FirstName,
                    u.LastName,
                    u.UserType,
                    u.Status,
                    PasswordLength = u.PasswordHash?.Length ?? 0,
                    StoredPassword = u.PasswordHash, // Show actual password in debug mode
                    IsPasswordHashed = u.PasswordHash?.StartsWith("$2") == true
                }).ToList();

                return Ok(new
                {
                    message = "Database check completed",
                    totalUsers = users.Count,
                    users = result
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking database");
                return StatusCode(500, new { message = "Error checking database", error = ex.Message });
            }
        }

        [HttpPost("test-login")]
        public async Task<IActionResult> TestLogin([FromBody] TestLoginRequest request)
        {
            try
            {
                _logger.LogInformation($"=== DEBUG LOGIN TEST ===");
                _logger.LogInformation($"Testing email: {request.Email}");
                _logger.LogInformation($"Testing password: {request.Password}");

                // First, check if user exists
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == request.Email.ToLower());

                if (user == null)
                {
                    return Ok(new
                    {
                        success = false,
                        message = "User not found",
                        details = new
                        {
                            userExists = false,
                            email = request.Email
                        }
                    });
                }

                // Test authentication service
                var authenticatedUser = await _authService.AuthenticateAsync(request.Email, request.Password);

                return Ok(new
                {
                    success = authenticatedUser != null,
                    message = authenticatedUser != null ? "Authentication successful" : "Authentication failed",
                    details = new
                    {
                        userExists = true,
                        userEmail = user.Email,
                        userStatus = user.Status,
                        userType = user.UserType,
                        storedPassword = user.PasswordHash,
                        testedPassword = request.Password,
                        passwordMatch = user.PasswordHash == request.Password,
                        authServiceResult = authenticatedUser != null ? "SUCCESS" : "FAILED"
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing login");
                return StatusCode(500, new { message = "Error testing login", error = ex.Message });
            }
        }

        [HttpPost("reset-to-plain-text")]
        public async Task<IActionResult> ResetToPlainText()
        {
            try
            {
                _logger.LogInformation("Resetting all passwords to plain text for debugging...");

                var users = await _context.Users.ToListAsync();
                var results = new List<object>();

                foreach (var user in users)
                {
                    string plainPassword = user.Email.Contains("dennis") ? "Admin1234" :
                                         user.Email.Contains("sales") ? "Employee123!" : "Manager123!";

                    var oldHash = user.PasswordHash;
                    user.PasswordHash = plainPassword; // Store as plain text
                    user.UpdatedAt = DateTime.UtcNow;

                    results.Add(new
                    {
                        email = user.Email,
                        oldPassword = oldHash,
                        newPassword = plainPassword,
                        isPlainText = true
                    });

                    _logger.LogInformation($"Reset {user.Email} password to plain text: {plainPassword}");
                }

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    message = "All passwords reset to plain text for debugging",
                    results = results
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting passwords to plain text");
                return StatusCode(500, new { message = "Error resetting passwords", error = ex.Message });
            }
        }

        [HttpPost("fix-admin-password")]
        public async Task<IActionResult> FixAdminPassword()
        {
            try
            {
                var adminUser = await _context.Users
                    .FirstOrDefaultAsync(u => u.Email.ToLower() == "dennisngugi219@gmail.com");

                if (adminUser == null)
                {
                    return NotFound(new { message = "Admin user not found" });
                }

                _logger.LogInformation($"Admin user found: {adminUser.Email}");
                _logger.LogInformation($"Old password: {adminUser.PasswordHash}");

                // Set to plain text for debugging
                adminUser.PasswordHash = "Admin1234";
                adminUser.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                _logger.LogInformation($"New password: {adminUser.PasswordHash}");

                return Ok(new
                {
                    message = "Admin password set to plain text",
                    email = adminUser.Email,
                    newPassword = "Admin1234",
                    isPlainText = true
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fixing admin password");
                return StatusCode(500, new { message = "Error fixing password", error = ex.Message });
            }
        }

        [HttpPost("convert-to-bcrypt")]
        public async Task<IActionResult> ConvertToBCrypt()
        {
            try
            {
                var users = await _context.Users.ToListAsync();
                var results = new List<object>();

                foreach (var user in users)
                {
                    // Only convert if it's plain text (not already hashed)
                    if (!user.PasswordHash.StartsWith("$2"))
                    {
                        string plainTextPassword = user.PasswordHash;
                        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(plainTextPassword, 12);
                        user.UpdatedAt = DateTime.UtcNow;

                        // Verify the new hash
                        bool verification = BCrypt.Net.BCrypt.Verify(plainTextPassword, user.PasswordHash);

                        results.Add(new
                        {
                            email = user.Email,
                            plainTextPassword = plainTextPassword,
                            hashLength = user.PasswordHash.Length,
                            verificationPassed = verification,
                            hashPreview = user.PasswordHash.Substring(0, 20) + "..."
                        });

                        _logger.LogInformation($"Converted {user.Email}: {plainTextPassword} -> HASHED (verification: {verification})");
                    }
                    else
                    {
                        results.Add(new
                        {
                            email = user.Email,
                            message = "Already hashed",
                            hashLength = user.PasswordHash.Length
                        });
                    }
                }

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    message = "Passwords converted to BCrypt format",
                    results = results
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error converting passwords to BCrypt");
                return StatusCode(500, new { message = "Error converting passwords", error = ex.Message });
            }
        }

        [HttpGet("check-user/{email}")]
        public async Task<IActionResult> CheckUser(string email)
        {
            try
            {
                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower());

                if (user == null)
                {
                    return NotFound(new { message = "User not found", email = email });
                }

                // Test password verification with common passwords
                var testPasswords = new[] { "Admin1234", "Employee123!", "AdminPassword123!", "Manager123!" };
                var testResults = new List<object>();

                foreach (var testPassword in testPasswords)
                {
                    try
                    {
                        bool isValid = false;
                        string testMethod = "";

                        if (user.PasswordHash.StartsWith("$2"))
                        {
                            // BCrypt verification
                            isValid = BCrypt.Net.BCrypt.Verify(testPassword, user.PasswordHash);
                            testMethod = "BCrypt";
                        }
                        else
                        {
                            // Plain text comparison
                            isValid = user.PasswordHash == testPassword;
                            testMethod = "PlainText";
                        }

                        testResults.Add(new
                        {
                            password = testPassword,
                            isValid = isValid,
                            method = testMethod
                        });
                    }
                    catch (Exception ex)
                    {
                        testResults.Add(new
                        {
                            password = testPassword,
                            isValid = false,
                            error = ex.Message
                        });
                    }
                }

                return Ok(new
                {
                    message = "User found",
                    email = user.Email,
                    userId = user.UserId,
                    status = user.Status,
                    userType = user.UserType,
                    storedPassword = user.PasswordHash, // Show in debug mode
                    hashLength = user.PasswordHash?.Length ?? 0,
                    hashFormat = user.PasswordHash?.StartsWith("$2") == true ? "BCrypt" : "PlainText",
                    passwordTests = testResults
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error checking user: {email}");
                return StatusCode(500, new { message = "Error checking user", error = ex.Message });
            }
        }

        [HttpPost("regenerate-all-passwords")]
        public async Task<IActionResult> RegenerateAllPasswords()
        {
            try
            {
                var users = await _context.Users.ToListAsync();
                var results = new List<object>();

                foreach (var user in users)
                {
                    string newPassword = user.Email.Contains("dennis") ? "Admin1234" :
                                       user.Email.Contains("sales") ? "Employee123!" : "Manager123!";

                    var oldHash = user.PasswordHash;
                    user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword, 12);
                    user.UpdatedAt = DateTime.UtcNow;

                    // Verify the new hash
                    bool verification = BCrypt.Net.BCrypt.Verify(newPassword, user.PasswordHash);

                    results.Add(new
                    {
                        email = user.Email,
                        password = newPassword,
                        oldHashLength = oldHash?.Length ?? 0,
                        newHashLength = user.PasswordHash.Length,
                        verificationPassed = verification
                    });

                    _logger.LogInformation($"Regenerated password for {user.Email}: {verification}");
                }

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    message = "All passwords regenerated successfully",
                    results = results
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error regenerating all passwords");
                return StatusCode(500, new { message = "Error regenerating passwords", error = ex.Message });
            }
        }
    }

    public class TestLoginRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}