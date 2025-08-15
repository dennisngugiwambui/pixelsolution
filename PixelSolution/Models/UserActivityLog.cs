using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PixelSolution.Models
{
    public class UserActivityLog
    {
        [Key]
        public int ActivityId { get; set; }

        [Required]
        public int UserId { get; set; }

        [Required]
        [StringLength(100)]
        public string ActivityType { get; set; } = string.Empty; // Login, Sale, Purchase, Export, etc.

        [Required]
        [StringLength(500)]
        public string Description { get; set; } = string.Empty;

        [StringLength(50)]
        public string? EntityType { get; set; } // Sale, Product, User, etc.

        public int? EntityId { get; set; } // ID of the related entity

        [StringLength(1000)]
        public string? Details { get; set; } // JSON or additional details

        [Required]
        [StringLength(45)]
        public string IpAddress { get; set; } = string.Empty;

        [StringLength(500)]
        public string? UserAgent { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation Properties
        [ForeignKey("UserId")]
        public virtual User User { get; set; } = null!;

        // Index properties for performance
        [NotMapped]
        public string UserFullName => User?.FirstName + " " + User?.LastName ?? "Unknown";
    }

    // Activity types enum for consistency
    public static class ActivityTypes
    {
        public const string Login = "Login";
        public const string Logout = "Logout";
        public const string Sale = "Sale";
        public const string PurchaseRequest = "PurchaseRequest";
        public const string ProductCreate = "ProductCreate";
        public const string ProductUpdate = "ProductUpdate";
        public const string ProductDelete = "ProductDelete";
        public const string UserCreate = "UserCreate";
        public const string UserUpdate = "UserUpdate";
        public const string ReportExport = "ReportExport";
        public const string SystemAccess = "SystemAccess";
    }
}
