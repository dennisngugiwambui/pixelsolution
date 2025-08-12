using Microsoft.EntityFrameworkCore;
using PixelSolution.Data;
using PixelSolution.Models;
using PixelSolution.Services.Interfaces;
using PixelSolution.ViewModels;

namespace PixelSolution.Services
{
    public class DepartmentService : IDepartmentService
    {
        private readonly ApplicationDbContext _context;

        public DepartmentService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Department>> GetAllDepartmentsAsync()
        {
            return await _context.Departments
                .OrderBy(d => d.Name)
                .ToListAsync();
        }

        public async Task<Department?> GetDepartmentByIdAsync(int departmentId)
        {
            return await _context.Departments
                .FirstOrDefaultAsync(d => d.DepartmentId == departmentId);
        }

        public async Task<Department> CreateDepartmentAsync(Department department)
        {
            department.CreatedAt = DateTime.UtcNow;
            _context.Departments.Add(department);
            await _context.SaveChangesAsync();
            return department;
        }

        public async Task<Department> UpdateDepartmentAsync(Department department)
        {
            _context.Departments.Update(department);
            await _context.SaveChangesAsync();
            return department;
        }

        public async Task<bool> DeleteDepartmentAsync(int departmentId)
        {
            try
            {
                var department = await _context.Departments.FindAsync(departmentId);
                if (department == null)
                    return false;

                // Check if department has users
                var hasUsers = await _context.UserDepartments.AnyAsync(ud => ud.DepartmentId == departmentId);
                if (hasUsers)
                    return false;

                _context.Departments.Remove(department);
                await _context.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    public class CategoryService : ICategoryService
    {
        private readonly ApplicationDbContext _context;

        public CategoryService(ApplicationDbContext context)
        {
            _context = context;
        }

        // New method to get categories with calculated fields using stored procedure logic
        public async Task<List<CategoryListViewModel>> GetCategoriesWithStatsAsync()
        {
            var result = await _context.Database
                .SqlQueryRaw<CategoryListViewModel>(@"
                    SELECT 
                        c.CategoryId,
                        c.Name,
                        c.Description,
                        c.ImageUrl,
                        c.IsActive,
                        c.CreatedAt,
                        COUNT(p.ProductId) AS ProductCount,
                        COUNT(CASE WHEN p.IsActive = 1 THEN 1 END) AS ActiveProductCount,
                        ISNULL(SUM(CASE WHEN p.IsActive = 1 THEN p.StockQuantity * p.BuyingPrice ELSE 0 END), 0) AS TotalStockValue
                    FROM Categories c
                    LEFT JOIN Products p ON c.CategoryId = p.CategoryId
                    WHERE c.Name IS NOT NULL
                    GROUP BY c.CategoryId, c.Name, c.Description, c.ImageUrl, c.IsActive, c.CreatedAt
                    ORDER BY c.Name
                ")
                .ToListAsync();
            
            return result;
        }

        public async Task<IEnumerable<Category>> GetAllCategoriesAsync()
        {
            return await _context.Categories
                .Where(c => c.Name != null) // Filter out categories with null names
                .OrderBy(c => c.Name)
                .ToListAsync();
        }

        public async Task<IEnumerable<Category>> GetActiveCategoriesAsync()
        {
            return await _context.Categories
                .Where(c => c.IsActive && c.Name != null) // Filter out categories with null names
                .OrderBy(c => c.Name)
                .ToListAsync();
        }

        public async Task<Category?> GetCategoryByIdAsync(int categoryId)
        {
            return await _context.Categories
                .FirstOrDefaultAsync(c => c.CategoryId == categoryId);
        }

        public async Task<Category> CreateCategoryAsync(Category category)
        {
            category.CreatedAt = DateTime.UtcNow;
            _context.Categories.Add(category);
            await _context.SaveChangesAsync();
            return category;
        }

        public async Task<Category> UpdateCategoryAsync(Category category)
        {
            _context.Categories.Update(category);
            await _context.SaveChangesAsync();
            return category;
        }

        public async Task<bool> DeleteCategoryAsync(int categoryId)
        {
            try
            {
                var category = await _context.Categories.FindAsync(categoryId);
                if (category == null)
                    return false;

                // Check if category has products
                var hasProducts = await _context.Products.AnyAsync(p => p.CategoryId == categoryId);
                if (hasProducts)
                    return false;

                _context.Categories.Remove(category);
                await _context.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> ToggleCategoryStatusAsync(int categoryId)
        {
            try
            {
                var category = await _context.Categories.FindAsync(categoryId);
                if (category == null)
                    return false;

                category.IsActive = !category.IsActive;
                await _context.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    public class SupplierService : ISupplierService
    {
        private readonly ApplicationDbContext _context;

        public SupplierService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Supplier>> GetAllSuppliersAsync()
        {
            return await _context.Suppliers
                .OrderBy(s => s.CompanyName)
                .ToListAsync();
        }

        public async Task<IEnumerable<Supplier>> GetActiveSuppliersAsync()
        {
            return await _context.Suppliers
                .Where(s => s.Status == "Active")
                .OrderBy(s => s.CompanyName)
                .ToListAsync();
        }

        public async Task<Supplier?> GetSupplierByIdAsync(int supplierId)
        {
            return await _context.Suppliers
                .FirstOrDefaultAsync(s => s.SupplierId == supplierId);
        }

        public async Task<Supplier> CreateSupplierAsync(Supplier supplier)
        {
            supplier.CreatedAt = DateTime.UtcNow;
            supplier.Status = "Active";
            _context.Suppliers.Add(supplier);
            await _context.SaveChangesAsync();
            return supplier;
        }

        public async Task<Supplier> UpdateSupplierAsync(Supplier supplier)
        {
            _context.Suppliers.Update(supplier);
            await _context.SaveChangesAsync();
            return supplier;
        }

        public async Task<bool> DeleteSupplierAsync(int supplierId)
        {
            try
            {
                var supplier = await _context.Suppliers.FindAsync(supplierId);
                if (supplier == null)
                    return false;

                // Check if supplier has products or purchase requests
                var hasProducts = await _context.Products.AnyAsync(p => p.SupplierId == supplierId);
                var hasPurchaseRequests = await _context.PurchaseRequests.AnyAsync(pr => pr.SupplierId == supplierId);

                if (hasProducts || hasPurchaseRequests)
                {
                    // Deactivate instead of delete
                    supplier.Status = "Inactive";
                    await _context.SaveChangesAsync();
                    return true;
                }

                _context.Suppliers.Remove(supplier);
                await _context.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> ToggleSupplierStatusAsync(int supplierId)
        {
            try
            {
                var supplier = await _context.Suppliers.FindAsync(supplierId);
                if (supplier == null)
                    return false;

                supplier.Status = supplier.Status == "Active" ? "Inactive" : "Active";
                await _context.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    public class ProductService : IProductService
    {
        private readonly ApplicationDbContext _context;

        public ProductService(ApplicationDbContext context)
        {
            _context = context;
        }

        // Enhanced method to get products with stats - includes all products for admin view
        public async Task<List<ProductListViewModel>> GetProductsWithStatsAsync()
        {
            try
            {
                Console.WriteLine("🔍 ProductService: Starting GetProductsWithStatsAsync...");
                
                // First, let's check what's actually in the database
                var rawProducts = await _context.Products
                    .Include(p => p.Category)
                    .Include(p => p.Supplier)
                    .Where(p => p.Name != null)
                    .ToListAsync();
                    
                Console.WriteLine($"📊 Raw products from DB: {rawProducts.Count} found");
                foreach (var rawProduct in rawProducts.Take(5))
                {
                    Console.WriteLine($"🔍 Raw Product: ID={rawProduct.ProductId}, Name={rawProduct.Name}, SKU={rawProduct.SKU}");
                }
                
                // Get all products including inactive ones for admin view
                var products = await _context.Products
                    .Include(p => p.Category)
                    .Include(p => p.Supplier)
                    .Where(p => p.Name != null) // Only filter out null names
                    .Select(p => new ProductListViewModel
                    {
                        ProductId = p.ProductId,
                        Name = p.Name,
                        Description = p.Description ?? "",
                        SKU = p.SKU,
                        CategoryName = p.Category != null ? p.Category.Name : "No Category",
                        SupplierName = p.Supplier != null ? p.Supplier.CompanyName : "No Supplier",
                        BuyingPrice = p.BuyingPrice,
                        SellingPrice = p.SellingPrice,
                        StockQuantity = p.StockQuantity,
                        MinStockLevel = p.MinStockLevel,
                        ImageUrl = p.ImageUrl ?? "",
                        IsActive = p.IsActive,
                        CreatedAt = p.CreatedAt,
                        UpdatedAt = p.UpdatedAt,
                        IsLowStock = p.StockQuantity <= p.MinStockLevel,
                        ProfitMargin = p.SellingPrice - p.BuyingPrice,
                        ProfitPercentage = p.BuyingPrice > 0 ? ((p.SellingPrice - p.BuyingPrice) / p.BuyingPrice) * 100 : 0,
                        TotalSold = 0, // Will be calculated separately if needed
                        TotalRevenue = 0, // Will be calculated separately if needed
                        Barcode = "PIX" + p.ProductId.ToString("D6") + (p.SKU ?? "")
                    })
                    .OrderBy(p => p.Name)
            .ToListAsync();
        
        Console.WriteLine($"📦 ProductService: Found {products.Count} products in database");
        
        // DEBUG: Log the final ProductListViewModel objects being returned
        Console.WriteLine("🔍 Final ProductListViewModel objects:");
        foreach (var product in products.Take(5))
        {
            Console.WriteLine($"📋 ViewModel Product: ID={product.ProductId}, Name={product.Name}, SKU={product.SKU}");
        }
        
        var zeroIdViewModels = products.Where(p => p.ProductId == 0).ToList();
        if (zeroIdViewModels.Any())
        {
            Console.WriteLine($"⚠️ WARNING: Found {zeroIdViewModels.Count} ProductListViewModel objects with ProductId = 0!");
            foreach (var zeroProduct in zeroIdViewModels.Take(3))
            {
                Console.WriteLine($"❌ Zero ID ViewModel: Name={zeroProduct.Name}, SKU={zeroProduct.SKU}");
            }
        }
        
        return products;
            }
            catch (Exception ex)
            {
                // Enhanced error logging
                Console.WriteLine($"Error loading products: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return new List<ProductListViewModel>();
            }
        }

        public async Task<IEnumerable<Product>> GetAllProductsAsync()
        {
            return await _context.Products
                .Include(p => p.Category)
                .Include(p => p.Supplier)
                .Where(p => p.Name != null) // Filter out products with null names
                .OrderBy(p => p.Name)
                .ToListAsync();
        }

        public async Task<IEnumerable<Product>> GetActiveProductsAsync()
        {
            return await _context.Products
                .Include(p => p.Category)
                .Include(p => p.Supplier)
                .Where(p => p.IsActive)
                .OrderBy(p => p.Name)
                .ToListAsync();
        }

        public async Task<Product?> GetProductByIdAsync(int productId)
        {
            return await _context.Products
                .Include(p => p.Category)
                .Include(p => p.Supplier)
                .FirstOrDefaultAsync(p => p.ProductId == productId);
        }

        public async Task<Product?> GetProductBySkuAsync(string sku)
        {
            return await _context.Products
                .Include(p => p.Category)
                .Include(p => p.Supplier)
                .FirstOrDefaultAsync(p => p.SKU == sku);
        }

        public async Task<IEnumerable<Product>> GetProductsByCategoryAsync(int categoryId)
        {
            return await _context.Products
                .Include(p => p.Category)
                .Include(p => p.Supplier)
                .Where(p => p.CategoryId == categoryId && p.IsActive)
                .OrderBy(p => p.Name)
                .ToListAsync();
        }

        public async Task<IEnumerable<Product>> GetProductsBySupplierAsync(int supplierId)
        {
            return await _context.Products
                .Include(p => p.Category)
                .Include(p => p.Supplier)
                .Where(p => p.SupplierId == supplierId && p.IsActive)
                .OrderBy(p => p.Name)
                .ToListAsync();
        }

        public async Task<IEnumerable<Product>> GetLowStockProductsAsync()
        {
            return await _context.Products
                .Include(p => p.Category)
                .Include(p => p.Supplier)
                .Where(p => p.IsActive && p.StockQuantity <= p.MinStockLevel)
                .OrderBy(p => p.StockQuantity)
                .ToListAsync();
        }

        public async Task<Product> CreateProductAsync(Product product)
        {
            product.CreatedAt = DateTime.UtcNow;
            product.UpdatedAt = DateTime.UtcNow;
            product.IsActive = true;

            _context.Products.Add(product);
            await _context.SaveChangesAsync();

            return await GetProductByIdAsync(product.ProductId) ?? product;
        }

        public async Task<Product> UpdateProductAsync(Product product)
        {
            var existingProduct = await _context.Products.FindAsync(product.ProductId);
            if (existingProduct == null)
                throw new ArgumentException("Product not found");

            existingProduct.Name = product.Name;
            existingProduct.Description = product.Description;
            existingProduct.SKU = product.SKU;
            existingProduct.CategoryId = product.CategoryId;
            existingProduct.SupplierId = product.SupplierId;
            existingProduct.BuyingPrice = product.BuyingPrice;
            existingProduct.SellingPrice = product.SellingPrice;
            existingProduct.StockQuantity = product.StockQuantity;
            existingProduct.MinStockLevel = product.MinStockLevel;
            existingProduct.ImageUrl = product.ImageUrl;
            existingProduct.IsActive = product.IsActive;
            existingProduct.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return await GetProductByIdAsync(product.ProductId) ?? existingProduct;
        }

        public async Task<bool> DeleteProductAsync(int productId)
        {
            try
            {
                var product = await _context.Products.FindAsync(productId);
                if (product == null)
                    return false;

                // Check if product has sales or purchase request items
                var hasSales = await _context.SaleItems.AnyAsync(si => si.ProductId == productId);
                var hasPurchaseRequests = await _context.PurchaseRequestItems.AnyAsync(pri => pri.ProductId == productId);

                if (hasSales || hasPurchaseRequests)
                {
                    // Deactivate instead of delete
                    product.IsActive = false;
                    product.UpdatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                    return true;
                }

                _context.Products.Remove(product);
                await _context.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> UpdateStockAsync(int productId, int quantity)
        {
            try
            {
                var product = await _context.Products.FindAsync(productId);
                if (product == null)
                    return false;

                product.StockQuantity = quantity;
                product.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> ToggleProductStatusAsync(int productId)
        {
            try
            {
                var product = await _context.Products.FindAsync(productId);
                if (product == null)
                    return false;

                product.IsActive = !product.IsActive;
                product.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}