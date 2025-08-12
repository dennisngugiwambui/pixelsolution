using Microsoft.EntityFrameworkCore;
using PixelSolution.Models;

namespace PixelSolution.Data
{
    public static class DbInitializer
    {
        public static async Task InitializeAsync(ApplicationDbContext context)
        {
            try
            {
                // Ensure database is created
                await context.Database.EnsureCreatedAsync();

                // ALWAYS CLEAR AND RESET - Force fresh start every time
                Console.WriteLine("=== FORCING DATABASE RESET (DEBUG MODE) ===");
                await ClearExistingDataAsync(context);

                // Seed Departments first
                await SeedDepartmentsAsync(context);

                // Seed Categories
                await SeedCategoriesAsync(context);

                // Seed Suppliers
                await SeedSuppliersAsync(context);

                // Seed Users with PLAIN TEXT passwords for debugging
                await SeedUsersAsync(context);

                // Seed Products
                await SeedProductsAsync(context);

                // Seed Sample Messages
                await SeedMessagesAsync(context);

                await context.SaveChangesAsync();

                Console.WriteLine("=== DATABASE RESET COMPLETED (DEBUG MODE) ===");
                Console.WriteLine("DEBUGGING: Passwords stored as PLAIN TEXT");
                Console.WriteLine("Login Credentials:");
                Console.WriteLine("Admin: dennisngugi219@gmail.com / Admin1234");
                Console.WriteLine("Employee: sales@pixelsolution.com / Employee123!");
                Console.WriteLine("Manager: manager@pixelsolution.com / Manager123!");
                Console.WriteLine("========================================================");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error seeding database: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                throw;
            }
        }

        private static async Task ClearExistingDataAsync(ApplicationDbContext context)
        {
            try
            {
                Console.WriteLine("Clearing all existing data...");

                // Clear all data in correct order (respecting foreign key constraints)
                var messages = await context.Messages.ToListAsync();
                if (messages.Any())
                {
                    context.Messages.RemoveRange(messages);
                    Console.WriteLine($"Removed {messages.Count} messages");
                }

                var purchaseRequestItems = await context.PurchaseRequestItems.ToListAsync();
                if (purchaseRequestItems.Any())
                {
                    context.PurchaseRequestItems.RemoveRange(purchaseRequestItems);
                    Console.WriteLine($"Removed {purchaseRequestItems.Count} purchase request items");
                }

                var purchaseRequests = await context.PurchaseRequests.ToListAsync();
                if (purchaseRequests.Any())
                {
                    context.PurchaseRequests.RemoveRange(purchaseRequests);
                    Console.WriteLine($"Removed {purchaseRequests.Count} purchase requests");
                }

                var saleItems = await context.SaleItems.ToListAsync();
                if (saleItems.Any())
                {
                    context.SaleItems.RemoveRange(saleItems);
                    Console.WriteLine($"Removed {saleItems.Count} sale items");
                }

                var sales = await context.Sales.ToListAsync();
                if (sales.Any())
                {
                    context.Sales.RemoveRange(sales);
                    Console.WriteLine($"Removed {sales.Count} sales");
                }

                var products = await context.Products.ToListAsync();
                if (products.Any())
                {
                    context.Products.RemoveRange(products);
                    Console.WriteLine($"Removed {products.Count} products");
                }

                var users = await context.Users.ToListAsync();
                if (users.Any())
                {
                    context.Users.RemoveRange(users);
                    Console.WriteLine($"Removed {users.Count} users (including old hashed passwords)");
                }

                var suppliers = await context.Suppliers.ToListAsync();
                if (suppliers.Any())
                {
                    context.Suppliers.RemoveRange(suppliers);
                    Console.WriteLine($"Removed {suppliers.Count} suppliers");
                }

                var categories = await context.Categories.ToListAsync();
                if (categories.Any())
                {
                    context.Categories.RemoveRange(categories);
                    Console.WriteLine($"Removed {categories.Count} categories");
                }

                var departments = await context.Departments.ToListAsync();
                if (departments.Any())
                {
                    context.Departments.RemoveRange(departments);
                    Console.WriteLine($"Removed {departments.Count} departments");
                }

                await context.SaveChangesAsync();
                Console.WriteLine("=== ALL EXISTING DATA CLEARED SUCCESSFULLY ===");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error clearing existing data: {ex.Message}");
                throw;
            }
        }

        private static async Task SeedDepartmentsAsync(ApplicationDbContext context)
        {
            var departments = new Department[]
            {
                new Department
                {
                    Name = "Administration",
                    Description = "Administrative and management department",
                    CreatedAt = DateTime.UtcNow
                },
                new Department
                {
                    Name = "Sales",
                    Description = "Sales and customer service department",
                    CreatedAt = DateTime.UtcNow
                },
                new Department
                {
                    Name = "Procurement",
                    Description = "Purchasing and supplier management department",
                    CreatedAt = DateTime.UtcNow
                },
                new Department
                {
                    Name = "Inventory",
                    Description = "Stock and inventory management department",
                    CreatedAt = DateTime.UtcNow
                }
            };

            await context.Departments.AddRangeAsync(departments);
            await context.SaveChangesAsync();
            Console.WriteLine("Departments seeded successfully");
        }

