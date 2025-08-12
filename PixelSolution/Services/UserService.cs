using Microsoft.EntityFrameworkCore;
using PixelSolution.Data;
using PixelSolution.Models;
using PixelSolution.Services.Interfaces;
using PixelSolution.ViewModels;
using BCrypt.Net;

namespace PixelSolution.Services
{
    public class UserService : IUserService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<UserService> _logger;

        public UserService(ApplicationDbContext context, ILogger<UserService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<IEnumerable<User>> GetAllUsersAsync()
        {
            try
            {
                return await _context.Users
                    .Include(u => u.UserDepartments)
                        .ThenInclude(ud => ud.Department)
                    .OrderBy(u => u.FirstName)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all users from database");
                return new List<User>();
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

        public async Task<User?> GetUserByEmailAsync(string email)
        {
            try
            {
                return await _context.Users
                    .Include(u => u.UserDepartments)
                        .ThenInclude(ud => ud.Department)
                    .FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting user by email: {email}");
                return null;
            }
        }

        public async Task<User> CreateUserWithDepartmentsAsync(EnhancedCreateUserViewModel model)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                if (model.DepartmentIds == null || !model.DepartmentIds.Any())
                {
                    throw new ArgumentException("User must be assigned to at least one department.");
                }

                var user = new User
                {
                    FirstName = model.FirstName,
                    LastName = model.LastName,
                    Email = model.Email,
                    Phone = model.Phone ?? string.Empty,
                    UserType = model.UserType,
                    Status = model.Status,
                    Privileges = model.Privileges ?? "Limited",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.Password, 12),
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                // Add department assignments
                foreach (var deptId in model.DepartmentIds)
                {
                    var userDepartment = new UserDepartment
                    {
                        UserId = user.UserId,
                        DepartmentId = deptId,
                        AssignedAt = DateTime.UtcNow
                    };
                    _context.UserDepartments.Add(userDepartment);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation($"Created user {user.Email} with {model.DepartmentIds.Count} department assignments");
                return user;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Error creating user: {model.Email}");
                throw;
            }
        }

        public async Task<User> UpdateUserWithDepartmentsAsync(EnhancedEditUserViewModel model)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var user = await _context.Users
                    .Include(u => u.UserDepartments)
                    .FirstOrDefaultAsync(u => u.UserId == model.UserId);

                if (user == null)
                {
                    throw new ArgumentException("User not found.");
                }

                user.FirstName = model.FirstName;
                user.LastName = model.LastName;
                user.Email = model.Email;
                user.Phone = model.Phone ?? string.Empty;
                user.UserType = model.UserType;
                user.Status = model.Status;
                user.Privileges = model.Privileges ?? "Limited";
                user.UpdatedAt = DateTime.UtcNow;

                // Update password if provided
                if (!string.IsNullOrWhiteSpace(model.NewPassword))
                {
                    user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.NewPassword, 12);
                }

                // Remove existing department assignments
                var existingAssignments = await _context.UserDepartments
                    .Where(ud => ud.UserId == user.UserId)
                    .ToListAsync();

                _context.UserDepartments.RemoveRange(existingAssignments);

                // Add new department assignments
                if (model.DepartmentIds != null && model.DepartmentIds.Any())
                {
                    foreach (var deptId in model.DepartmentIds)
                    {
                        var userDepartment = new UserDepartment
                        {
                            UserId = user.UserId,
                            DepartmentId = deptId,
                            AssignedAt = DateTime.UtcNow
                        };
                        _context.UserDepartments.Add(userDepartment);
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation($"Updated user {user.Email}");
                return user;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Error updating user: {model.UserId}");
                throw;
            }
        }

        public async Task<bool> DeleteUserAsync(int userId)
        {
            try
            {
                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                {
                    return false;
                }

                // Instead of deleting, set status to "Deleted" or "Inactive"
                user.Status = "Deleted";
                user.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Soft deleted user: {userId}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting user: {userId}");
                return false;
            }
        }

        public async Task<bool> ChangeUserStatusAsync(int userId, string status)
        {
            try
            {
                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                {
                    return false;
                }

                user.Status = status;
                user.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Changed user {userId} status to {status}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error changing user status: {userId}");
                return false;
            }
        }

        public async Task<IEnumerable<User>> GetUsersByDepartmentAsync(int departmentId)
        {
            try
            {
                return await _context.Users
                    .Include(u => u.UserDepartments)
                        .ThenInclude(ud => ud.Department)
                    .Where(u => u.UserDepartments.Any(ud => ud.DepartmentId == departmentId))
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting users by department: {departmentId}");
                return new List<User>();
            }
        }

        public async Task<IEnumerable<User>> GetUsersByTypeAsync(string userType)
        {
            try
            {
                return await _context.Users
                    .Include(u => u.UserDepartments)
                        .ThenInclude(ud => ud.Department)
                    .Where(u => u.UserType == userType)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting users by type: {userType}");
                return new List<User>();
            }
        }

        public async Task UpdateUserDepartmentsAsync(int userId, List<int> departmentIds)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var user = await _context.Users
                    .Include(u => u.UserDepartments)
                    .FirstOrDefaultAsync(u => u.UserId == userId);

                if (user == null)
                {
                    throw new ArgumentException("User not found.");
                }

                // Remove existing assignments
                var existingAssignments = await _context.UserDepartments
                    .Where(ud => ud.UserId == userId)
                    .ToListAsync();

                _context.UserDepartments.RemoveRange(existingAssignments);

                // Add new assignments
                if (departmentIds != null && departmentIds.Any())
                {
                    foreach (var deptId in departmentIds)
                    {
                        var userDepartment = new UserDepartment
                        {
                            UserId = userId,
                            DepartmentId = deptId,
                            AssignedAt = DateTime.UtcNow
                        };
                        _context.UserDepartments.Add(userDepartment);
                    }
                }

                user.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation($"Updated departments for user: {userId}");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Error updating user departments: {userId}");
                throw;
            }
        }

        public async Task<bool> ValidatePasswordStrengthAsync(string password)
        {
            try
            {
                // Enhanced password validation
                if (string.IsNullOrEmpty(password) || password.Length < 8)
                    return false;

                var hasUpper = password.Any(char.IsUpper);
                var hasLower = password.Any(char.IsLower);
                var hasNumber = password.Any(char.IsDigit);
                var hasSpecial = password.Any(c => "@$!%*?&".Contains(c));

                return hasUpper && hasLower && hasNumber && hasSpecial;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating password strength");
                return false;
            }
        }

        public async Task<bool> IsEmailUniqueAsync(string email, int? excludeUserId = null)
        {
            try
            {
                var query = _context.Users.Where(u => u.Email.ToLower() == email.ToLower());

                if (excludeUserId.HasValue)
                {
                    query = query.Where(u => u.UserId != excludeUserId.Value);
                }

                return !await query.AnyAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error checking email uniqueness: {email}");
                return false;
            }
        }

        public async Task<Dictionary<string, int>> GetUserStatisticsAsync()
        {
            try
            {
                var stats = new Dictionary<string, int>
                {
                    ["TotalUsers"] = await _context.Users.CountAsync(),
                    ["ActiveUsers"] = await _context.Users.CountAsync(u => u.Status == "Active"),
                    ["InactiveUsers"] = await _context.Users.CountAsync(u => u.Status == "Inactive"),
                    ["AdminUsers"] = await _context.Users.CountAsync(u => u.UserType == "Admin"),
                    ["ManagerUsers"] = await _context.Users.CountAsync(u => u.UserType == "Manager"),
                    ["EmployeeUsers"] = await _context.Users.CountAsync(u => u.UserType == "Employee"),
                    ["UsersWithSales"] = await _context.Users.CountAsync(u => u.Sales.Any()),
                    ["NewUsersThisMonth"] = await _context.Users.CountAsync(u => u.CreatedAt >= DateTime.UtcNow.AddDays(-30))
                };

                return stats;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user statistics");
                return new Dictionary<string, int>();
            }
        }

        public async Task<bool> BulkUpdateUserStatusAsync(List<int> userIds, string status)
        {
            try
            {
                var users = await _context.Users
                    .Where(u => userIds.Contains(u.UserId))
                    .ToListAsync();

                foreach (var user in users)
                {
                    user.Status = status;
                    user.UpdatedAt = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();
                _logger.LogInformation($"Bulk updated {users.Count} users to status: {status}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in bulk updating user status");
                return false;
            }
        }

        public async Task<List<User>> SearchUsersAsync(string searchTerm, string? userType = null, string? status = null, int? departmentId = null)
        {
            try
            {
                var query = _context.Users
                    .Include(u => u.UserDepartments)
                        .ThenInclude(ud => ud.Department)
                    .AsQueryable();

                if (!string.IsNullOrEmpty(searchTerm))
                {
                    query = query.Where(u =>
                        u.FirstName.Contains(searchTerm) ||
                        u.LastName.Contains(searchTerm) ||
                        u.Email.Contains(searchTerm) ||
                        u.Phone.Contains(searchTerm));
                }

                if (!string.IsNullOrEmpty(userType))
                {
                    query = query.Where(u => u.UserType == userType);
                }

                if (!string.IsNullOrEmpty(status))
                {
                    query = query.Where(u => u.Status == status);
                }

                if (departmentId.HasValue)
                {
                    query = query.Where(u => u.UserDepartments.Any(ud => ud.DepartmentId == departmentId.Value));
                }

                return await query.OrderBy(u => u.FirstName).ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching users");
                return new List<User>();
            }
        }

        public async Task<bool> ResetUserPasswordAsync(int userId, string newPassword)
        {
            try
            {
                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                    return false;

                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword, 12);
                user.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                _logger.LogInformation($"Password reset for user: {userId}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error resetting password for user: {userId}");
                return false;
            }
        }

        public async Task<List<User>> GetTopPerformingUsersAsync(int limit = 10, DateTime? startDate = null, DateTime? endDate = null)
        {
            try
            {
                var start = startDate ?? DateTime.UtcNow.AddMonths(-1);
                var end = endDate ?? DateTime.UtcNow;

                return await _context.Users
                    .Include(u => u.UserDepartments)
                        .ThenInclude(ud => ud.Department)
                    .Include(u => u.Sales)
                    .Where(u => u.UserType != "Admin" && u.Status == "Active")
                    .OrderByDescending(u => u.Sales
                        .Where(s => s.SaleDate >= start && s.SaleDate <= end && s.Status == "Completed")
                        .Sum(s => s.TotalAmount))
                    .Take(limit)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting top performing users");
                return new List<User>();
            }
        }
    }
}