using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PixelSolution.Data;

namespace PixelSolution.Services
{
    public class ExcelExportService
    {
        private readonly ApplicationDbContext _context;

        public ExcelExportService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<byte[]> GenerateSalesReportExcelAsync(DateTime startDate, DateTime endDate)
        {
            var sales = await _context.Sales
                .Include(s => s.User)
                .Include(s => s.SaleItems)
                    .ThenInclude(si => si.Product)
                        .ThenInclude(p => p.Category)
                .Where(s => s.SaleDate >= startDate && s.SaleDate <= endDate && s.Status == "Completed")
                .OrderByDescending(s => s.SaleDate)
                .ToListAsync();

            var csv = new StringBuilder();
            
            // Add title and period
            csv.AppendLine("Sales Report");
            csv.AppendLine($"Period: {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}");
            csv.AppendLine();
            
            // Add headers
            csv.AppendLine("Sale ID,Date,Customer,User,Items,Total Amount,Payment Method,Status");
            
            // Add data rows
            foreach (var sale in sales)
            {
                var customerEmail = EscapeCsvField(sale.CustomerEmail ?? "Walk-in Customer");
                var userName = EscapeCsvField(sale.User?.FullName ?? "Unknown");
                var paymentMethod = EscapeCsvField(sale.PaymentMethod ?? "Cash");
                var status = EscapeCsvField(sale.Status);
                
                csv.AppendLine($"{sale.SaleId},{sale.SaleDate:yyyy-MM-dd HH:mm},{customerEmail},{userName},{sale.SaleItems?.Count ?? 0},{sale.TotalAmount:F2},{paymentMethod},{status}");
            }
            
            // Add summary
            csv.AppendLine();
            csv.AppendLine("Summary:");
            csv.AppendLine($"Total Sales,{sales.Sum(s => s.TotalAmount):F2}");
            csv.AppendLine($"Total Orders,{sales.Count}");
            
            return Encoding.UTF8.GetBytes(csv.ToString());
        }

        public async Task<byte[]> GenerateInventoryReportExcelAsync()
        {
            var products = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.Supplier)
                .Where(p => p.IsActive)
                .OrderBy(p => p.Name)
                .ToListAsync();

            var csv = new StringBuilder();
            
            // Add title
            csv.AppendLine("Inventory Report");
            csv.AppendLine($"Generated on: {DateTime.Now:yyyy-MM-dd HH:mm}");
            csv.AppendLine();
            
            // Add headers
            csv.AppendLine("Product ID,Name,SKU,Category,Supplier,Stock Quantity,Unit Price,Total Value,Status");
            
            // Add data rows
            decimal totalInventoryValue = 0;
            foreach (var product in products)
            {
                var totalValue = product.StockQuantity * product.SellingPrice;
                totalInventoryValue += totalValue;
                
                var name = EscapeCsvField(product.Name);
                var sku = EscapeCsvField(product.SKU ?? "");
                var categoryName = EscapeCsvField(product.Category?.Name ?? "No Category");
                var supplierName = EscapeCsvField(product.Supplier?.CompanyName ?? "No Supplier");
                var status = product.StockQuantity <= 10 ? "Low Stock" : "In Stock";
                
                csv.AppendLine($"{product.ProductId},{name},{sku},{categoryName},{supplierName},{product.StockQuantity},{product.SellingPrice:F2},{totalValue:F2},{status}");
            }
            
            // Add summary
            csv.AppendLine();
            csv.AppendLine("Summary:");
            csv.AppendLine($"Total Products,{products.Count}");
            csv.AppendLine($"Total Inventory Value,{totalInventoryValue:F2}");
            csv.AppendLine($"Low Stock Items,{products.Count(p => p.StockQuantity <= 10)}");
            
            return Encoding.UTF8.GetBytes(csv.ToString());
        }

        public async Task<byte[]> GenerateUserReportExcelAsync()
        {
            var users = await _context.Users
                .Where(u => u.Status == "Active")
                .OrderBy(u => u.LastName)
                .ThenBy(u => u.FirstName)
                .ToListAsync();

            var csv = new StringBuilder();
            
            // Add title
            csv.AppendLine("User Report");
            csv.AppendLine($"Generated on: {DateTime.Now:yyyy-MM-dd HH:mm}");
            csv.AppendLine();
            
            // Add headers
            csv.AppendLine("User ID,Full Name,Email,Phone,User Type,Department,Created Date");
            
            // Add data rows
            foreach (var user in users)
            {
                var fullName = EscapeCsvField(user.FullName);
                var email = EscapeCsvField(user.Email);
                var phone = EscapeCsvField(user.Phone ?? "N/A");
                var userType = EscapeCsvField(user.UserType);
                var department = EscapeCsvField("No Department"); // Department relationship not available
                
                csv.AppendLine($"{user.UserId},{fullName},{email},{phone},{userType},{department},{user.CreatedAt:yyyy-MM-dd}");
            }
            
            // Add summary
            csv.AppendLine();
            csv.AppendLine("Summary:");
            csv.AppendLine($"Total Active Users,{users.Count}");
            csv.AppendLine($"Administrators,{users.Count(u => u.UserType == "Administrator")}");
            csv.AppendLine($"Employees,{users.Count(u => u.UserType == "Employee")}");
            
            return Encoding.UTF8.GetBytes(csv.ToString());
        }

        public async Task<byte[]> GenerateCategoriesReportExcelAsync()
        {
            var categories = await _context.Categories
                .Include(c => c.Products)
                .Where(c => c.IsActive)
                .OrderBy(c => c.Name)
                .ToListAsync();

            var csv = new StringBuilder();
            
            // Add title
            csv.AppendLine("Categories Report");
            csv.AppendLine($"Generated on: {DateTime.Now:yyyy-MM-dd HH:mm}");
            csv.AppendLine();
            
            // Add headers
            csv.AppendLine("Category ID,Name,Description,Product Count,Active Products,Created Date");
            
            // Add data rows
            foreach (var category in categories)
            {
                var name = EscapeCsvField(category.Name);
                var description = EscapeCsvField(category.Description ?? "No description");
                
                csv.AppendLine($"{category.CategoryId},{name},{description},{category.Products?.Count ?? 0},{category.Products?.Count(p => p.IsActive) ?? 0},{category.CreatedAt:yyyy-MM-dd}");
            }
            
            // Add summary
            csv.AppendLine();
            csv.AppendLine("Summary:");
            csv.AppendLine($"Total Categories,{categories.Count}");
            csv.AppendLine($"Total Products,{categories.Sum(c => c.Products?.Count ?? 0)}");
            
            return Encoding.UTF8.GetBytes(csv.ToString());
        }
        
        private string EscapeCsvField(string field)
        {
            if (string.IsNullOrEmpty(field))
                return "";
                
            // If field contains comma, newline, or quotes, wrap in quotes and escape internal quotes
            if (field.Contains(',') || field.Contains('\n') || field.Contains('\r') || field.Contains('"'))
            {
                return $"\"{field.Replace("\"", "\"\"")}\";";
            }
            
            return field;
        }
    }
}