        private static async Task SeedCategoriesAsync(ApplicationDbContext context)
        {
            var categories = new Category[]
            {
                new Category
                {
                    Name = "Electronics",
                    Description = "Electronic devices and accessories",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                },
                new Category
                {
                    Name = "Home & Garden",
                    Description = "Home improvement and garden supplies",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                },
                new Category
                {
                    Name = "Fashion",
                    Description = "Clothing and fashion accessories",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                },
                new Category
                {
                    Name = "Sports & Outdoors",
                    Description = "Sports equipment and outdoor gear",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                },
                new Category
                {
                    Name = "Books & Media",
                    Description = "Books, magazines, and media content",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                }
            };

            await context.Categories.AddRangeAsync(categories);
            await context.SaveChangesAsync();
            Console.WriteLine("Categories seeded successfully");
        }

        private static async Task SeedSuppliersAsync(ApplicationDbContext context)
        {
            var suppliers = new Supplier[]
            {
                new Supplier
                {
                    CompanyName = "TechSupply Co.",
                    ContactPerson = "John Smith",
                    Email = "john@techsupply.com",
                    Phone = "+254711000001",
                    Address = "Industrial Area, Nairobi",
                    Status = "Active",
                    CreatedAt = DateTime.UtcNow
                },
                new Supplier
                {
                    CompanyName = "Global Electronics Ltd",
                    ContactPerson = "Mary Johnson",
                    Email = "mary@globalelectronics.com",
                    Phone = "+254711000002",
                    Address = "Westlands, Nairobi",
                    Status = "Active",
                    CreatedAt = DateTime.UtcNow
                },
                new Supplier
                {
                    CompanyName = "Fashion Hub Kenya",
                    ContactPerson = "Peter Kamau",
                    Email = "peter@fashionhub.co.ke",
                    Phone = "+254711000003",
                    Address = "CBD, Nairobi",
                    Status = "Active",
                    CreatedAt = DateTime.UtcNow
                }
            };

            await context.Suppliers.AddRangeAsync(suppliers);
            await context.SaveChangesAsync();
            Console.WriteLine("Suppliers seeded successfully");
        }

