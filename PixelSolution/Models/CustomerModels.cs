using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PixelSolution.Models
{
    public class Customer
    {
        [Key]
        public int CustomerId { get; set; }

        [Required]
        [StringLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string LastName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [StringLength(255)]
        public string Email { get; set; } = string.Empty;

        [Required]
        [StringLength(20)]
        public string Phone { get; set; } = string.Empty;

        [StringLength(500)]
        public string Address { get; set; } = string.Empty;

        [StringLength(100)]
        public string City { get; set; } = string.Empty;

        [StringLength(20)]
        public string Status { get; set; } = "Active"; // Active, Inactive, Blocked

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation Properties
        public virtual ICollection<CustomerCart> CartItems { get; set; } = new List<CustomerCart>();
        public virtual ICollection<CustomerWishlist> WishlistItems { get; set; } = new List<CustomerWishlist>();
        public virtual ICollection<ProductRequest> ProductRequests { get; set; } = new List<ProductRequest>();

        // Computed Properties
        [NotMapped]
        public string FullName => $"{FirstName} {LastName}";

        [NotMapped]
        public bool IsActive => Status.Equals("Active", StringComparison.OrdinalIgnoreCase);
    }

    public class CustomerCart
    {
        [Key]
        public int CartId { get; set; }

        public int CustomerId { get; set; }
        public int ProductId { get; set; }

        [Required]
        public int Quantity { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal UnitPrice { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalPrice { get; set; }

        public DateTime AddedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation Properties
        [ForeignKey("CustomerId")]
        public virtual Customer Customer { get; set; } = null!;

        [ForeignKey("ProductId")]
        public virtual Product Product { get; set; } = null!;
    }

    public class ProductRequest
    {
        [Key]
        public int ProductRequestId { get; set; }

        [Required]
        [StringLength(50)]
        public string RequestNumber { get; set; } = string.Empty;

        public int CustomerId { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalAmount { get; set; }

        [StringLength(20)]
        public string Status { get; set; } = "Pending"; // Pending, Processing, Delivered, Cancelled

        [StringLength(20)]
        public string PaymentStatus { get; set; } = "Unpaid"; // Unpaid, Paid, Partial

        [StringLength(1000)]
        public string Notes { get; set; } = string.Empty;

        [StringLength(500)]
        public string DeliveryAddress { get; set; } = string.Empty;

        public DateTime RequestDate { get; set; } = DateTime.UtcNow;
        public DateTime? DeliveryDate { get; set; }
        public DateTime? CompletedDate { get; set; }

        public int? ProcessedByUserId { get; set; } // Staff member who processed

        // Navigation Properties
        [ForeignKey("CustomerId")]
        public virtual Customer Customer { get; set; } = null!;

        [ForeignKey("ProcessedByUserId")]
        public virtual User? ProcessedByUser { get; set; }

        public virtual ICollection<ProductRequestItem> ProductRequestItems { get; set; } = new List<ProductRequestItem>();
    }

    public class ProductRequestItem
    {
        [Key]
        public int ProductRequestItemId { get; set; }

        public int ProductRequestId { get; set; }
        public int ProductId { get; set; }

        [Required]
        public int Quantity { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal UnitPrice { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalPrice { get; set; }

        [StringLength(20)]
        public string Status { get; set; } = "Pending"; // Pending, Fulfilled, OutOfStock

        // Navigation Properties
        [ForeignKey("ProductRequestId")]
        public virtual ProductRequest ProductRequest { get; set; } = null!;

        [ForeignKey("ProductId")]
        public virtual Product Product { get; set; } = null!;
    }

    public class CustomerWishlist
    {
        [Key]
        public int WishlistId { get; set; }

        public int CustomerId { get; set; }
        public int ProductId { get; set; }

        public DateTime AddedAt { get; set; } = DateTime.UtcNow;

        [StringLength(500)]
        public string Notes { get; set; } = string.Empty;

        // Navigation Properties
        [ForeignKey("CustomerId")]
        public virtual Customer Customer { get; set; } = null!;

        [ForeignKey("ProductId")]
        public virtual Product Product { get; set; } = null!;
    }
}
