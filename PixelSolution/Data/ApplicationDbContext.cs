using Microsoft.EntityFrameworkCore;
using PixelSolution.Models;

namespace PixelSolution.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        // DbSets
        public DbSet<User> Users { get; set; }
        public DbSet<Department> Departments { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<Supplier> Suppliers { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<Sale> Sales { get; set; }
        public DbSet<SaleItem> SaleItems { get; set; }
        public DbSet<PurchaseRequest> PurchaseRequests { get; set; }
        public DbSet<PurchaseRequestItem> PurchaseRequestItems { get; set; }
        public DbSet<Message> Messages { get; set; }
        public DbSet<MpesaTransaction> MpesaTransactions { get; set; }
        public DbSet<UserDepartment> UserDepartments { get; set; }
        public DbSet<UserActivityLog> UserActivityLogs { get; set; }
        
        // Customer Management
        public DbSet<Customer> Customers { get; set; }
        public DbSet<CustomerCart> CustomerCarts { get; set; }
        public DbSet<ProductRequest> ProductRequests { get; set; }
        public DbSet<ProductRequestItem> ProductRequestItems { get; set; }
        
        // Employee Management
        public DbSet<EmployeeProfile> EmployeeProfiles { get; set; }
        public DbSet<EmployeeSalary> EmployeeSalaries { get; set; }
        public DbSet<EmployeeFine> EmployeeFines { get; set; }
        public DbSet<EmployeePayment> EmployeePayments { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure UserDepartment many-to-many relationship
            modelBuilder.Entity<UserDepartment>()
                .HasKey(ud => new { ud.UserId, ud.DepartmentId });

            modelBuilder.Entity<UserDepartment>()
                .HasOne(ud => ud.User)
                .WithMany(u => u.UserDepartments)
                .HasForeignKey(ud => ud.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<UserDepartment>()
                .HasOne(ud => ud.Department)
                .WithMany(d => d.UserDepartments)
                .HasForeignKey(ud => ud.DepartmentId)
                .OnDelete(DeleteBehavior.Cascade);



            // Configure Product relationships
            modelBuilder.Entity<Product>()
                .HasOne(p => p.Category)
                .WithMany(c => c.Products)
                .HasForeignKey(p => p.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Product>()
                .HasOne(p => p.Supplier)
                .WithMany(s => s.Products)
                .HasForeignKey(p => p.SupplierId)
                .OnDelete(DeleteBehavior.SetNull);

            // Configure Sale relationships
            modelBuilder.Entity<Sale>()
                .HasOne(s => s.User)
                .WithMany(u => u.Sales)
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure SaleItem relationships
            modelBuilder.Entity<SaleItem>()
                .HasOne(si => si.Sale)
                .WithMany(s => s.SaleItems)
                .HasForeignKey(si => si.SaleId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<SaleItem>()
                .HasOne(si => si.Product)
                .WithMany(p => p.SaleItems)
                .HasForeignKey(si => si.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure PurchaseRequest relationships
            modelBuilder.Entity<PurchaseRequest>()
                .HasOne(pr => pr.User)
                .WithMany(u => u.PurchaseRequests)
                .HasForeignKey(pr => pr.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<PurchaseRequest>()
                .HasOne(pr => pr.Supplier)
                .WithMany(s => s.PurchaseRequests)
                .HasForeignKey(pr => pr.SupplierId)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure PurchaseRequestItem relationships
            modelBuilder.Entity<PurchaseRequestItem>()
                .HasOne(pri => pri.PurchaseRequest)
                .WithMany(pr => pr.PurchaseRequestItems)
                .HasForeignKey(pri => pri.PurchaseRequestId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<PurchaseRequestItem>()
                .HasOne(pri => pri.Product)
                .WithMany(p => p.PurchaseRequestItems)
                .HasForeignKey(pri => pri.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure Message relationships
            modelBuilder.Entity<Message>()
                .HasOne(m => m.FromUser)
                .WithMany(u => u.SentMessages)
                .HasForeignKey(m => m.FromUserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Message>()
                .HasOne(m => m.ToUser)
                .WithMany(u => u.ReceivedMessages)
                .HasForeignKey(m => m.ToUserId)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure decimal precision
            modelBuilder.Entity<Product>()
                .Property(p => p.BuyingPrice)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<Product>()
                .Property(p => p.SellingPrice)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<Sale>()
                .Property(s => s.TotalAmount)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<Sale>()
                .Property(s => s.AmountPaid)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<Sale>()
                .Property(s => s.ChangeGiven)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<SaleItem>()
                .Property(si => si.UnitPrice)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<SaleItem>()
                .Property(si => si.TotalPrice)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<PurchaseRequest>()
                .Property(pr => pr.TotalAmount)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<PurchaseRequestItem>()
                .Property(pri => pri.UnitPrice)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<PurchaseRequestItem>()
                .Property(pri => pri.TotalPrice)
                .HasColumnType("decimal(18,2)");

            // Configure unique constraints
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();

            modelBuilder.Entity<Product>()
                .HasIndex(p => p.SKU)
                .IsUnique();

            modelBuilder.Entity<Sale>()
                .HasIndex(s => s.SaleNumber)
                .IsUnique();

            modelBuilder.Entity<PurchaseRequest>()
                .HasIndex(pr => pr.RequestNumber)
                .IsUnique();

            // Configure default values
            modelBuilder.Entity<User>()
                .Property(u => u.CreatedAt)
                .HasDefaultValueSql("GETUTCDATE()");

            modelBuilder.Entity<User>()
                .Property(u => u.UpdatedAt)
                .HasDefaultValueSql("GETUTCDATE()");

            modelBuilder.Entity<Department>()
                .Property(d => d.CreatedAt)
                .HasDefaultValueSql("GETUTCDATE()");

            modelBuilder.Entity<Category>()
                .Property(c => c.CreatedAt)
                .HasDefaultValueSql("GETUTCDATE()");

            modelBuilder.Entity<Supplier>()
                .Property(s => s.CreatedAt)
                .HasDefaultValueSql("GETUTCDATE()");

            modelBuilder.Entity<Product>()
                .Property(p => p.CreatedAt)
                .HasDefaultValueSql("GETUTCDATE()");

            modelBuilder.Entity<Product>()
                .Property(p => p.UpdatedAt)
                .HasDefaultValueSql("GETUTCDATE()");

            modelBuilder.Entity<Sale>()
                .Property(s => s.SaleDate)
                .HasDefaultValueSql("GETUTCDATE()");

            modelBuilder.Entity<PurchaseRequest>()
                .Property(pr => pr.RequestDate)
                .HasDefaultValueSql("GETUTCDATE()");

            modelBuilder.Entity<Message>()
                .Property(m => m.SentDate)
                .HasDefaultValueSql("GETUTCDATE()");

            // Configure UserActivityLog relationships and indexes
            modelBuilder.Entity<UserActivityLog>()
                .HasOne(ual => ual.User)
                .WithMany()
                .HasForeignKey(ual => ual.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Performance indexes for UserActivityLog
            modelBuilder.Entity<UserActivityLog>()
                .HasIndex(ual => ual.UserId);

            modelBuilder.Entity<UserActivityLog>()
                .HasIndex(ual => ual.ActivityType);

            modelBuilder.Entity<UserActivityLog>()
                .HasIndex(ual => ual.CreatedAt);

            modelBuilder.Entity<UserActivityLog>()
                .HasIndex(ual => new { ual.UserId, ual.CreatedAt });

            modelBuilder.Entity<UserActivityLog>()
                .Property(ual => ual.CreatedAt)
                .HasDefaultValueSql("GETUTCDATE()");

            // Configure Customer relationships
            modelBuilder.Entity<Customer>()
                .HasIndex(c => c.Email)
                .IsUnique();

            modelBuilder.Entity<Customer>()
                .Property(c => c.CreatedAt)
                .HasDefaultValueSql("GETUTCDATE()");

            modelBuilder.Entity<Customer>()
                .Property(c => c.UpdatedAt)
                .HasDefaultValueSql("GETUTCDATE()");

            // Configure CustomerCart relationships
            modelBuilder.Entity<CustomerCart>()
                .HasOne(cc => cc.Customer)
                .WithMany(c => c.CartItems)
                .HasForeignKey(cc => cc.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<CustomerCart>()
                .HasOne(cc => cc.Product)
                .WithMany()
                .HasForeignKey(cc => cc.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<CustomerCart>()
                .Property(cc => cc.UnitPrice)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<CustomerCart>()
                .Property(cc => cc.TotalPrice)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<CustomerCart>()
                .Property(cc => cc.AddedAt)
                .HasDefaultValueSql("GETUTCDATE()");

            modelBuilder.Entity<CustomerCart>()
                .Property(cc => cc.UpdatedAt)
                .HasDefaultValueSql("GETUTCDATE()");

            // Configure ProductRequest relationships
            modelBuilder.Entity<ProductRequest>()
                .HasOne(pr => pr.Customer)
                .WithMany(c => c.ProductRequests)
                .HasForeignKey(pr => pr.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ProductRequest>()
                .HasOne(pr => pr.ProcessedByUser)
                .WithMany()
                .HasForeignKey(pr => pr.ProcessedByUserId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<ProductRequest>()
                .HasIndex(pr => pr.RequestNumber)
                .IsUnique();

            modelBuilder.Entity<ProductRequest>()
                .Property(pr => pr.TotalAmount)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<ProductRequest>()
                .Property(pr => pr.RequestDate)
                .HasDefaultValueSql("GETUTCDATE()");

            // Configure ProductRequestItem relationships
            modelBuilder.Entity<ProductRequestItem>()
                .HasOne(pri => pri.ProductRequest)
                .WithMany(pr => pr.ProductRequestItems)
                .HasForeignKey(pri => pri.ProductRequestId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ProductRequestItem>()
                .HasOne(pri => pri.Product)
                .WithMany()
                .HasForeignKey(pri => pri.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ProductRequestItem>()
                .Property(pri => pri.UnitPrice)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<ProductRequestItem>()
                .Property(pri => pri.TotalPrice)
                .HasColumnType("decimal(18,2)");

            // Configure EmployeeProfile relationships
            modelBuilder.Entity<EmployeeProfile>()
                .HasOne(ep => ep.User)
                .WithOne()
                .HasForeignKey<EmployeeProfile>(ep => ep.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<EmployeeProfile>()
                .HasIndex(ep => ep.EmployeeNumber)
                .IsUnique();

            modelBuilder.Entity<EmployeeProfile>()
                .Property(ep => ep.BaseSalary)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<EmployeeProfile>()
                .Property(ep => ep.CreatedAt)
                .HasDefaultValueSql("GETUTCDATE()");

            modelBuilder.Entity<EmployeeProfile>()
                .Property(ep => ep.UpdatedAt)
                .HasDefaultValueSql("GETUTCDATE()");

            // Configure EmployeeSalary relationships
            modelBuilder.Entity<EmployeeSalary>()
                .HasOne(es => es.EmployeeProfile)
                .WithMany(ep => ep.SalaryRecords)
                .HasForeignKey(es => es.EmployeeProfileId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<EmployeeSalary>()
                .Property(es => es.Amount)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<EmployeeSalary>()
                .Property(es => es.CreatedAt)
                .HasDefaultValueSql("GETUTCDATE()");

            // Configure EmployeeFine relationships
            modelBuilder.Entity<EmployeeFine>()
                .HasOne(ef => ef.EmployeeProfile)
                .WithMany(ep => ep.Fines)
                .HasForeignKey(ef => ef.EmployeeProfileId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<EmployeeFine>()
                .HasOne(ef => ef.IssuedByUser)
                .WithMany()
                .HasForeignKey(ef => ef.IssuedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<EmployeeFine>()
                .Property(ef => ef.Amount)
                .HasColumnType("decimal(18,2)");

            // Configure EmployeePayment relationships
            modelBuilder.Entity<EmployeePayment>()
                .HasOne(ep => ep.EmployeeProfile)
                .WithMany(epr => epr.Payments)
                .HasForeignKey(ep => ep.EmployeeProfileId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<EmployeePayment>()
                .HasOne(ep => ep.ProcessedByUser)
                .WithMany()
                .HasForeignKey(ep => ep.ProcessedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<EmployeePayment>()
                .HasIndex(ep => ep.PaymentNumber)
                .IsUnique();

            modelBuilder.Entity<EmployeePayment>()
                .Property(ep => ep.GrossPay)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<EmployeePayment>()
                .Property(ep => ep.Deductions)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<EmployeePayment>()
                .Property(ep => ep.NetPay)
                .HasColumnType("decimal(18,2)");
        }
    }
}