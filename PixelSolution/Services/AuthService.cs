using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using PixelSolution.Data;
using PixelSolution.Models;
using PixelSolution.Services.Interfaces;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BCrypt.Net;

namespace PixelSolution.Services
{
    public class AuthService : IAuthService
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AuthService> _logger;

        public AuthService(ApplicationDbContext context, IConfiguration configuration, ILogger<AuthService> logger)
        {
            _context = context;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<User?> AuthenticateAsync(string email, string password)
        {
            try
            {
                _logger.LogInformation($"🔍 LOGIN ATTEMPT: {email}");

                if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
                {
                    _logger.LogWarning("❌ Email or password is empty");
                    return null;
                }

                // Get user from database
                var user = await _context.Users
                    .Include(u => u.UserDepartments)
                        .ThenInclude(ud => ud.Department)
                    .FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower());

                if (user == null)
                {
                    _logger.LogWarning($"❌ User not found: {email}");
                    return null;
                }

                if (user.Status != "Active")
                {
                    _logger.LogWarning($"❌ User not active: {email}");
                    return null;
                }

                _logger.LogInformation($"🔍 Found user: {user.Email}, Hash: {user.PasswordHash?.Substring(0, 20)}...");

                // Use standard BCrypt verification for all users
                bool isValid = ValidatePassword(password, user.PasswordHash);
                
                if (isValid)
                {
                    _logger.LogInformation($"✅ User {email} authenticated successfully");
                    return user;
                }
                else
                {
                    _logger.LogWarning($"❌ Password validation failed for: {email}");
                    _logger.LogInformation($"🔍 Attempted password length: {password?.Length}, Hash starts with: {user.PasswordHash?.Substring(0, 10)}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"💥 Authentication error for {email}");
                return null;
            }
        }

        public bool ValidatePassword(string password, string hash)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(hash))
                    return false;

                // BCrypt validation
                if (hash.StartsWith("$2"))
                {
                    return BCrypt.Net.BCrypt.Verify(password.Trim(), hash.Trim());
                }

                // Plain text fallback
                return password.Trim() == hash.Trim();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating password");
                return false;
            }
        }

        public string HashPassword(string password)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(password))
                    throw new ArgumentException("Password cannot be null or empty");

                return BCrypt.Net.BCrypt.HashPassword(password.Trim(), 12);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error hashing password");
                throw;
            }
        }

        public async Task<bool> ChangePasswordAsync(int userId, string currentPassword, string newPassword)
        {
            try
            {
                var user = await _context.Users.FindAsync(userId);
                if (user == null) return false;

                if (!ValidatePassword(currentPassword, user.PasswordHash))
                    return false;

                user.PasswordHash = HashPassword(newPassword);
                user.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error changing password for user: {userId}");
                return false;
            }
        }

        public async Task<string> GenerateJwtTokenAsync(User user)
        {
            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var key = Encoding.ASCII.GetBytes(_configuration["Jwt:Key"] ?? "your-secret-key");

                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                    new Claim(ClaimTypes.Name, user.FullName),
                    new Claim(ClaimTypes.Email, user.Email),
                    new Claim(ClaimTypes.Role, user.UserType),
                    new Claim("Privileges", user.Privileges ?? "Limited"),
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

                var tokenDescriptor = new SecurityTokenDescriptor
                {
                    Subject = new ClaimsIdentity(claims),
                    Expires = DateTime.UtcNow.AddHours(8),
                    SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
                };

                var token = tokenHandler.CreateToken(tokenDescriptor);
                return tokenHandler.WriteToken(token);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating JWT token");
                throw;
            }
        }

        public async Task<User?> GetUserByIdAsync(int userId)
        {
            try
            {
                return await _context.Users
                    .Include(u => u.UserDepartments)
                        .ThenInclude(ud => ud.Department)
                    .FirstOrDefaultAsync(u => u.UserId == userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting user by ID: {userId}");
                return null;
            }
        }

        public async Task<bool> EnsureUserPasswordIsHashedAsync(string email, string plainPassword)
        {
            try
            {
                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower());

                if (user == null) return false;

                if (!user.PasswordHash.StartsWith("$2"))
                {
                    user.PasswordHash = HashPassword(plainPassword);
                    user.UpdatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error ensuring password is hashed for user: {email}");
                return false;
            }
        }

        public async Task<bool> ConvertToHashedPasswordsAsync()
        {
            try
            {
                var users = await _context.Users
                    .Where(u => !u.PasswordHash.StartsWith("$2"))
                    .ToListAsync();

                foreach (var user in users)
                {
                    var originalPassword = user.PasswordHash;
                    user.PasswordHash = HashPassword(originalPassword);
                    user.UpdatedAt = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error converting passwords to hashed format");
                return false;
            }
        }

        // FINAL EMERGENCY FIX
        public async Task<bool> ForceFixAdminPasswordAsync()
        {
            try
            {
                _logger.LogWarning("🚨 EMERGENCY ADMIN PASSWORD FIX");

                var adminUser = await _context.Users
                    .FirstOrDefaultAsync(u => u.Email.ToLower() == "dennisngugi219@gmail.com");

                if (adminUser == null)
                {
                    _logger.LogError("❌ Admin user not found");
                    return false;
                }

                // Force create correct hash for Admin1234
                string correctHash = BCrypt.Net.BCrypt.HashPassword("Admin1234", 12);
                adminUser.PasswordHash = correctHash;
                adminUser.Status = "Active";
                adminUser.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                // Test immediately
                bool testResult = BCrypt.Net.BCrypt.Verify("Admin1234", correctHash);

                _logger.LogInformation($"✅ Admin password FORCE FIXED. Test: {testResult}");
                return testResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 Error force fixing admin password");
                return false;
            }
        }
    }
}