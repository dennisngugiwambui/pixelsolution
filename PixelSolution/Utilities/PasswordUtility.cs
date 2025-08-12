using Microsoft.EntityFrameworkCore;
using PixelSolution.Data;
using BCrypt.Net;

namespace PixelSolution.Utilities
{
    public class PasswordUtility
    {
        private readonly ApplicationDbContext _context;

        public PasswordUtility(ApplicationDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Updates a user's password using raw SQL to bypass trigger issues
        /// </summary>
        public async Task<(bool Success, string ErrorMessage)> UpdateUserPasswordWithRawSqlAsync(string email, string newPassword)
        {
            try
            {
                Console.WriteLine($"=== RAW SQL PASSWORD UPDATE DEBUG ===");
                Console.WriteLine($"Email: {email}");
                Console.WriteLine($"Password: {newPassword}");
                
                // First verify user exists
                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower());

                if (user == null)
                {
                    string error = $"User not found: {email}";
                    Console.WriteLine(error);
                    return (false, error);
                }

                Console.WriteLine($"User found: ID={user.UserId}, Email={user.Email}");
                Console.WriteLine($"Current hash: {user.PasswordHash}");

                // Generate proper bcrypt hash
                Console.WriteLine("Generating new BCrypt hash...");
                string hashedPassword = BCrypt.Net.BCrypt.HashPassword(newPassword, 11);
                
                Console.WriteLine($"New hash generated: {hashedPassword}");
                Console.WriteLine($"Hash length: {hashedPassword.Length}");

                // Use raw SQL to update (bypasses EF Core trigger issues)
                Console.WriteLine("Executing raw SQL update...");
                string sql = "UPDATE Users SET PasswordHash = {0}, UpdatedAt = {1} WHERE Email = {2}";
                int rowsAffected = await _context.Database.ExecuteSqlRawAsync(sql, hashedPassword, DateTime.UtcNow, email);
                
                Console.WriteLine($"Rows affected: {rowsAffected}");

                if (rowsAffected == 0)
                {
                    return (false, "No rows were updated in the database");
                }

                // Verify the hash works
                Console.WriteLine("Verifying new hash...");
                bool verification = BCrypt.Net.BCrypt.Verify(newPassword, hashedPassword);
                Console.WriteLine($"Hash verification: {verification}");

                if (!verification)
                {
                    return (false, "Generated hash failed verification");
                }

                Console.WriteLine("Password update completed successfully!");
                return (true, "Password updated successfully using raw SQL");
            }
            catch (Exception ex)
            {
                string error = $"Error updating password with raw SQL: {ex.Message}\nStack trace: {ex.StackTrace}";
                Console.WriteLine(error);
                return (false, error);
            }
        }

        /// <summary>
        /// Updates a user's password with proper bcrypt hashing (EF Core method - may fail with triggers)
        /// </summary>
        public async Task<(bool Success, string ErrorMessage)> UpdateUserPasswordAsync(string email, string newPassword)
        {
            try
            {
                Console.WriteLine($"=== PASSWORD UPDATE DEBUG ===");
                Console.WriteLine($"Email: {email}");
                Console.WriteLine($"Password: {newPassword}");
                
                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower());

                if (user == null)
                {
                    string error = $"User not found: {email}";
                    Console.WriteLine(error);
                    return (false, error);
                }

                Console.WriteLine($"User found: ID={user.UserId}, Email={user.Email}");
                Console.WriteLine($"Current hash: {user.PasswordHash}");

                // Generate proper bcrypt hash
                Console.WriteLine("Generating new BCrypt hash...");
                string hashedPassword = BCrypt.Net.BCrypt.HashPassword(newPassword, 11);
                
                Console.WriteLine($"New hash generated: {hashedPassword}");
                Console.WriteLine($"Hash length: {hashedPassword.Length}");

                // Update the user's password
                Console.WriteLine("Updating user object...");
                user.PasswordHash = hashedPassword;
                user.UpdatedAt = DateTime.UtcNow;

                Console.WriteLine("Saving changes to database...");
                int rowsAffected = await _context.SaveChangesAsync();
                Console.WriteLine($"Rows affected: {rowsAffected}");

                if (rowsAffected == 0)
                {
                    return (false, "No rows were updated in the database");
                }

                // Verify the hash works
                Console.WriteLine("Verifying new hash...");
                bool verification = BCrypt.Net.BCrypt.Verify(newPassword, hashedPassword);
                Console.WriteLine($"Hash verification: {verification}");

                if (!verification)
                {
                    return (false, "Generated hash failed verification");
                }

                Console.WriteLine("Password update completed successfully!");
                return (true, "Password updated successfully");
            }
            catch (Exception ex)
            {
                string error = $"Error updating password: {ex.Message}\nStack trace: {ex.StackTrace}";
                Console.WriteLine(error);
                return (false, error);
            }
        }

        /// <summary>
        /// Test password verification for debugging
        /// </summary>
        public async Task<bool> TestPasswordAsync(string email, string password)
        {
            try
            {
                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower());

                if (user == null)
                {
                    Console.WriteLine($"User not found: {email}");
                    return false;
                }

                Console.WriteLine($"Testing password for: {email}");
                Console.WriteLine($"Password: {password}");
                Console.WriteLine($"Stored hash: {user.PasswordHash}");

                bool isValid = BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);
                Console.WriteLine($"Verification result: {isValid}");

                return isValid;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error testing password: {ex.Message}");
                return false;
            }
        }
    }
}
