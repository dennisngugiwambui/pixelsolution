using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PixelSolution.Models
{
    public class EmployeeProfile
    {
        [Key]
        public int EmployeeProfileId { get; set; }

        [Required]
        public int UserId { get; set; } // Foreign key to User table

        [StringLength(50)]
        public string EmployeeNumber { get; set; } = string.Empty;

        [StringLength(100)]
        public string Position { get; set; } = string.Empty;

        public DateTime HireDate { get; set; } = DateTime.UtcNow;

        [Column(TypeName = "decimal(18,2)")]
        public decimal BaseSalary { get; set; } = 0;

        [StringLength(20)]
        public string PaymentFrequency { get; set; } = "Monthly"; // Weekly, Bi-weekly, Monthly

        [StringLength(50)]
        public string BankAccount { get; set; } = string.Empty;

        [StringLength(100)]
        public string BankName { get; set; } = string.Empty;

        [StringLength(500)]
        public string EmergencyContact { get; set; } = string.Empty;

        [StringLength(20)]
        public string EmploymentStatus { get; set; } = "Active"; // Active, Suspended, Terminated

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation Properties
        [ForeignKey("UserId")]
        public virtual User User { get; set; } = null!;

        public virtual ICollection<EmployeeSalary> EmployeeSalaries { get; set; } = new List<EmployeeSalary>();
        public virtual ICollection<EmployeeFine> EmployeeFines { get; set; } = new List<EmployeeFine>();
        public virtual ICollection<EmployeePayment> EmployeePayments { get; set; } = new List<EmployeePayment>();
    }

    public class EmployeeSalary
    {
        [Key]
        public int SalaryId { get; set; }

        public int EmployeeProfileId { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        [StringLength(20)]
        public string SalaryType { get; set; } = "Base"; // Base, Overtime, Bonus, Commission

        public DateTime EffectiveDate { get; set; } = DateTime.UtcNow;
        public DateTime? EndDate { get; set; }

        [StringLength(500)]
        public string Notes { get; set; } = string.Empty;

        [StringLength(20)]
        public string Status { get; set; } = "Active"; // Active, Inactive, Suspended

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation Properties
        [ForeignKey("EmployeeProfileId")]
        public virtual EmployeeProfile EmployeeProfile { get; set; } = null!;
    }


    //this model
    public class EmployeeFine
    {
        [Key]
        public int FineId { get; set; }

        public int EmployeeProfileId { get; set; }

        [Required]
        [StringLength(200)]
        public string Reason { get; set; } = string.Empty;

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        [StringLength(20)]
        public string Status { get; set; } = "Pending"; // Pending, Paid, Waived, Disputed

        [StringLength(1000)]
        public string Description { get; set; } = string.Empty;

        public DateTime IssuedDate { get; set; } = DateTime.UtcNow;
        public DateTime? DueDate { get; set; }
        public DateTime? PaidDate { get; set; }
        public bool IsPaid => Status == "Paid";

        public int IssuedByUserId { get; set; } // Admin/Manager who issued the fine

        [StringLength(500)]
        public string PaymentMethod { get; set; } = string.Empty;

        // Navigation Properties
        [ForeignKey("EmployeeProfileId")]
        public virtual EmployeeProfile EmployeeProfile { get; set; } = null!;

        [ForeignKey("IssuedByUserId")]
        public virtual User IssuedByUser { get; set; } = null!;
    }

    public class EmployeePayment
    {
        [Key]
        public int PaymentId { get; set; }

        public int EmployeeProfileId { get; set; }

        [Required]
        [StringLength(50)]
        public string PaymentNumber { get; set; } = string.Empty;

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal GrossPay { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Deductions { get; set; } = 0; // Fines, taxes, etc.

        [Column(TypeName = "decimal(18,2)")]
        public decimal NetPay { get; set; }

        [StringLength(20)]
        public string PaymentPeriod { get; set; } = string.Empty; // "January 2024", "Week 1 Jan 2024"

        public DateTime PaymentDate { get; set; } = DateTime.UtcNow;

        [StringLength(20)]
        public string Status { get; set; } = "Pending"; // Pending, Paid, Failed

        [StringLength(50)]
        public string PaymentMethod { get; set; } = "Bank Transfer";

        [StringLength(1000)]
        public string Notes { get; set; } = string.Empty;

        public int ProcessedByUserId { get; set; } // Admin who processed payment

        // Navigation Properties
        [ForeignKey("EmployeeProfileId")]
        public virtual EmployeeProfile EmployeeProfile { get; set; } = null!;

        [ForeignKey("ProcessedByUserId")]
        public virtual User ProcessedByUser { get; set; } = null!;
    }

    // Request/Response Models for Employee Management
    public class CreateEmployeeProfileRequest
    {
        public int UserId { get; set; }
        public string EmployeeNumber { get; set; } = string.Empty;
        public string Position { get; set; } = string.Empty;
        public DateTime HireDate { get; set; }
        public decimal BaseSalary { get; set; }
        public string PaymentFrequency { get; set; } = "Monthly";
        public string BankAccount { get; set; } = string.Empty;
        public string BankName { get; set; } = string.Empty;
        public string EmergencyContact { get; set; } = string.Empty;
    }

    public class IssueFineRequest
    {
        public int EmployeeProfileId { get; set; }
        public string Reason { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Description { get; set; } = string.Empty;
    }

    public class ProcessPaymentRequest
    {
        public int EmployeeProfileId { get; set; }
        public string PaymentPeriod { get; set; } = string.Empty;
        public decimal GrossPay { get; set; }
        public decimal Deductions { get; set; }
        public string PaymentMethod { get; set; } = "Bank Transfer";
        public string Notes { get; set; } = string.Empty;
    }
}
