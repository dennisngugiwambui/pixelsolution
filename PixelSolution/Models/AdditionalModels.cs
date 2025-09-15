using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PixelSolution.Models
{
    public class Department
    {
        [Key]
        public int DepartmentId { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [StringLength(500)]
        public string Description { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation Properties
        public virtual ICollection<UserDepartment> UserDepartments { get; set; } = new List<UserDepartment>();
    }

    public class Category
    {
        [Key]
        public int CategoryId { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [StringLength(500)]
        public string Description { get; set; } = string.Empty;

        [StringLength(255)]
        public string ImageUrl { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;

        [StringLength(20)]
        public string Status { get; set; } = "Active"; // Active, Inactive

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation Properties
        public virtual ICollection<Product> Products { get; set; } = new List<Product>();
    }

    public class Supplier
    {
        [Key]
        public int SupplierId { get; set; }

        [Required]
        [StringLength(200)]
        public string CompanyName { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string ContactPerson { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [StringLength(255)]
        public string Email { get; set; } = string.Empty;

        [Required]
        [StringLength(20)]
        public string Phone { get; set; } = string.Empty;

        [StringLength(500)]
        public string Address { get; set; } = string.Empty;

        [StringLength(20)]
        public string Status { get; set; } = "Active"; // Active, Inactive

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation Properties
        public virtual ICollection<Product> Products { get; set; } = new List<Product>();
        public virtual ICollection<PurchaseRequest> PurchaseRequests { get; set; } = new List<PurchaseRequest>();
    }

    public class Product
    {
        [Key]
        public int ProductId { get; set; }

        [Required]
        [StringLength(200)]
        public string Name { get; set; } = string.Empty;

        [StringLength(500)]
        public string Description { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string SKU { get; set; } = string.Empty;

        [Required]
        public int CategoryId { get; set; }

        public int? SupplierId { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal BuyingPrice { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal SellingPrice { get; set; }

        [Required]
        public int StockQuantity { get; set; }

        public int MinStockLevel { get; set; } = 10;

        [StringLength(255)]
        public string ImageUrl { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;

        [StringLength(20)]
        public string Status { get; set; } = "Active"; // Active, Inactive, Discontinued

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation Properties
        [ForeignKey("CategoryId")]
        public virtual Category Category { get; set; } = null!;

        [ForeignKey("SupplierId")]
        public virtual Supplier? Supplier { get; set; }

        public virtual ICollection<SaleItem> SaleItems { get; set; } = new List<SaleItem>();
        public virtual ICollection<PurchaseRequestItem> PurchaseRequestItems { get; set; } = new List<PurchaseRequestItem>();

        // Computed Properties
        [NotMapped]
        public decimal ProfitMargin => SellingPrice - BuyingPrice;

        [NotMapped]
        public decimal ProfitPercentage => BuyingPrice > 0 ? ((SellingPrice - BuyingPrice) / BuyingPrice) * 100 : 0;

        [NotMapped]
        public bool IsLowStock => StockQuantity <= MinStockLevel;
    }

    public class Sale
    {
        [Key]
        public int SaleId { get; set; }

        [Required]
        [StringLength(50)]
        public string SaleNumber { get; set; } = string.Empty;

        public int UserId { get; set; } // Sales person

        [StringLength(200)]
        public string CashierName { get; set; } = string.Empty; // Store cashier name directly

        [StringLength(200)]
        public string CustomerName { get; set; } = string.Empty;

        [StringLength(20)]
        public string CustomerPhone { get; set; } = string.Empty;

        [EmailAddress]
        [StringLength(255)]
        public string CustomerEmail { get; set; } = string.Empty;

        [Required]
        [StringLength(20)]
        public string PaymentMethod { get; set; } = string.Empty; // Cash, MPesa, Card

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal AmountPaid { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal ChangeGiven { get; set; }

        [StringLength(20)]
        public string Status { get; set; } = "Completed"; // Pending, Completed, Cancelled

        public DateTime SaleDate { get; set; } = DateTime.UtcNow;

        // Navigation Properties
        [ForeignKey("UserId")]
        public virtual User User { get; set; } = null!;

        public virtual ICollection<SaleItem> SaleItems { get; set; } = new List<SaleItem>();
    }

    public class SaleItem
    {
        [Key]
        public int SaleItemId { get; set; }

        public int SaleId { get; set; }
        public int ProductId { get; set; }

        [Required]
        public int Quantity { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal UnitPrice { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalPrice { get; set; }

        // Navigation Properties
        [ForeignKey("SaleId")]
        public virtual Sale Sale { get; set; } = null!;

        [ForeignKey("ProductId")]
        public virtual Product Product { get; set; } = null!;
    }

    public class PurchaseRequest
    {
        [Key]
        public int PurchaseRequestId { get; set; }

        [StringLength(50)]
        public string RequestNumber { get; set; } = string.Empty;

        public int UserId { get; set; }
        public int SupplierId { get; set; }
        public decimal TotalAmount { get; set; }

        [StringLength(20)]
        public string Status { get; set; } = "Pending"; // Pending, Approved, Completed, Cancelled

        [StringLength(1000)]
        public string Notes { get; set; } = string.Empty;

        public DateTime RequestDate { get; set; } = DateTime.UtcNow;
        public DateTime? ApprovedDate { get; set; }
        public DateTime? DeliveryDate { get; set; }
        public DateTime? CompletedDate { get; set; }
        public int? ProcessedByUserId { get; set; }

        [StringLength(20)]
        public string PaymentStatus { get; set; } = "Pending"; // Pending, Processing, Paid

        [StringLength(500)]
        public string DeliveryAddress { get; set; } = string.Empty;

        // Navigation Properties
        [ForeignKey("UserId")]
        public virtual User User { get; set; } = null!;

        [ForeignKey("SupplierId")]
        public virtual Supplier Supplier { get; set; } = null!;

        public virtual ICollection<PurchaseRequestItem> PurchaseRequestItems { get; set; } = new List<PurchaseRequestItem>();
    }

    public class PurchaseRequestItem
    {
        [Key]
        public int PurchaseRequestItemId { get; set; }

        public int PurchaseRequestId { get; set; }
        public int ProductId { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalPrice { get; set; }

        // Navigation Properties
        [ForeignKey("PurchaseRequestId")]
        public virtual PurchaseRequest PurchaseRequest { get; set; } = null!;

        [ForeignKey("ProductId")]
        public virtual Product Product { get; set; } = null!;
    }

    public class Message
    {
        [Key]
        public int MessageId { get; set; }

        public int FromUserId { get; set; }
        public int ToUserId { get; set; }

        [Required]
        [StringLength(200)]
        public string Subject { get; set; } = string.Empty;

        [Required]
        [StringLength(2000)]
        public string Content { get; set; } = string.Empty;

        [StringLength(50)]
        public string MessageType { get; set; } = "General"; // General, Reminder, Promotion

        public bool IsRead { get; set; } = false;

        public DateTime SentDate { get; set; } = DateTime.UtcNow;
        public DateTime? ReadDate { get; set; }

        // Navigation Properties
        [ForeignKey("FromUserId")]
        public virtual User FromUser { get; set; } = null!;

        [ForeignKey("ToUserId")]
        public virtual User ToUser { get; set; } = null!;
    }

    public class MpesaTransaction
    {
        [Key]
        public int MpesaTransactionId { get; set; }

        [Required]
        public int SaleId { get; set; }

        [Required]
        [StringLength(50)]
        public string CheckoutRequestId { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string MerchantRequestId { get; set; } = string.Empty;

        [Required]
        [StringLength(15)]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        [StringLength(20)]
        public string Status { get; set; } = "Pending"; // Pending, Success, Failed, Cancelled

        [StringLength(100)]
        public string MpesaReceiptNumber { get; set; } = string.Empty;

        [StringLength(100)]
        public string TransactionId { get; set; } = string.Empty;

        [StringLength(500)]
        public string CallbackResponse { get; set; } = string.Empty;

        [StringLength(200)]
        public string ErrorMessage { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAt { get; set; }

        // Navigation Properties
        [ForeignKey("SaleId")]
        public virtual Sale Sale { get; set; } = null!;
    }

    // Request/Response Models for API
    public class ProcessSaleRequest
    {
        public List<SaleItemRequest> Items { get; set; } = new List<SaleItemRequest>();
        public decimal TotalAmount { get; set; }
        public string PaymentMethod { get; set; } = string.Empty;
        public decimal AmountPaid { get; set; }
        public decimal ChangeGiven { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerPhone { get; set; } = string.Empty;
        public string CustomerEmail { get; set; } = string.Empty;
    }

    public class SaleItemRequest
    {
        public int ProductId { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalPrice { get; set; }
    }

    public class ProcessSaleResult
    {
        public bool Success { get; set; }
        public int SaleId { get; set; }
        public string SaleNumber { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
        public object? ReceiptData { get; set; }
    }

    public class Wishlist
    {
        [Key]
        public int WishlistId { get; set; }

        [Required]
        public int CustomerId { get; set; }

        [Required]
        public int ProductId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [StringLength(500)]
        public string Notes { get; set; } = string.Empty;

        // Navigation Properties
        [ForeignKey("CustomerId")]
        public virtual Customer Customer { get; set; } = null!;

        [ForeignKey("ProductId")]
        public virtual Product Product { get; set; } = null!;
    }

    public class MpesaToken
    {
        [Key]
        public int MpesaTokenId { get; set; }

        [Required]
        [StringLength(500)]
        public string AccessToken { get; set; } = string.Empty;

        [Required]
        public DateTime ExpiresAt { get; set; }

        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [StringLength(50)]
        public string TokenType { get; set; } = "Bearer";

        public bool IsActive { get; set; } = true;

        // Helper method to check if token is still valid
        [NotMapped]
        public bool IsValid => IsActive && ExpiresAt > DateTime.UtcNow;
    }

    public class PaymentRequest
    {
        [Required]
        [StringLength(15)]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required]
        [Range(1, 70000)]
        public decimal Amount { get; set; }

        [StringLength(100)]
        public string? AccountReference { get; set; }

        [StringLength(200)]
        public string? TransactionDesc { get; set; }

        [StringLength(100)]
        public string? CustomerName { get; set; }

        [StringLength(100)]
        public string? CustomerEmail { get; set; }

        public List<PaymentSaleItem>? SaleItems { get; set; }
    }

    public class PaymentSaleItem
    {
        public int ProductId { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalPrice { get; set; }
    }

    // Supplier Product Supply Tracking - tracks supply batches for existing products
    public class SupplierProductSupply
    {
        [Key]
        public int SupplierProductSupplyId { get; set; }

        [Required]
        public int SupplierId { get; set; }

        [Required]
        public int ProductId { get; set; }

        [Required]
        public int QuantitySupplied { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal UnitCost { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalCost { get; set; }

        [StringLength(50)]
        public string BatchNumber { get; set; } = string.Empty;

        public DateTime SupplyDate { get; set; } = DateTime.UtcNow;

        public DateTime? ExpiryDate { get; set; }

        [StringLength(20)]
        public string PaymentStatus { get; set; } = "Pending"; // Pending, Invoiced, Paid, Settled

        [StringLength(500)]
        public string Notes { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation Properties
        [ForeignKey("SupplierId")]
        public virtual Supplier Supplier { get; set; } = null!;

        [ForeignKey("ProductId")]
        public virtual Product Product { get; set; } = null!;

        public virtual ICollection<SupplierInvoiceItem> SupplierInvoiceItems { get; set; } = new List<SupplierInvoiceItem>();
    }

    public class SupplierInvoice
    {
        [Key]
        public int SupplierInvoiceId { get; set; }

        [Required]
        [StringLength(50)]
        public string InvoiceNumber { get; set; } = string.Empty;

        [Required]
        public int SupplierId { get; set; }

        [Required]
        public int CreatedByUserId { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal SubTotal { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TaxAmount { get; set; } = 0;

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalAmount { get; set; }

        [StringLength(20)]
        public string Status { get; set; } = "Pending"; // Pending, Sent, Paid, Overdue, Cancelled

        [StringLength(20)]
        public string PaymentStatus { get; set; } = "Unpaid"; // Unpaid, Partially_Paid, Paid

        [Column(TypeName = "decimal(18,2)")]
        public decimal AmountPaid { get; set; } = 0;

        [Column(TypeName = "decimal(18,2)")]
        public decimal AmountDue { get; set; }

        public DateTime InvoiceDate { get; set; } = DateTime.UtcNow;
        public DateTime DueDate { get; set; }

        [StringLength(1000)]
        public string Notes { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation Properties
        [ForeignKey("SupplierId")]
        public virtual Supplier Supplier { get; set; } = null!;

        [ForeignKey("CreatedByUserId")]
        public virtual User CreatedByUser { get; set; } = null!;

        public virtual ICollection<SupplierInvoiceItem> SupplierInvoiceItems { get; set; } = new List<SupplierInvoiceItem>();
        public virtual ICollection<SupplierPayment> SupplierPayments { get; set; } = new List<SupplierPayment>();

        // Computed Properties
        [NotMapped]
        public bool IsOverdue => DueDate < DateTime.UtcNow && PaymentStatus != "Paid";

        [NotMapped]
        public decimal BalanceAmount => TotalAmount - AmountPaid;
    }

    public class SupplierInvoiceItem
    {
        [Key]
        public int SupplierInvoiceItemId { get; set; }

        [Required]
        public int SupplierInvoiceId { get; set; }

        [Required]
        public int SupplierProductSupplyId { get; set; }

        [Required]
        public int Quantity { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal UnitCost { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalCost { get; set; }

        [StringLength(500)]
        public string Description { get; set; } = string.Empty;

        // Navigation Properties
        [ForeignKey("SupplierInvoiceId")]
        public virtual SupplierInvoice SupplierInvoice { get; set; } = null!;

        [ForeignKey("SupplierProductSupplyId")]
        public virtual SupplierProductSupply SupplierProductSupply { get; set; } = null!;
    }

    public class SupplierPayment
    {
        [Key]
        public int SupplierPaymentId { get; set; }

        [Required]
        public int SupplierInvoiceId { get; set; }

        [Required]
        public int ProcessedByUserId { get; set; }

        [Required]
        [StringLength(50)]
        public string PaymentReference { get; set; } = string.Empty;

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        [Required]
        [StringLength(20)]
        public string PaymentMethod { get; set; } = string.Empty; // Cash, Bank_Transfer, Cheque, M-Pesa

        [StringLength(20)]
        public string Status { get; set; } = "Completed"; // Pending, Completed, Failed, Cancelled

        public DateTime PaymentDate { get; set; } = DateTime.UtcNow;

        [StringLength(1000)]
        public string Notes { get; set; } = string.Empty;

        [StringLength(100)]
        public string TransactionId { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation Properties
        [ForeignKey("SupplierInvoiceId")]
        public virtual SupplierInvoice SupplierInvoice { get; set; } = null!;

        [ForeignKey("ProcessedByUserId")]
        public virtual User ProcessedByUser { get; set; } = null!;
    }
}