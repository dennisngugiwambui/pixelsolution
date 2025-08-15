using Microsoft.EntityFrameworkCore;
using PixelSolution.Data;
using PixelSolution.Models;
using System.Text.Json;

namespace PixelSolution.Services
{
    public interface IActivityLogService
    {
        Task LogActivityAsync(int userId, string activityType, string description, string? entityType = null, int? entityId = null, object? details = null, string? ipAddress = null, string? userAgent = null);
        Task<List<UserActivityLog>> GetUserActivitiesAsync(int userId, int pageSize = 50, int page = 1);
        Task<List<UserActivityLog>> GetRecentActivitiesAsync(int count = 100);
        Task CleanupOldActivitiesAsync(int daysToKeep = 90);
    }

    public class ActivityLogService : IActivityLogService
    {
        private readonly ApplicationDbContext _context;

        public ActivityLogService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task LogActivityAsync(int userId, string activityType, string description, string? entityType = null, int? entityId = null, object? details = null, string? ipAddress = null, string? userAgent = null)
        {
            try
            {
                var activity = new UserActivityLog
                {
                    UserId = userId,
                    ActivityType = activityType,
                    Description = description,
                    EntityType = entityType,
                    EntityId = entityId,
                    Details = details != null ? JsonSerializer.Serialize(details) : null,
                    IpAddress = ipAddress ?? "Unknown",
                    UserAgent = userAgent,
                    CreatedAt = DateTime.UtcNow
                };

                _context.UserActivityLogs.Add(activity);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                // Log error but don't throw to avoid breaking main functionality
                Console.WriteLine($"Error logging activity: {ex.Message}");
            }
        }

        public async Task<List<UserActivityLog>> GetUserActivitiesAsync(int userId, int pageSize = 50, int page = 1)
        {
            return await _context.UserActivityLogs
                .Include(ual => ual.User)
                .Where(ual => ual.UserId == userId)
                .OrderByDescending(ual => ual.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<List<UserActivityLog>> GetRecentActivitiesAsync(int count = 100)
        {
            return await _context.UserActivityLogs
                .Include(ual => ual.User)
                .OrderByDescending(ual => ual.CreatedAt)
                .Take(count)
                .ToListAsync();
        }

        public async Task CleanupOldActivitiesAsync(int daysToKeep = 90)
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-daysToKeep);
            var oldActivities = await _context.UserActivityLogs
                .Where(ual => ual.CreatedAt < cutoffDate)
                .ToListAsync();

            if (oldActivities.Any())
            {
                _context.UserActivityLogs.RemoveRange(oldActivities);
                await _context.SaveChangesAsync();
            }
        }
    }
}