        private static async Task SeedUsersAsync(ApplicationDbContext context)
        {
            if (await context.Users.AnyAsync()) 
            {
                Console.WriteLine("Users already seeded");
                return;
            }

            // Get departments
            var managementDept = await context.Departments.FirstOrDefaultAsync(d => d.Name == "Administration");
            var salesDept = await context.Departments.FirstOrDefaultAsync(d => d.Name == "Sales");
            var supportDept = await context.Departments.FirstOrDefaultAsync(d => d.Name == "Procurement");

            if (managementDept == null || salesDept == null || supportDept == null)
            {
                throw new Exception("Required departments (Administration, Sales, Procurement) not found for seeding users.");
            }

            Console.WriteLine("Seeding users...");
            var users = new User[]
            {
                new User
                {
                    FirstName = "Dennis",
                    LastName = "Ngugi",
                    Email = "dennisngugi219@gmail.com",
                    Phone = "+254742282250",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin1234", 12),
                    UserType = "Admin",
                    Status = "Active",
                    Privileges = "All",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new User
                {
                    FirstName = "Jane",
                    LastName = "Doe",
                    Email = "sales@pixelsolution.com",
                    Phone = "+254712000001",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("Employee123!", 12),
                    UserType = "Employee",
                    Status = "Active",
                    Privileges = "Sales,View",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new User
                {
                    FirstName = "John",
                    LastName = "Manager",
                    Email = "manager@pixelsolution.com",
                    Phone = "+254712000002",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("Manager123!", 12),
                    UserType = "Manager",
                    Status = "Active",
                    Privileges = "Manage,Reports,View",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new User
                {
                    FirstName = "Peter",
                    LastName = "Jones",
                    Email = "support@pixelsolution.com",
                    Phone = "+254712000003",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("Support123!", 12),
                    UserType = "Employee",
                    Status = "Active",
                    Privileges = "Support,View",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                }
            };

            await context.Users.AddRangeAsync(users);
            await context.SaveChangesAsync();

            // Assign departments to users
            var adminUser = await context.Users.FirstAsync(u => u.Email == "dennisngugi219@gmail.com");
            var salesUser = await context.Users.FirstAsync(u => u.Email == "sales@pixelsolution.com");
            var managerUser = await context.Users.FirstAsync(u => u.Email == "manager@pixelsolution.com");
            var supportUser = await context.Users.FirstAsync(u => u.Email == "support@pixelsolution.com");

            var userDepartments = new[]
            {
                new UserDepartment { UserId = adminUser.UserId, DepartmentId = managementDept.DepartmentId },
                new UserDepartment { UserId = salesUser.UserId, DepartmentId = salesDept.DepartmentId },
                new UserDepartment { UserId = managerUser.UserId, DepartmentId = managementDept.DepartmentId },
                new UserDepartment { UserId = supportUser.UserId, DepartmentId = supportDept.DepartmentId },
                new UserDepartment { UserId = managerUser.UserId, DepartmentId = salesDept.DepartmentId }
            };

            await context.UserDepartments.AddRangeAsync(userDepartments);
            await context.SaveChangesAsync();

            Console.WriteLine("Users and department links seeded successfully");
        }

        private static async Task SeedProductsAsync(ApplicationDbContext context)
        {
            // Get categories and suppliers
            var electronicsCategory = await context.Categories
                .FirstOrDefaultAsync(c => c.Name == "Electronics");
            var homeCategory = await context.Categories
                .FirstOrDefaultAsync(c => c.Name == "Home & Garden");
            var fashionCategory = await context.Categories
                .FirstOrDefaultAsync(c => c.Name == "Fashion");

            var techSupplier = await context.Suppliers
                .FirstOrDefaultAsync(s => s.CompanyName == "TechSupply Co.");
            var globalElectronics = await context.Suppliers
                .FirstOrDefaultAsync(s => s.CompanyName == "Global Electronics Ltd");
            var fashionHub = await context.Suppliers
                .FirstOrDefaultAsync(s => s.CompanyName == "Fashion Hub Kenya");

            if (electronicsCategory == null || homeCategory == null || fashionCategory == null)
            {
                throw new Exception("Required categories not found");
            }

            var products = new Product[]
            {
                new Product
                {
                    Name = "Samsung Galaxy Phone",
                    Description = "Latest Samsung Galaxy smartphone with advanced features",
                    SKU = "SAM-GAL-001",
                    CategoryId = electronicsCategory.CategoryId,
                    SupplierId = globalElectronics?.SupplierId,
                    BuyingPrice = 45000.00m,
                    SellingPrice = 55000.00m,
                    StockQuantity = 25,
                    MinStockLevel = 5,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new Product
                {
                    Name = "MacBook Pro Laptop",
                    Description = "Apple MacBook Pro with M2 chip",
                    SKU = "APP-MBP-001",
                    CategoryId = electronicsCategory.CategoryId,
                    SupplierId = techSupplier?.SupplierId,
                    BuyingPrice = 120000.00m,
                    SellingPrice = 145000.00m,
                    StockQuantity = 10,
                    MinStockLevel = 3,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                }
            };

            await context.Products.AddRangeAsync(products);
            await context.SaveChangesAsync();
            Console.WriteLine("Products seeded successfully");
        }

        private static async Task SeedMessagesAsync(ApplicationDbContext context)
        {
            var adminUser = await context.Users
                .FirstOrDefaultAsync(u => u.Email == "dennisngugi219@gmail.com");

            if (adminUser != null)
            {
                var welcomeMessage = new Message
                {
                    FromUserId = adminUser.UserId,
                    ToUserId = adminUser.UserId,
                    Subject = "Welcome to PixelSolution",
                    Content = "Welcome to the PixelSolution Sales Management System. This system will help you manage sales, inventory, suppliers, and much more efficiently.",
                    MessageType = "General",
                    IsRead = false,
                    SentDate = DateTime.UtcNow
                };

                await context.Messages.AddAsync(welcomeMessage);
                await context.SaveChangesAsync();
                Console.WriteLine("Messages seeded successfully");
            }
        }

        // Method to convert plain text passwords to hashed passwords (use after debugging)
        public static async Task ConvertToHashedPasswordsAsync(ApplicationDbContext context)
        {
            try
            {
                var users = await context.Users.ToListAsync();

                foreach (var user in users)
                {
                    // The current PasswordHash field contains plain text
                    string plainTextPassword = user.PasswordHash;

                    // Convert to BCrypt hash
                    user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(plainTextPassword, 12);
                    user.UpdatedAt = DateTime.UtcNow;

                    // Verify the hash works
                    bool isValid = BCrypt.Net.BCrypt.Verify(plainTextPassword, user.PasswordHash);
                    Console.WriteLine($"Converted {user.Email}: {plainTextPassword} -> HASHED (verification: {isValid})");
                }

                await context.SaveChangesAsync();
                Console.WriteLine("All passwords converted to hashed format successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error converting passwords to hashed format: {ex.Message}");
                throw;
            }
        }
    }
}