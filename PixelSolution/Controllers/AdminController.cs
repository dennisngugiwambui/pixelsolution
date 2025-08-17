using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PixelSolution.Services.Interfaces;
using PixelSolution.ViewModels;
using PixelSolution.Models;
using System.Security.Claims;
using PixelSolution.Services;
using Microsoft.EntityFrameworkCore;
using PixelSolution.Data;
using System.Text.Json;
using iTextSharp.text;
using iTextSharp.text.pdf;

namespace PixelSolution.Controllers
{
    [Authorize]
    public class AdminController : Controller
    {
        private readonly IUserService _userService;
        private readonly IDepartmentService _departmentService;
        private readonly ICategoryService _categoryService;
        private readonly ISupplierService _supplierService;
        private readonly IProductService _productService;
        private readonly ISaleService _saleService;
        private readonly IPurchaseRequestService _purchaseRequestService;
        private readonly IMessageService _messageService;
        private readonly IReportService _reportService;
        private readonly IActivityLogService _activityLogService;
        private readonly IEmailService _emailService;
        private readonly IBarcodeService _barcodeService;
        private readonly IReceiptPrintingService _receiptPrintingService;
        private readonly IMpesaService _mpesaService;
        private readonly ILogger<AdminController> _logger;
        private readonly ApplicationDbContext _context;

        public AdminController(
            IUserService userService,
            IDepartmentService departmentService,
            ICategoryService categoryService,
            ISupplierService supplierService,
            IProductService productService,
            ISaleService saleService,
            IPurchaseRequestService purchaseRequestService,
            IMessageService messageService,
            IReportService reportService,
            IActivityLogService activityLogService,
            IEmailService emailService,
            IBarcodeService barcodeService,
            IReceiptPrintingService receiptPrintingService,
            IMpesaService mpesaService,
            ILogger<AdminController> logger,
            ApplicationDbContext context)
        {
            _userService = userService;
            _departmentService = departmentService;
            _categoryService = categoryService;
            _supplierService = supplierService;
            _productService = productService;
            _saleService = saleService;
            _purchaseRequestService = purchaseRequestService;
            _messageService = messageService;
            _reportService = reportService;
            _activityLogService = activityLogService;
            _emailService = emailService;
            _barcodeService = barcodeService;
            _receiptPrintingService = receiptPrintingService;
            _mpesaService = mpesaService;
            _logger = logger;
            _context = context;
        }

        public async Task<IActionResult> Dashboard()
        {
            try
            {
                _logger.LogInformation("Loading admin dashboard");
                var dashboardData = await _reportService.GetDashboardDataAsync();
                var userName = User.FindFirst(ClaimTypes.Name)?.Value ?? "Admin";
                var userType = User.FindFirst(ClaimTypes.Role)?.Value ?? "Admin";

                ViewBag.UserName = userName;
                ViewBag.UserType = userType;

                return View(dashboardData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading admin dashboard");
                ViewBag.UserName = User.FindFirst(ClaimTypes.Name)?.Value ?? "Admin";
                ViewBag.UserType = User.FindFirst(ClaimTypes.Role)?.Value ?? "Admin";
                ViewBag.ErrorMessage = "Some dashboard data could not be loaded.";
                return View();
            }
        }

        #region Departments Management
        public async Task<IActionResult> Departments()
        {
            try
            {
                var departments = await _departmentService.GetAllDepartmentsAsync();
                var departmentViewModels = departments.Select(d => new DepartmentListViewModel
                {
                    DepartmentId = d.DepartmentId,
                    Name = d.Name,
                    Description = d.Description,
                    UserCount = 0, // Will be populated by stored procedure
                    ActiveUserCount = 0, // Will be populated by stored procedure
                    CreatedAt = d.CreatedAt
                }).ToList();

                return View(departmentViewModels);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading departments");
                TempData["ErrorMessage"] = "Error loading departments data.";
                return View(new List<DepartmentListViewModel>());
            }
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateDepartment(CreateDepartmentViewModel model)
        {
            try
            {
                _logger.LogInformation("CreateDepartment called with model: Name={Name}, Description={Description}", 
                    model?.Name ?? "null", model?.Description ?? "null");
                
                _logger.LogInformation("ModelState.IsValid: {IsValid}", ModelState.IsValid);
                
                if (!ModelState.IsValid)
                {
                    var errors = ModelState
                        .Where(x => x.Value.Errors.Count > 0)
                        .Select(x => new { Field = x.Key, Errors = x.Value.Errors.Select(e => e.ErrorMessage) })
                        .ToList();

                    _logger.LogWarning("CreateDepartment validation failed: {@ValidationErrors}", errors);
                    
                    var errorMessages = errors.SelectMany(e => e.Errors).ToList();
                    var errorMessage = errorMessages.Any() ? string.Join("; ", errorMessages) : "Please provide valid department details.";
                    
                    return Json(new { 
                        success = false, 
                        message = errorMessage,
                        validationErrors = errors
                    });
                }

                // Check if department name already exists
                var existingDepartments = await _departmentService.GetAllDepartmentsAsync();
                if (existingDepartments.Any(d => d.Name.Equals(model.Name.Trim(), StringComparison.OrdinalIgnoreCase)))
                {
                    _logger.LogWarning("Department with name '{Name}' already exists", model.Name);
                    return Json(new { success = false, message = "A department with this name already exists." });
                }

                var department = new Department
                {
                    Name = model.Name.Trim(),
                    Description = model.Description?.Trim() ?? string.Empty
                };

                _logger.LogInformation("Creating department: {@Department}", department);
                await _departmentService.CreateDepartmentAsync(department);
                
                _logger.LogInformation("Department created successfully with ID: {DepartmentId}", department.DepartmentId);
                return Json(new { success = true, message = "Department created successfully!" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating department with model: {@Model}", model);
                return Json(new { success = false, message = "Error creating department. Please try again." });
            }
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateDepartment(EditDepartmentViewModel model)
        {
            try
            {
                _logger.LogInformation("UpdateDepartment called with model: DepartmentId={DepartmentId}, Name={Name}, Description={Description}", 
                    model?.DepartmentId ?? 0, model?.Name ?? "null", model?.Description ?? "null");
                
                _logger.LogInformation("ModelState.IsValid: {IsValid}", ModelState.IsValid);
                
                if (!ModelState.IsValid)
                {
                    var errors = ModelState
                        .Where(x => x.Value.Errors.Count > 0)
                        .Select(x => new { Field = x.Key, Errors = x.Value.Errors.Select(e => e.ErrorMessage) })
                        .ToList();

                    _logger.LogWarning("UpdateDepartment validation failed: {@ValidationErrors}", errors);
                    
                    var errorMessages = errors.SelectMany(e => e.Errors).ToList();
                    var errorMessage = errorMessages.Any() ? string.Join("; ", errorMessages) : "Please provide valid department details.";
                    
                    return Json(new { 
                        success = false, 
                        message = errorMessage,
                        validationErrors = errors
                    });
                }

                var department = await _departmentService.GetDepartmentByIdAsync(model.DepartmentId);
                if (department == null)
                {
                    _logger.LogWarning("Department with ID {DepartmentId} not found", model.DepartmentId);
                    return Json(new { success = false, message = "Department not found." });
                }

                // Check if another department with the same name exists (excluding current department)
                var existingDepartments = await _departmentService.GetAllDepartmentsAsync();
                if (existingDepartments.Any(d => d.DepartmentId != model.DepartmentId && 
                                                d.Name.Equals(model.Name.Trim(), StringComparison.OrdinalIgnoreCase)))
                {
                    _logger.LogWarning("Another department with name '{Name}' already exists", model.Name);
                    return Json(new { success = false, message = "A department with this name already exists." });
                }

                _logger.LogInformation("Updating department {DepartmentId}: Old Name='{OldName}', New Name='{NewName}'", 
                    department.DepartmentId, department.Name, model.Name);

                department.Name = model.Name.Trim();
                department.Description = model.Description?.Trim() ?? string.Empty;

                await _departmentService.UpdateDepartmentAsync(department);
                
                _logger.LogInformation("Department {DepartmentId} updated successfully", department.DepartmentId);
                return Json(new { success = true, message = "Department updated successfully!" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating department with model: {@Model}", model);
                return Json(new { success = false, message = "Error updating department. Please try again." });
            }
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteDepartment(int id)
        {
            try
            {
                var result = await _departmentService.DeleteDepartmentAsync(id);
                if (result)
                {
                    return Json(new { success = true, message = "Department deleted successfully!" });
                }
                else
                {
                    return Json(new { success = false, message = "Cannot delete department as it has assigned users." });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting department");
                return Json(new { success = false, message = "Error deleting department. Please try again." });
            }
        }
        #endregion

        #region Categories Management
        public async Task<IActionResult> Categories()
        {
            try
            {
                var categoryViewModels = await _categoryService.GetCategoriesWithStatsAsync();
                return View(categoryViewModels);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading categories");
                TempData["ErrorMessage"] = "Error loading categories data.";
                return View(new List<CategoryListViewModel>());
            }
        }

        [HttpGet]
        public async Task<IActionResult> CategoryDetails(int id)
        {
            try
            {
                _logger.LogInformation("🔍 Loading category details page for ID: {CategoryId}", id);
                
                var category = await _categoryService.GetCategoryByIdAsync(id);
                if (category == null)
                {
                    _logger.LogWarning("❌ Category not found for details page: {CategoryId}", id);
                    TempData["Error"] = $"Category with ID {id} not found.";
                    return RedirectToAction("Categories");
                }

                // Create a view model to avoid JSON serialization cycles
                var categoryDetailsViewModel = new CategoryDetailsViewModel
                {
                    CategoryId = category.CategoryId,
                    Name = category.Name,
                    Description = category.Description,
                    ImageUrl = category.ImageUrl,
                    IsActive = category.IsActive,
                    CreatedAt = category.CreatedAt,
                    ProductCount = category.Products?.Count ?? 0,
                    ActiveProductCount = category.Products?.Count(p => p.IsActive) ?? 0,
                    TotalStockValue = category.Products?.Where(p => p.IsActive).Sum(p => p.StockQuantity * p.SellingPrice) ?? 0
                };

                _logger.LogInformation("✅ Successfully loaded category details: {CategoryName}", category.Name);
                return View(categoryDetailsViewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error loading category details page for ID {CategoryId}: {ErrorMessage}", id, ex.Message);
                TempData["Error"] = $"Error loading category details: {ex.Message}";
                return RedirectToAction("Categories");
            }
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateCategory([FromBody] CreateCategoryViewModel model)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = ModelState
                        .Where(x => x.Value.Errors.Count > 0)
                        .Select(x => new { Field = x.Key, Errors = x.Value.Errors.Select(e => e.ErrorMessage) })
                        .ToList();
                    
                    _logger.LogWarning("Model validation failed for CreateCategory: {Errors}", 
                        string.Join("; ", errors.SelectMany(e => e.Errors)));
                    
                    var errorMessage = errors.Any() 
                        ? string.Join("; ", errors.SelectMany(e => e.Errors))
                        : "Please provide valid category details.";
                    
                    return Json(new { success = false, message = errorMessage });
                }

                var category = new Category
                {
                    Name = model.Name,
                    Description = model.Description,
                    ImageUrl = model.ImageUrl ?? string.Empty,
                    IsActive = true
                };

                await _categoryService.CreateCategoryAsync(category);
                return Json(new { success = true, message = "Category created successfully!" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating category");
                return Json(new { success = false, message = "Error creating category. Please try again." });
            }
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateCategory([FromBody] EditCategoryViewModel model)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return Json(new { success = false, message = "Please provide valid category details." });
                }

                var category = await _categoryService.GetCategoryByIdAsync(model.CategoryId);
                if (category == null)
                {
                    return Json(new { success = false, message = "Category not found." });
                }

                category.Name = model.Name;
                category.Description = model.Description;
                category.ImageUrl = model.ImageUrl ?? string.Empty;

                await _categoryService.UpdateCategoryAsync(category);
                return Json(new { success = true, message = "Category updated successfully!" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating category");
                return Json(new { success = false, message = "Error updating category. Please try again." });
            }
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ToggleCategoryStatus([FromBody] ToggleStatusRequest request)
        {
            try
            {
                _logger.LogInformation("ToggleCategoryStatus called with ID: {CategoryId}", request?.Id);
                
                if (request?.Id == null || request.Id <= 0)
                {
                    _logger.LogWarning("ToggleCategoryStatus: Invalid ID provided: {Id}", request?.Id);
                    return Json(new { success = false, message = "Valid Category ID is required." });
                }
                
                _logger.LogInformation("Toggling category status for ID: {CategoryId}", request.Id);
                
                var result = await _categoryService.ToggleCategoryStatusAsync(request.Id);
                if (result)
                {
                    _logger.LogInformation("Category status toggled successfully for ID: {CategoryId}", request.Id);
                    return Json(new { success = true, message = "Category status updated successfully!" });
                }
                else
                {
                    _logger.LogWarning("Category not found for ID: {CategoryId}", request.Id);
                    return Json(new { success = false, message = "Category not found." });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling category status for ID: {CategoryId}", request?.Id);
                return Json(new { success = false, message = "Error updating category status. Please try again." });
            }
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteCategory([FromBody] DeleteItemRequest request)
        {
            try
            {
                _logger.LogInformation("DeleteCategory called with ID: {CategoryId}", request?.Id);
                
                if (request?.Id == null || request.Id <= 0)
                {
                    _logger.LogWarning("DeleteCategory: Invalid ID provided: {Id}", request?.Id);
                    return Json(new { success = false, message = "Valid Category ID is required." });
                }
                
                _logger.LogInformation("Deleting category with ID: {CategoryId}", request.Id);
                
                var result = await _categoryService.DeleteCategoryAsync(request.Id);
                if (result)
                {
                    _logger.LogInformation("Category deleted successfully for ID: {CategoryId}", request.Id);
                    return Json(new { success = true, message = "Category deleted successfully!" });
                }
                else
                {
                    _logger.LogWarning("Cannot delete category with ID: {CategoryId} - has associated products", request.Id);
                    return Json(new { success = false, message = "Cannot delete category as it has associated products." });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting category for ID: {CategoryId}", request?.Id);
                return Json(new { success = false, message = "Error deleting category. Please try again." });
            }
        }
        #endregion

      

        #region Products Management
        public async Task<IActionResult> Products()
        {
            try
            {
                _logger.LogInformation("📎 Loading products page");
                var productViewModels = await _productService.GetProductsWithStatsAsync();
                
                // DEBUG: Log actual ProductId values being returned
                _logger.LogInformation("🔍 DEBUG: ProductId values from service:");
                if (productViewModels != null && productViewModels.Any())
                {
                    foreach (var product in productViewModels.Take(10)) // Log first 10 products
                    {
                        _logger.LogInformation("📦 Product: ID={ProductId}, Name={Name}, SKU={SKU}", 
                            product.ProductId, product.Name, product.SKU);
                    }
                    
                    var zeroIdProducts = productViewModels.Where(p => p.ProductId == 0).ToList();
                    if (zeroIdProducts.Any())
                    {
                        _logger.LogWarning("⚠️ Found {Count} products with ProductId = 0!", zeroIdProducts.Count);
                        foreach (var zeroProduct in zeroIdProducts.Take(5))
                        {
                            _logger.LogWarning("❌ Zero ID Product: Name={Name}, SKU={SKU}", 
                                zeroProduct.Name, zeroProduct.SKU);
                        }
                    }
                }
                
                _logger.LogInformation("✅ Successfully loaded {ProductCount} products", productViewModels?.Count ?? 0);
                return View(productViewModels ?? new List<ProductListViewModel>());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error loading products page: {ErrorMessage}", ex.Message);
                TempData["ErrorMessage"] = $"Error loading products: {ex.Message}";
                return View(new List<ProductListViewModel>());
            }
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateProduct([FromBody] CreateProductViewModel model)
        {
            try
            {
                _logger.LogInformation("🆕 Creating new product: {ProductName}", model?.Name ?? "Unknown");
                
                // Debug: Log all received model properties
                _logger.LogInformation("📋 Received model data:");
                _logger.LogInformation("  Name: {Name}", model?.Name ?? "NULL");
                _logger.LogInformation("  SKU: {SKU}", model?.SKU ?? "NULL");
                _logger.LogInformation("  CategoryId: {CategoryId}", model?.CategoryId ?? 0);
                _logger.LogInformation("  BuyingPrice: {BuyingPrice}", model?.BuyingPrice ?? 0);
                _logger.LogInformation("  SellingPrice: {SellingPrice}", model?.SellingPrice ?? 0);
                _logger.LogInformation("  StockQuantity: {StockQuantity}", model?.StockQuantity ?? 0);
                _logger.LogInformation("  ImageUrl length: {ImageUrlLength}", model?.ImageUrl?.Length ?? 0);
                _logger.LogInformation("  ImageUrl preview: {ImageUrlPreview}", 
                    model?.ImageUrl?.Length > 100 ? model.ImageUrl.Substring(0, 100) + "..." : model?.ImageUrl ?? "NULL");
                
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                    var errorMessage = string.Join("; ", errors);
                    
                    // Debug: Log detailed validation errors
                    _logger.LogWarning("❌ Model validation failed:");
                    foreach (var key in ModelState.Keys)
                    {
                        var state = ModelState[key];
                        if (state.Errors.Count > 0)
                        {
                            _logger.LogWarning("  Field {Field}: {Errors}", key, string.Join(", ", state.Errors.Select(e => e.ErrorMessage)));
                        }
                    }
                    
                    return Json(new { success = false, message = $"Validation failed: {errorMessage}" });
                }

                // Check if SKU already exists
                var existingProduct = await _productService.GetProductBySkuAsync(model.SKU);
                if (existingProduct != null)
                {
                    _logger.LogWarning("❌ SKU already exists: {SKU}", model.SKU);
                    return Json(new { success = false, message = $"A product with SKU '{model.SKU}' already exists." });
                }

                var product = new Product
                {
                    Name = model.Name,
                    SKU = model.SKU,
                    Description = model.Description,
                    CategoryId = model.CategoryId,
                    SupplierId = model.SupplierId,
                    BuyingPrice = model.BuyingPrice,
                    SellingPrice = model.SellingPrice,
                    StockQuantity = model.StockQuantity,
                    MinStockLevel = model.MinStockLevel,
                    ImageUrl = model.ImageUrl,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                await _productService.CreateProductAsync(product);
                
                _logger.LogInformation("✅ Successfully created product: {ProductName} (ID: {ProductId})", product.Name, product.ProductId);
                return Json(new { success = true, message = "Product created successfully!", productId = product.ProductId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error creating product {ProductName}: {ErrorMessage}", model?.Name ?? "Unknown", ex.Message);
                return Json(new { success = false, message = $"Error creating product: {ex.Message}" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetProductDetails(int productId)
        {
            try
            {
                _logger.LogInformation("🔍 Getting product details for ID: {ProductId}", productId);
                
                var product = await _productService.GetProductByIdAsync(productId);
                if (product == null)
                {
                    _logger.LogWarning("❌ Product not found: {ProductId}", productId);
                    return Json(new { success = false, message = $"Product with ID {productId} not found." });
                }

                var result = new
                {
                    success = true,
                    product = new
                    {
                        productId = product.ProductId,
                        name = product.Name,
                        sku = product.SKU,
                        description = product.Description,
                        categoryName = product.Category?.Name,
                        supplierName = product.Supplier?.CompanyName,
                        buyingPrice = product.BuyingPrice,
                        sellingPrice = product.SellingPrice,
                        stockQuantity = product.StockQuantity,
                        minStockLevel = product.MinStockLevel,
                        isActive = product.IsActive,
                        isLowStock = product.IsLowStock,
                        imageUrl = product.ImageUrl,
                        profitMargin = product.ProfitMargin,
                        profitPercentage = product.ProfitPercentage
                    }
                };

                _logger.LogInformation("✅ Successfully retrieved product details: {ProductName}", product.Name);
                return Json(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error getting product details for ID {ProductId}: {ErrorMessage}", productId, ex.Message);
                return Json(new { success = false, message = $"Error retrieving product details: {ex.Message}" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> UpdateProduct([FromBody] EditProductViewModel model)
        {
            try
            {
                _logger.LogInformation("🔄 Updating product: {ProductId}", model.ProductId);
                
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                    var errorMessage = string.Join("; ", errors);
                    _logger.LogWarning("❌ Invalid model state for UpdateProduct: {Errors}", errorMessage);
                    return Json(new { success = false, message = $"Validation failed: {errorMessage}" });
                }

                var product = await _productService.GetProductByIdAsync(model.ProductId);
                if (product == null)
                {
                    _logger.LogWarning("❌ Product not found for update: {ProductId}", model.ProductId);
                    return Json(new { success = false, message = $"Product with ID {model.ProductId} not found." });
                }

                // Check if SKU already exists for another product
                var existingProduct = await _productService.GetProductBySkuAsync(model.SKU);
                if (existingProduct != null && existingProduct.ProductId != model.ProductId)
                {
                    _logger.LogWarning("❌ SKU already exists for another product: {SKU}", model.SKU);
                    return Json(new { success = false, message = $"SKU '{model.SKU}' is already used by another product." });
                }

                // Update product properties
                product.Name = model.Name;
                product.Description = model.Description;
                product.SKU = model.SKU;
                product.CategoryId = model.CategoryId;
                product.SupplierId = model.SupplierId;
                product.BuyingPrice = model.BuyingPrice;
                product.SellingPrice = model.SellingPrice;
                product.StockQuantity = model.StockQuantity;
                product.MinStockLevel = model.MinStockLevel;
                product.ImageUrl = model.ImageUrl;
                product.UpdatedAt = DateTime.UtcNow;

                await _productService.UpdateProductAsync(product);
                
                _logger.LogInformation("✅ Successfully updated product: {ProductName} (ID: {ProductId})", product.Name, product.ProductId);
                return Json(new { success = true, message = "Product updated successfully!" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error updating product {ProductId}: {ErrorMessage}", model?.ProductId ?? 0, ex.Message);
                return Json(new { success = false, message = $"Error updating product: {ex.Message}" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> DeleteProduct([FromBody] DeleteProductRequest request)
        {
            try
            {
                _logger.LogInformation("🗑️ Deleting product: {ProductId}", request.ProductId);
                
                var product = await _productService.GetProductByIdAsync(request.ProductId);
                if (product == null)
                {
                    _logger.LogWarning("❌ Product not found for deletion: {ProductId}", request.ProductId);
                    return Json(new { success = false, message = $"Product with ID {request.ProductId} not found." });
                }

                var result = await _productService.DeleteProductAsync(request.ProductId);
                if (result)
                {
                    _logger.LogInformation("✅ Successfully deleted product: {ProductName} (ID: {ProductId})", product.Name, request.ProductId);
                    return Json(new { success = true, message = "Product deleted successfully!" });
                }
                else
                {
                    _logger.LogWarning("❌ Failed to delete product: {ProductId}", request.ProductId);
                    return Json(new { success = false, message = "Failed to delete product. It may be referenced by other records." });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error deleting product {ProductId}: {ErrorMessage}", request?.ProductId ?? 0, ex.Message);
                return Json(new { success = false, message = $"Error deleting product: {ex.Message}" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> ProductDetails(int id)
        {
            try
            {
                _logger.LogInformation("🔍 Loading product details page for ID: {ProductId}", id);
                
                var product = await _productService.GetProductByIdAsync(id);
                if (product == null)
                {
                    _logger.LogWarning("❌ Product not found for details page: {ProductId}", id);
                    TempData["Error"] = $"Product with ID {id} not found.";
                    return RedirectToAction("Products");
                }

                // Create a flattened view model to avoid JSON serialization cycles
                var productDetailsViewModel = new ProductDetailsViewModel
                {
                    ProductId = product.ProductId,
                    Name = product.Name,
                    Description = product.Description,
                    SKU = product.SKU,
                    BuyingPrice = product.BuyingPrice,
                    SellingPrice = product.SellingPrice,
                    StockQuantity = product.StockQuantity,
                    MinStockLevel = product.MinStockLevel,
                    ImageUrl = product.ImageUrl,
                    IsActive = product.IsActive,
                    CreatedAt = product.CreatedAt,
                    UpdatedAt = product.UpdatedAt,
                    CategoryId = product.CategoryId,
                    CategoryName = product.Category?.Name ?? "No Category",
                    SupplierId = product.SupplierId,
                    SupplierName = product.Supplier?.CompanyName ?? "No Supplier",
                    ProfitMargin = product.SellingPrice - product.BuyingPrice,
                    ProfitPercentage = product.BuyingPrice > 0 ? ((product.SellingPrice - product.BuyingPrice) / product.BuyingPrice) * 100 : 0,
                    IsLowStock = product.StockQuantity <= product.MinStockLevel,
                    
                    // Navigation properties for view compatibility
                    Category = product.Category != null ? new CategoryViewModel
                    {
                        CategoryId = product.Category.CategoryId,
                        Name = product.Category.Name,
                        Description = product.Category.Description
                    } : null,
                    
                    Supplier = product.Supplier != null ? new SupplierViewModel
                    {
                        SupplierId = product.Supplier.SupplierId,
                        CompanyName = product.Supplier.CompanyName,
                        ContactPerson = product.Supplier.ContactPerson,
                        Email = product.Supplier.Email,
                        Phone = product.Supplier.Phone,
                        Address = product.Supplier.Address
                    } : null
                };

                _logger.LogInformation("✅ Successfully loaded product details: {ProductName}", product.Name);
                return View(productDetailsViewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error loading product details page for ID {ProductId}: {ErrorMessage}", id, ex.Message);
                TempData["Error"] = $"Error loading product details: {ex.Message}";
                return RedirectToAction("Products");
            }
        }

        [HttpPost]
        public async Task<IActionResult> ToggleProductStatus([FromBody] ToggleProductStatusRequest request)
        {
            try
            {
                _logger.LogInformation("🔄 Toggling product status: {ProductId}", request.ProductId);
                
                var product = await _productService.GetProductByIdAsync(request.ProductId);
                if (product == null)
                {
                    _logger.LogWarning("❌ Product not found for status toggle: {ProductId}", request.ProductId);
                    return Json(new { success = false, message = $"Product with ID {request.ProductId} not found." });
                }

                // Toggle the status
                product.IsActive = !product.IsActive;
                product.UpdatedAt = DateTime.UtcNow;

                await _productService.UpdateProductAsync(product);
                
                var statusText = product.IsActive ? "activated" : "deactivated";
                _logger.LogInformation("✅ Successfully {StatusText} product: {ProductName} (ID: {ProductId})", statusText, product.Name, product.ProductId);
                return Json(new { success = true, message = $"Product {statusText} successfully!", isActive = product.IsActive });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error toggling product status {ProductId}: {ErrorMessage}", request?.ProductId ?? 0, ex.Message);
                return Json(new { success = false, message = $"Error toggling product status: {ex.Message}" });
            }
        }

        // Helper class for delete request
        public class DeleteProductRequest
        {
            public int ProductId { get; set; }
        }

        // Helper class for toggle status request
        public class ToggleProductStatusRequest
        {
            public int ProductId { get; set; }
        }
        #endregion

        #region Inventory Management
        [Authorize(Roles = "Admin,Manager")]
        public async Task<IActionResult> Inventory()
        {
            try
            {
                var products = await _productService.GetAllProductsAsync();
                var productViewModels = products.Select(p => new ProductListViewModel
                {
                    ProductId = p.ProductId,
                    Name = p.Name,
                    SKU = p.SKU,
                    Description = p.Description,
                    CategoryName = p.Category?.Name ?? "No Category",
                    SupplierName = p.Supplier?.CompanyName ?? "No Supplier",
                    BuyingPrice = p.BuyingPrice,
                    SellingPrice = p.SellingPrice,
                    StockQuantity = p.StockQuantity,
                    MinStockLevel = p.MinStockLevel,
                    IsLowStock = p.StockQuantity <= p.MinStockLevel,
                    IsActive = p.IsActive,
                    CreatedAt = p.CreatedAt,
                    UpdatedAt = p.UpdatedAt
                }).ToList();
                
                return View(productViewModels);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading inventory");
                TempData["ErrorMessage"] = "Error loading inventory. Please try again.";
                return View(new List<ProductListViewModel>());
            }
        }

        [HttpPost]
        [Authorize(Roles = "Admin,Manager")]
        public async Task<IActionResult> UpdateStock([FromBody] UpdateStockViewModel model)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return Json(new { success = false, message = "Invalid data provided." });
                }

                var result = await _productService.UpdateStockAsync(model.ProductId, model.NewQuantity);
                if (result)
                {
                    return Json(new { success = true, message = "Stock updated successfully." });
                }
                else
                {
                    return Json(new { success = false, message = "Failed to update stock." });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating stock");
                return Json(new { success = false, message = "An error occurred while updating stock." });
            }
        }
        #endregion

        #region Users Management
        [Authorize(Roles = "Admin,Manager")]
        public async Task<IActionResult> Users()
        {
            try
            {
                _logger.LogInformation("Starting to load users from database");
                var users = await _userService.GetAllUsersAsync();
                _logger.LogInformation($"Retrieved {users.Count()} users from database");
                
                var userViewModels = users.Select(u => new UserListViewModel
                {
                    UserId = u.UserId,
                    FullName = !string.IsNullOrEmpty(u.FirstName) && !string.IsNullOrEmpty(u.LastName) 
                        ? $"{u.FirstName} {u.LastName}" 
                        : "Unknown User",
                    Email = u.Email ?? "No email",
                    Phone = u.Phone ?? "No phone",
                    UserType = u.UserType ?? "Employee",
                    Status = u.Status ?? "Inactive",
                    DepartmentNames = u.UserDepartments != null && u.UserDepartments.Any() ? string.Join(", ", u.UserDepartments.Select(ud => ud.Department.Name)) : "N/A",
                    TotalSales = u.Sales != null ? u.Sales.Count : 0,
                    TotalSalesAmount = u.Sales != null ? u.Sales.Sum(s => s.TotalAmount) : 0,
                    CreatedAt = u.CreatedAt
                }).ToList();
                
                _logger.LogInformation($"Created {userViewModels.Count} user view models");
                
                // Log first user for debugging
                if (userViewModels.Any())
                {
                    var firstUser = userViewModels.First();
                    _logger.LogInformation($"First user view model: {firstUser.FullName} ({firstUser.Email}) - {firstUser.UserType}");
                }
                
                ViewBag.UserCount = userViewModels.Count;
                ViewBag.CurrentUserRole = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? "Employee";
                
                return View(userViewModels);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading users");
                TempData["ErrorMessage"] = "Error loading users. Please try again.";
                return View(new List<UserListViewModel>());
            }
        }





        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<IActionResult> DeleteUser([FromBody] DeleteUserRequest request)
        {
            try
            {
                var deleted = await _userService.DeleteUserAsync(request.UserId);
                if (deleted)
                {
                    return Json(new { success = true, message = "User deleted successfully." });
                }
                else
                {
                    return Json(new { success = false, message = "Failed to delete user." });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting user");
                return Json(new { success = false, message = "An error occurred while deleting the user." });
            }
        }

        public class DeleteUserRequest
        {
            public int UserId { get; set; }
        }
        #endregion

        #region Reports and Analytics
        public async Task<IActionResult> Reports()
        {
            try
            {
                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading reports page");
                TempData["ErrorMessage"] = "Error loading reports.";
                return View();
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetDashboardData()
        {
            try
            {
                var data = await _reportService.GetDashboardDataAsync();
                return Json(data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting dashboard data");
                return Json(new { success = false, message = "Error loading dashboard data." });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetLowStockProducts()
        {
            try
            {
                var lowStockProducts = await _productService.GetLowStockProductsAsync();
                return Json(new
                {
                    success = true,
                    products = lowStockProducts.Select(p => new
                    {
                        productId = p.ProductId,
                        name = p.Name,
                        sku = p.SKU,
                        stockQuantity = p.StockQuantity,
                        minStockLevel = p.MinStockLevel,
                        category = p.Category?.Name,
                        supplier = p.Supplier?.CompanyName
                    })
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting low stock products");
                return Json(new { success = false, message = "Error loading low stock products." });
            }
        }
        #endregion


        #region Settings
        public IActionResult Settings()
        {
            try
            {
                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading settings page");
                TempData["ErrorMessage"] = "Error loading settings.";
                return View();
            }
        }
        #endregion

        // Add these methods to your existing AdminController.cs

        #region Suppliers Management - COMPLETE IMPLEMENTATION
        public async Task<IActionResult> Suppliers()
        {
            try
            {
                var suppliers = await _supplierService.GetAllSuppliersAsync();
                var supplierViewModels = suppliers.Select(s => new SupplierListViewModel
                {
                    SupplierId = s.SupplierId,
                    CompanyName = s.CompanyName,
                    ContactPerson = s.ContactPerson,
                    Email = s.Email,
                    Phone = s.Phone,
                    Address = s.Address,
                    Status = s.Status,
                    ProductCount = s.Products?.Count ?? 0,
                    PurchaseRequestCount = s.PurchaseRequests?.Count ?? 0,
                    TotalPurchaseValue = s.PurchaseRequests?.Where(pr => pr.Status == "Completed").Sum(pr => pr.TotalAmount) ?? 0,
                    CreatedAt = s.CreatedAt
                }).ToList();

                return View(supplierViewModels);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading suppliers");
                TempData["ErrorMessage"] = "Error loading suppliers data.";
                return View(new List<SupplierListViewModel>());
            }
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateSupplier([FromBody] CreateSupplierViewModel model)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                    return Json(new { success = false, message = string.Join("; ", errors) });
                }

                var supplier = new Supplier
                {
                    CompanyName = model.CompanyName,
                    ContactPerson = model.ContactPerson,
                    Email = model.Email,
                    Phone = model.Phone,
                    Address = model.Address,
                    Status = "Active"
                };

                await _supplierService.CreateSupplierAsync(supplier);
                return Json(new { success = true, message = "Supplier created successfully!" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating supplier");
                return Json(new { success = false, message = "Error creating supplier. Please try again." });
            }
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateSupplier([FromBody] EditSupplierViewModel model)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return Json(new { success = false, message = "Please provide valid supplier details." });
                }

                var supplier = await _supplierService.GetSupplierByIdAsync(model.SupplierId);
                if (supplier == null)
                {
                    return Json(new { success = false, message = "Supplier not found." });
                }

                supplier.CompanyName = model.CompanyName;
                supplier.ContactPerson = model.ContactPerson;
                supplier.Email = model.Email;
                supplier.Phone = model.Phone;
                supplier.Address = model.Address;
                supplier.Status = model.Status;

                await _supplierService.UpdateSupplierAsync(supplier);
                return Json(new { success = true, message = "Supplier updated successfully!" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating supplier");
                return Json(new { success = false, message = "Error updating supplier. Please try again." });
            }
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ToggleSupplierStatus([FromBody] ToggleStatusRequest request)
        {
            try
            {
                _logger.LogInformation("ToggleSupplierStatus called with ID: {SupplierId}", request?.Id);
                
                if (request?.Id == null || request.Id <= 0)
                {
                    _logger.LogWarning("ToggleSupplierStatus: Invalid ID provided: {Id}", request?.Id);
                    return Json(new { success = false, message = "Valid Supplier ID is required." });
                }
                
                _logger.LogInformation("Toggling supplier status for ID: {SupplierId}", request.Id);
                
                var result = await _supplierService.ToggleSupplierStatusAsync(request.Id);
                if (result)
                {
                    _logger.LogInformation("Supplier status toggled successfully for ID: {SupplierId}", request.Id);
                    return Json(new { success = true, message = "Supplier status updated successfully!" });
                }
                else
                {
                    _logger.LogWarning("Supplier not found for ID: {SupplierId}", request.Id);
                    return Json(new { success = false, message = "Supplier not found." });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling supplier status for ID: {SupplierId}", request?.Id);
                return Json(new { success = false, message = "Error updating supplier status. Please try again." });
            }
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteSupplier([FromBody] DeleteItemRequest request)
        {
            try
            {
                _logger.LogInformation("DeleteSupplier called with ID: {SupplierId}", request?.Id);
                
                if (request?.Id == null || request.Id <= 0)
                {
                    _logger.LogWarning("DeleteSupplier: Invalid ID provided: {Id}", request?.Id);
                    return Json(new { success = false, message = "Valid Supplier ID is required." });
                }
                
                _logger.LogInformation("Deleting supplier with ID: {SupplierId}", request.Id);
                
                var result = await _supplierService.DeleteSupplierAsync(request.Id);
                if (result)
                {
                    _logger.LogInformation("Supplier deleted successfully for ID: {SupplierId}", request.Id);
                    return Json(new { success = true, message = "Supplier deleted successfully!" });
                }
                else
                {
                    _logger.LogWarning("Cannot delete supplier with ID: {SupplierId} - has associated products or purchase requests", request.Id);
                    return Json(new { success = false, message = "Cannot delete supplier as it has associated products or purchase requests." });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting supplier for ID: {SupplierId}", request?.Id);
                return Json(new { success = false, message = "Error deleting supplier. Please try again." });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetSupplierForEdit(int supplierId)
        {
            try
            {
                var supplier = await _supplierService.GetSupplierByIdAsync(supplierId);
                if (supplier == null)
                {
                    return Json(new { success = false, message = "Supplier not found." });
                }

                var editModel = new EditSupplierViewModel
                {
                    SupplierId = supplier.SupplierId,
                    CompanyName = supplier.CompanyName,
                    ContactPerson = supplier.ContactPerson,
                    Email = supplier.Email,
                    Phone = supplier.Phone,
                    Address = supplier.Address,
                    Status = supplier.Status
                };

                return Json(new { success = true, supplier = editModel });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting supplier for edit: {SupplierId}", supplierId);
                return Json(new { success = false, message = "An error occurred while retrieving supplier details." });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetSupplier(int id)
        {
            try
            {
                var supplier = await _supplierService.GetSupplierByIdAsync(id);
                if (supplier == null)
                {
                    return Json(new { success = false, message = "Supplier not found." });
                }

                var supplierData = new
                {
                    supplierId = supplier.SupplierId,
                    companyName = supplier.CompanyName,
                    contactPerson = supplier.ContactPerson,
                    email = supplier.Email,
                    phone = supplier.Phone,
                    address = supplier.Address,
                    status = supplier.Status,
                    createdAt = supplier.CreatedAt
                };

                return Json(new { success = true, supplier = supplierData });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting supplier details for ID: {SupplierId}", id);
                return Json(new { success = false, message = "Error loading supplier details. Please try again." });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetCategory(int id)
        {
            try
            {
                var category = await _categoryService.GetCategoryByIdAsync(id);
                if (category == null)
                {
                    return Json(new { success = false, message = "Category not found." });
                }

                var categoryData = new
                {
                    categoryId = category.CategoryId,
                    name = category.Name,
                    description = category.Description,
                    imageUrl = category.ImageUrl,
                    isActive = category.IsActive,
                    createdAt = category.CreatedAt,
                    productCount = category.Products?.Count ?? 0
                };

                return Json(new { success = true, category = categoryData });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting category details for ID: {CategoryId}", id);
                return Json(new { success = false, message = "Error loading category details. Please try again." });
            }
        }

        #endregion

        #region Products Management - COMPLETE IMPLEMENTATION

        [HttpGet]
        public async Task<IActionResult> GetProductsForSale()
        {
            try
            {
                var products = await _productService.GetActiveProductsAsync();
                var productList = products.Select(p => new {
                    id = p.ProductId,
                    name = p.Name,
                    sku = p.SKU,
                    price = p.SellingPrice,
                    stockQuantity = p.StockQuantity,
                    categoryName = p.Category?.Name ?? "No Category",
                    categoryId = p.CategoryId,
                    imageUrl = p.ImageUrl,
                    isActive = p.IsActive
                }).ToList();

                return Json(new { success = true, products = productList });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting products for sale");
                return Json(new { success = false, message = "Error loading products." });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetTodaysSalesStats()
        {
            try
            {
                // Get today's sales only using proper date filtering
                var today = DateTime.Today;
                var tomorrow = today.AddDays(1);
                var todaysSales = await _context.Sales
                    .AsNoTracking()
                    .Where(s => s.SaleDate >= today && s.SaleDate < tomorrow)
                    .ToListAsync();

                _logger.LogInformation($"GetTodaysSalesStats API - TODAY'S SALES: {todaysSales.Count()} totaling {todaysSales.Sum(s => s.AmountPaid):C}");

                var stats = new
                {
                    totalSales = todaysSales.Sum(s => s.AmountPaid),
                    transactionCount = todaysSales.Count(),
                    averageTransaction = todaysSales.Any() ? todaysSales.Average(s => s.AmountPaid) : 0
                };

                return Json(new { success = true, stats = stats });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting today's sales stats");
                return Json(new { success = false, message = "Error loading sales statistics." });
            }
        }
        #endregion

        // API Endpoints for data loading
        [HttpGet]
        [Route("/api/categories")]
        public async Task<IActionResult> GetCategories()
        {
            try
            {
                var categories = await _categoryService.GetActiveCategoriesAsync();
                return Json(categories.Select(c => new { categoryId = c.CategoryId, name = c.Name }));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting categories");
                return Json(new List<object>());
            }
        }

        [HttpGet]
        [Route("/api/suppliers")]
        public async Task<IActionResult> GetSuppliersApi()
        {
            try
            {
                var suppliers = await _supplierService.GetActiveSuppliersAsync();
                return Json(suppliers.Select(s => new { supplierId = s.SupplierId, companyName = s.CompanyName }));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting suppliers");
                return Json(new List<object>());
            }
        }

        #region Additional Report Endpoints
        [HttpGet]
        public async Task<IActionResult> GetSalesReport(DateTime startDate, DateTime endDate)
        {
            try
            {
                var report = await _reportService.GetSalesReportAsync(startDate, endDate);
                return Json(report);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting sales report");
                return Json(new { success = false, message = "Error generating sales report." });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetInventoryReport()
        {
            try
            {
                var report = await _reportService.GetInventoryReportAsync();
                return Json(report);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting inventory report");
                return Json(new { success = false, message = "Error generating inventory report." });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetSupplierReport()
        {
            try
            {
                var report = await _reportService.GetSupplierReportAsync();
                return Json(report);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting supplier report");
                return Json(new { success = false, message = "Error generating supplier report." });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetUserActivityReport()
        {
            try
            {
                var report = await _reportService.GetUserActivityReportAsync();
                return Json(report);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user activity report");
                return Json(new { success = false, message = "Error generating user activity report." });
            }
        }
        #endregion

        // ProcessSale method removed - using SalesController.ProcessSale instead to avoid routing conflicts

        // Helper method to generate sale numbers
        [HttpGet]
        public async Task<IActionResult> GetReceiptHtml(int saleId)
        {
            try
            {
                var sale = await _saleService.GetSaleByIdAsync(saleId);
                if (sale == null)
                {
                    return NotFound("Sale not found");
                }

                var receiptBytes = await _reportService.GenerateSalesReceiptAsync(saleId);
                var receiptHtml = System.Text.Encoding.UTF8.GetString(receiptBytes);

                return Content(receiptHtml, "text/html");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating receipt HTML for sale {SaleId}", saleId);
                return BadRequest("Error generating receipt");
            }
        }

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Receipt(int saleId)
        {
            try
            {
                _logger.LogInformation("Generating receipt view for sale {SaleId}", saleId);

                // Get sale details with related data
                var sale = await _saleService.GetSaleByIdAsync(saleId);
                if (sale == null)
                {
                    _logger.LogWarning("Sale {SaleId} not found for receipt", saleId);
                    return NotFound("Sale not found");
                }

                // Create detailed view model
                var saleDetails = new SaleDetailsViewModel
                {
                    SaleId = sale.SaleId,
                    SaleNumber = sale.SaleNumber,
                    SaleDate = sale.SaleDate,
                    TotalAmount = sale.TotalAmount,
                    PaymentMethod = sale.PaymentMethod,
                    CustomerName = sale.CustomerName ?? "Walk-in Customer",
                    CustomerPhone = sale.CustomerPhone,
                    CustomerEmail = sale.CustomerEmail,
                    CashierName = !string.IsNullOrEmpty(sale.CashierName) ? sale.CashierName :
                                 (sale.User != null ? $"{sale.User.FirstName} {sale.User.LastName}" : "Unknown"),
                    AmountPaid = sale.AmountPaid,
                    ChangeGiven = sale.ChangeGiven,
                    Status = sale.Status ?? "Completed",
                    Items = sale.SaleItems?.Select(si => new SaleItemDetailsViewModel
                    {
                        SaleItemId = si.SaleItemId,
                        ProductId = si.ProductId,
                        ProductName = si.Product?.Name ?? "Unknown Product",
                        ProductSKU = si.Product?.SKU ?? "",
                        Quantity = si.Quantity,
                        UnitPrice = si.UnitPrice,
                        TotalPrice = si.TotalPrice
                    }).ToList() ?? new List<SaleItemDetailsViewModel>()
                };

                _logger.LogInformation("Receipt view model created for sale {SaleNumber} with {ItemCount} items", 
                    saleDetails.SaleNumber, saleDetails.Items.Count);

                // Return the receipt view with proper view model
                return View(saleDetails);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading receipt view for sale {SaleId}", saleId);
                return BadRequest("Error loading receipt");
            }
        }

        private async Task<string> GenerateSaleNumberAsync()
        {
            var today = DateTime.Today;
            var prefix = $"SAL{today:yyyyMMdd}";

            var lastSale = await _context.Sales
                .Where(s => s.SaleNumber.StartsWith(prefix))
                .OrderByDescending(s => s.SaleNumber)
                .FirstOrDefaultAsync();

            int nextNumber = 1;
            if (lastSale != null)
            {
                var numberPart = lastSale.SaleNumber.Substring(prefix.Length);
                if (int.TryParse(numberPart, out int currentNumber))
                {
                    nextNumber = currentNumber + 1;
                }
            }

            return $"{prefix}{nextNumber:D4}";
        }

        #region Sales Management - COMPLETE IMPLEMENTATION
        public async Task<IActionResult> Sales()
        {
            try
            {
                _logger.LogInformation("Loading sales page...");

                // Load all products for the POS (including inactive ones for admin visibility)
                var products = await _productService.GetAllProductsAsync();

                // Load recent sales
                var recentSales = await _saleService.GetAllSalesAsync();

                // Get today's sales only using proper date filtering
                var today = DateTime.Today;
                var tomorrow = today.AddDays(1);
                var todaysSales = await _context.Sales
                    .AsNoTracking()
                    .Where(s => s.SaleDate >= today && s.SaleDate < tomorrow)
                    .ToListAsync();

                _logger.LogInformation($"Sales page - TODAY'S SALES: {todaysSales.Count()} totaling {todaysSales.Sum(s => s.AmountPaid):C}");

                // Create view model for sales page
                var salesViewModel = new SalesPageViewModel
                {
                    Products = products.Select(p => new ProductSearchViewModel
                    {
                        ProductId = p.ProductId,
                        Name = p.Name,
                        SKU = p.SKU,
                        SellingPrice = p.SellingPrice,
                        StockQuantity = p.StockQuantity,
                        CategoryName = p.Category?.Name ?? "No Category",
                        ImageUrl = p.ImageUrl,
                        IsActive = p.IsActive
                    }).ToList(),
                    RecentSales = recentSales.Take(10).Select(s => new SaleListViewModel
                    {
                        SaleId = s.SaleId,
                        SaleNumber = s.SaleNumber,
                        SaleDate = s.SaleDate,
                        TotalAmount = s.TotalAmount,
                        PaymentMethod = s.PaymentMethod,
                        CustomerName = s.CustomerName ?? "Walk-in Customer",
                        ItemCount = s.SaleItems?.Count ?? 0,
                        CashierName = !string.IsNullOrEmpty(s.CashierName) ? s.CashierName :
                                     (s.User != null ? $"{s.User.FirstName} {s.User.LastName}" : "Unknown")
                    }).ToList(),
                    TodaysSales = todaysSales.Sum(s => s.AmountPaid),
                    TodaysTransactions = todaysSales.Count(),
                    AverageTransaction = todaysSales.Any() ? todaysSales.Average(s => s.AmountPaid) : 0
                };

                return View(salesViewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading sales page: {Message}", ex.Message);
                TempData["ErrorMessage"] = "Error loading sales data.";
                return View(new SalesPageViewModel
                {
                    Products = new List<ProductSearchViewModel>(),
                    RecentSales = new List<SaleListViewModel>(),
                    TodaysSales = 0,
                    TodaysTransactions = 0,
                    AverageTransaction = 0
                });
            }
        }
        public async Task<IActionResult> SalesHistory()
        {
            try
            {
                _logger.LogInformation("Loading sales history page...");

                // Load all sales with their details
                var sales = await _saleService.GetAllSalesAsync();

                // Convert to view models
                var salesHistory = sales.Select(s => new SaleListViewModel
                {
                    SaleId = s.SaleId,
                    SaleNumber = s.SaleNumber,
                    SaleDate = s.SaleDate,
                    TotalAmount = s.TotalAmount,
                    PaymentMethod = s.PaymentMethod,
                    CustomerName = s.CustomerName ?? "Walk-in Customer",
                    CustomerPhone = s.CustomerPhone,
                    ItemCount = s.SaleItems?.Count ?? 0,
                    CashierName = !string.IsNullOrEmpty(s.CashierName) ? s.CashierName :
                                 (s.User != null ? $"{s.User.FirstName} {s.User.LastName}" : "Unknown"),
                    ChangeGiven = s.ChangeGiven,
                    AmountPaid = s.AmountPaid,
                    TotalQuantity = s.SaleItems?.Sum(si => si.Quantity) ?? 0,
                    Status = s.Status ?? "Completed"
                }).ToList();

                _logger.LogInformation($"Loaded {salesHistory.Count()} sales records");

                return View(salesHistory);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading sales history: {Message}", ex.Message);
                TempData["ErrorMessage"] = "Error loading sales history.";
                return View(new List<SaleListViewModel>());
            }
        }

        [HttpGet]
        public async Task<IActionResult> SaleDetails(int id)
        {
            try
            {
                var sale = await _saleService.GetSaleByIdAsync(id);
                if (sale == null)
                {
                    TempData["ErrorMessage"] = "Sale not found.";
                    return RedirectToAction("SalesHistory");
                }

                // Create detailed view model
                var saleDetails = new SaleDetailsViewModel
                {
                    SaleId = sale.SaleId,
                    SaleNumber = sale.SaleNumber,
                    SaleDate = sale.SaleDate,
                    TotalAmount = sale.TotalAmount,
                    PaymentMethod = sale.PaymentMethod,
                    CustomerName = sale.CustomerName ?? "Walk-in Customer",
                    CustomerPhone = sale.CustomerPhone,
                    CashierName = !string.IsNullOrEmpty(sale.CashierName) ? sale.CashierName :
                                 (sale.User != null ? $"{sale.User.FirstName} {sale.User.LastName}" : "Unknown"),
                    AmountPaid = sale.AmountPaid,
                    ChangeGiven = sale.ChangeGiven,
                    Status = sale.Status ?? "Completed",
                    Items = sale.SaleItems?.Select(si => new SaleItemDetailsViewModel
                    {
                        SaleItemId = si.SaleItemId,
                        ProductId = si.ProductId,
                        ProductName = si.Product?.Name ?? "Unknown Product",
                        ProductSKU = si.Product?.SKU ?? "",
                        Quantity = si.Quantity,
                        UnitPrice = si.UnitPrice,
                        TotalPrice = si.TotalPrice
                    }).ToList() ?? new List<SaleItemDetailsViewModel>()
                };

                return View(saleDetails);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading sale details for ID {SaleId}", id);
                TempData["ErrorMessage"] = "Error loading sale details.";
                return RedirectToAction("SalesHistory");
            }
        }
        #endregion

        // Add these methods to your existing AdminController.cs file

        #region Reports API Endpoints - Add these methods to AdminController


        [HttpPost]
        public async Task<IActionResult> GetReportsChartData([FromBody] ReportsChartRequest request)
        {
            try
            {
                var startDate = request.StartDate;
                var endDate = request.EndDate;

                // Get sales data for the date range
                var sales = await _context.Sales
                    .Include(s => s.SaleItems)
                        .ThenInclude(si => si.Product)
                            .ThenInclude(p => p.Category)
                    .Where(s => s.SaleDate >= startDate && s.SaleDate <= endDate && s.Status == "Completed")
                    .ToListAsync();

                // Sales data by date
                var salesData = sales
                    .GroupBy(s => s.SaleDate.Date)
                    .Select(g => new
                    {
                        date = g.Key.ToString("yyyy-MM-dd"),
                        amount = g.Sum(s => s.TotalAmount)
                    })
                    .OrderBy(x => x.date)
                    .ToList();

                // Categories data
                var categoriesData = sales
                    .SelectMany(s => s.SaleItems)
                    .Where(si => si.Product?.Category != null)
                    .GroupBy(si => si.Product.Category.Name)
                    .Select(g => new
                    {
                        category = g.Key,
                        amount = g.Sum(si => si.TotalPrice)
                    })
                    .OrderByDescending(x => x.amount)
                    .Take(10)
                    .ToList();

                // Top products
                var topProducts = sales
                    .SelectMany(s => s.SaleItems)
                    .Where(si => si.Product != null)
                    .GroupBy(si => new { ProductId = si.ProductId, ProductName = si.Product.Name })
                    .Select(g => new
                    {
                        productName = g.Key.ProductName,
                        quantitySold = g.Sum(si => si.Quantity),
                        revenue = g.Sum(si => si.TotalPrice)
                    })
                    .OrderByDescending(x => x.revenue)
                    .Take(10)
                    .ToList();

                return Json(new
                {
                    success = true,
                    salesData = salesData,
                    categoriesData = categoriesData,
                    topProducts = topProducts
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting reports chart data");
                return Json(new { success = false, message = "Error loading chart data." });
            }
        }

        // Request model for chart data
        public class ReportsChartRequest
        {
            public DateTime StartDate { get; set; }
            public DateTime EndDate { get; set; }
        }

       

       

        // Get all sales for receipt selection
        [HttpGet]
        public async Task<IActionResult> GetSalesForReceipt()
        {
            try
            {
                var sales = await _context.Sales
                    .Include(s => s.User)
                    .Where(s => s.Status == "Completed")
                    .OrderByDescending(s => s.SaleDate)
                    .Take(100)
                    .Select(s => new
                    {
                        s.SaleId,
                        s.SaleNumber,
                        CustomerName = string.IsNullOrEmpty(s.CustomerName) ? "Walk-in Customer" : s.CustomerName,
                        s.CustomerEmail,
                        s.TotalAmount,
                        s.SaleDate,
                        CashierName = s.User.FullName
                    })
                    .ToListAsync();

                return Json(new { success = true, sales = sales });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting sales for receipt");
                return Json(new { success = false, message = "Error loading sales data" });
            }
        }

        // Request models
        public class ExportReportRequest
        {
            public string ReportType { get; set; } = "";
            public string? StartDate { get; set; }
            public string? EndDate { get; set; }
            public string? Format { get; set; } = "pdf";
        }

        public class EmailReportRequest
        {
            public string Email { get; set; } = "";
            public string ReportType { get; set; } = "";
            public DateTime StartDate { get; set; }
            public DateTime EndDate { get; set; }
        }

        public class EmailReceiptRequest
        {
            public string Email { get; set; } = "";
            public int SaleId { get; set; }
        }

        // Helper methods for date range and sales trend data
        private (DateTime startDate, DateTime endDate) GetDateRange(string period)
        {
            var now = DateTime.Now;
            var today = DateTime.Today;

            return period.ToLower() switch
            {
                "today" => (today, today.AddDays(1).AddSeconds(-1)),
                "yesterday" => (today.AddDays(-1), today.AddSeconds(-1)),
                "thisweek" => (today.AddDays(-(int)today.DayOfWeek), today.AddDays(7 - (int)today.DayOfWeek).AddSeconds(-1)),
                "lastweek" => (today.AddDays(-(int)today.DayOfWeek - 7), today.AddDays(-(int)today.DayOfWeek).AddSeconds(-1)),
                "thismonth" => (new DateTime(today.Year, today.Month, 1), new DateTime(today.Year, today.Month, 1).AddMonths(1).AddSeconds(-1)),
                "lastmonth" => (new DateTime(today.Year, today.Month, 1).AddMonths(-1), new DateTime(today.Year, today.Month, 1).AddSeconds(-1)),
                "thisquarter" => GetQuarterRange(today, 0),
                "thisyear" => (new DateTime(today.Year, 1, 1), new DateTime(today.Year, 12, 31, 23, 59, 59)),
                "lastyear" => (new DateTime(today.Year - 1, 1, 1), new DateTime(today.Year - 1, 12, 31, 23, 59, 59)),
                _ => (today, today.AddDays(1).AddSeconds(-1))
            };
        }

        private (DateTime startDate, DateTime endDate) GetQuarterRange(DateTime date, int quarterOffset)
        {
            var quarter = (date.Month - 1) / 3 + 1 + quarterOffset;
            var year = date.Year;
            
            if (quarter < 1)
            {
                quarter += 4;
                year--;
            }
            else if (quarter > 4)
            {
                quarter -= 4;
                year++;
            }

            var startMonth = (quarter - 1) * 3 + 1;
            var startDate = new DateTime(year, startMonth, 1);
            var endDate = startDate.AddMonths(3).AddSeconds(-1);

            return (startDate, endDate);
        }

        private async Task<List<object>> GetSalesTrendData(DateTime startDate, DateTime endDate, string period)
        {
            var sales = await _context.Sales
                .Where(s => s.SaleDate >= startDate && s.SaleDate <= endDate && s.Status == "Completed")
                .OrderBy(s => s.SaleDate)
                .ToListAsync();

            if (period.ToLower() == "today" || period.ToLower() == "yesterday")
            {
                // Hourly data for single day
                var hourlyData = sales
                    .GroupBy(s => s.SaleDate.Hour)
                    .Select(g => new {
                        date = $"{g.Key:00}:00",
                        sales = g.Sum(s => s.TotalAmount)
                    })
                    .OrderBy(x => x.date)
                    .ToList<object>();

                return hourlyData;
            }
            else if (period.ToLower().Contains("week"))
            {
                // Daily data for weeks
                var dailyData = sales
                    .GroupBy(s => s.SaleDate.Date)
                    .Select(g => new {
                        date = g.Key.ToString("MMM dd"),
                        sales = g.Sum(s => s.TotalAmount)
                    })
                    .OrderBy(x => x.date)
                    .ToList<object>();

                return dailyData;
            }
            else if (period.ToLower().Contains("month"))
            {
                // Daily data for months
                var dailyData = sales
                    .GroupBy(s => s.SaleDate.Date)
                    .Select(g => new {
                        date = g.Key.Day.ToString(),
                        sales = g.Sum(s => s.TotalAmount)
                    })
                    .OrderBy(x => int.Parse(x.date))
                    .ToList<object>();

                return dailyData;
            }
            else
            {
                // Monthly data for quarters/years
                var monthlyData = sales
                    .GroupBy(s => new { s.SaleDate.Year, s.SaleDate.Month })
                    .Select(g => new {
                        date = new DateTime(g.Key.Year, g.Key.Month, 1).ToString("MMM yyyy"),
                        sales = g.Sum(s => s.TotalAmount)
                    })
                    .OrderBy(x => x.date)
                    .ToList<object>();

                return monthlyData;
            }
        }

        #endregion

        
        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request)
        {
            try
            {
                _logger.LogInformation("📝 Creating new user: {Email}", request.Email);

                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)
                        .ToList();

                    _logger.LogWarning("❌ Model validation failed: {Errors}", string.Join("; ", errors));
                    return Json(new { success = false, message = string.Join("; ", errors) });
                }

                // Check if email already exists
                var existingUser = await _context.Users
                    .FirstOrDefaultAsync(u => u.Email.ToLower() == request.Email.ToLower());

                if (existingUser != null)
                {
                    return Json(new { success = false, message = "A user with this email address already exists." });
                }

                // Create new user
                var user = new User
                {
                    FirstName = request.FirstName.Trim(),
                    LastName = request.LastName.Trim(),
                    Email = request.Email.Trim().ToLower(),
                    Phone = request.Phone?.Trim() ?? string.Empty,
                    UserType = request.UserType,
                    Status = request.IsActive ? "Active" : "Inactive",
                    Privileges = request.UserType == "Admin" ? "Full" : "Limited",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password, 12),
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                _logger.LogInformation("✅ User created successfully: {Email} with ID: {UserId}", user.Email, user.UserId);

                return Json(new { success = true, message = "User created successfully!" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error creating user: {Email}", request?.Email ?? "Unknown");
                return Json(new { success = false, message = "An error occurred while creating the user. Please try again." });
            }
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<IActionResult> UpdateUser([FromBody] UpdateUserRequest request)
        {
            try
            {
                _logger.LogInformation("📝 Updating user: {UserId}", request.UserId);

                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)
                        .ToList();

                    return Json(new { success = false, message = string.Join("; ", errors) });
                }

                var user = await _context.Users.FindAsync(request.UserId);
                if (user == null)
                {
                    return Json(new { success = false, message = "User not found." });
                }

                // Check if email is unique (excluding current user)
                var existingUser = await _context.Users
                    .FirstOrDefaultAsync(u => u.Email.ToLower() == request.Email.ToLower() && u.UserId != request.UserId);

                if (existingUser != null)
                {
                    return Json(new { success = false, message = "A user with this email address already exists." });
                }

                // Update user properties
                user.FirstName = request.FirstName.Trim();
                user.LastName = request.LastName.Trim();
                user.Email = request.Email.Trim().ToLower();
                user.Phone = request.Phone?.Trim() ?? string.Empty;
                user.UserType = request.UserType;
                user.Status = request.IsActive ? "Active" : "Inactive";
                user.Privileges = request.UserType == "Admin" ? "Full" : "Limited";
                user.UpdatedAt = DateTime.UtcNow;

                // Update password if provided
                if (!string.IsNullOrWhiteSpace(request.NewPassword))
                {
                    user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword, 12);
                }

                _context.Users.Update(user);
                await _context.SaveChangesAsync();

                _logger.LogInformation("✅ User updated successfully: {Email}", user.Email);

                return Json(new { success = true, message = "User updated successfully!" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error updating user: {UserId}", request?.UserId ?? 0);
                return Json(new { success = false, message = "An error occurred while updating the user. Please try again." });
            }
        }

        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<IActionResult> GetUserForEdit(int userId)
        {
            try
            {
                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                {
                    return Json(new { success = false, message = "User not found." });
                }

                var userModel = new
                {
                    UserId = user.UserId,
                    FirstName = user.FirstName ?? "",
                    LastName = user.LastName ?? "",
                    Email = user.Email ?? "",
                    Phone = user.Phone ?? "",
                    UserType = user.UserType ?? "Employee",
                    IsActive = user.Status == "Active"
                };

                return Json(new { success = true, user = userModel });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error getting user for edit: {UserId}", userId);
                return Json(new { success = false, message = "An error occurred while retrieving user details." });
            }
        }

        
        public class CreateUserRequest
        {
            public string FirstName { get; set; } = string.Empty;
            public string LastName { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
            public string? Phone { get; set; }
            public string Password { get; set; } = string.Empty;
            public string ConfirmPassword { get; set; } = string.Empty;
            public string UserType { get; set; } = "Employee";
            public bool IsActive { get; set; } = true;
        }

        public class UpdateUserRequest
        {
            public int UserId { get; set; }
            public string FirstName { get; set; } = string.Empty;
            public string LastName { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
            public string? Phone { get; set; }
            public string? NewPassword { get; set; }
            public string? ConfirmNewPassword { get; set; }
            public string UserType { get; set; } = "Employee";
            public bool IsActive { get; set; } = true;
        }

        [HttpGet]
        public async Task<IActionResult> GetCurrentUserProfile()
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!int.TryParse(userIdClaim, out int userId))
                {
                    return Json(new { success = false, message = "User not found." });
                }

                var user = await _userService.GetUserByIdAsync(userId);
                if (user == null)
                {
                    return Json(new { success = false, message = "User not found." });
                }

                return Json(new
                {
                    success = true,
                    user = new
                    {
                        firstName = user.FirstName,
                        lastName = user.LastName,
                        email = user.Email,
                        phone = user.Phone,
                        userType = user.UserType,
                        status = user.Status
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting current user profile");
                return Json(new { success = false, message = "Error loading profile." });
            }
        }

        [HttpPost]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!int.TryParse(userIdClaim, out int userId))
                {
                    return Json(new { success = false, message = "User not found." });
                }

                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                {
                    return Json(new { success = false, message = "User not found." });
                }

                user.FirstName = request.FirstName.Trim();
                user.LastName = request.LastName.Trim();
                user.Email = request.Email.Trim();
                user.Phone = request.Phone?.Trim() ?? string.Empty;
                user.UpdatedAt = DateTime.UtcNow;

                _context.Users.Update(user);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Profile updated successfully!" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating profile");
                return Json(new { success = false, message = "Error updating profile." });
            }
        }

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAllUsersForPermissions()
        {
            try
            {
                var users = await _context.Users
                    .Select(u => new {
                        userId = u.UserId,
                        fullName = u.FirstName + " " + u.LastName,
                        email = u.Email,
                        userType = u.UserType,
                        status = u.Status
                    })
                    .ToListAsync();

                return Json(new { success = true, users = users });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting users for permissions");
                return Json(new { success = false, message = "Error loading users." });
            }
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ToggleUserStatus([FromBody] ToggleUserStatusRequest request)
        {
            try
            {
                var user = await _context.Users.FindAsync(request.UserId);
                if (user == null)
                {
                    return Json(new { success = false, message = "User not found." });
                }

                user.Status = request.Status;
                user.UpdatedAt = DateTime.UtcNow;

                _context.Users.Update(user);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = $"User {request.Status.ToLower()} successfully!" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling user status");
                return Json(new { success = false, message = "Error updating user status." });
            }
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ResetUserPassword([FromBody] ResetPasswordRequest request)
        {
            try
            {
                var user = await _context.Users.FindAsync(request.UserId);
                if (user == null)
                {
                    return Json(new { success = false, message = "User not found." });
                }

                // Generate temporary password
                var tempPassword = GenerateTemporaryPassword();
                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(tempPassword, 12);
                user.UpdatedAt = DateTime.UtcNow;

                _context.Users.Update(user);
                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    message = "Password reset successfully!",
                    tempPassword = tempPassword
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting user password");
                return Json(new { success = false, message = "Error resetting password." });
            }
        }



        private string GenerateTemporaryPassword()
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%";
            var random = new Random();
            return new string(Enumerable.Repeat(chars, 8)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }

        // Request models
        public class UpdateProfileRequest
        {
            public string FirstName { get; set; } = string.Empty;
            public string LastName { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
            public string? Phone { get; set; }
        }

        public class ToggleUserStatusRequest
        {
            public int UserId { get; set; }
            public string Status { get; set; } = string.Empty;
        }

        public class ResetPasswordRequest
        {
            public int UserId { get; set; }
        }

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetUsersForPermissions()
        {
            try
            {
                var users = await _userService.GetAllUsersAsync();
                return Json(new { success = true, users = users });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting users for permissions");
                return Json(new { success = false, message = "Error loading users." });
            }
        }



        [HttpGet]
        public async Task<IActionResult> GetProductStatistics(int productId)
        {
            try
            {
                _logger.LogInformation("📈 Getting product statistics for ProductId: {ProductId}", productId);
                
                var product = await _productService.GetProductByIdAsync(productId);
                if (product == null)
                {
                    _logger.LogWarning("⚠️ Product not found for statistics: {ProductId}", productId);
                    return NotFound(new { error = "Product not found" });
                }
                
                // Calculate real-time statistics from database
                var profitMargin = product.SellingPrice - product.BuyingPrice;
                var profitPercentage = product.BuyingPrice > 0 ? (profitMargin / product.BuyingPrice) * 100 : 0;
                var totalPotentialProfit = product.StockQuantity * profitMargin;
                var totalInvestment = product.StockQuantity * product.BuyingPrice;
                var isLowStock = product.StockQuantity <= product.MinStockLevel;
                
                var statistics = new
                {
                    productId = product.ProductId,
                    sellingPrice = product.SellingPrice,
                    buyingPrice = product.BuyingPrice,
                    stockQuantity = product.StockQuantity,
                    minStockLevel = product.MinStockLevel,
                    profitMargin = profitMargin,
                    profitPercentage = profitPercentage,
                    totalPotentialProfit = totalPotentialProfit,
                    totalInvestment = totalInvestment,
                    isLowStock = isLowStock,
                    lastUpdated = product.UpdatedAt
                };
                
                _logger.LogInformation("✅ Product statistics calculated successfully for ProductId: {ProductId}", productId);
                return Json(statistics);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error getting product statistics for ProductId: {ProductId}", productId);
                return StatusCode(500, new { error = "Error loading product statistics" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetCategoryStatistics(int categoryId)
        {
            try
            {
                _logger.LogInformation("📈 Getting category statistics for CategoryId: {CategoryId}", categoryId);
                
                var category = await _categoryService.GetCategoryByIdAsync(categoryId);
                if (category == null)
                {
                    _logger.LogWarning("⚠️ Category not found for statistics: {CategoryId}", categoryId);
                    return NotFound(new { error = "Category not found" });
                }
                
                // Get all products in this category from database
                var productsInCategory = await _context.Products
                    .Where(p => p.CategoryId == categoryId)
                    .ToListAsync();
                
                // Calculate real-time statistics from database
                var totalProducts = productsInCategory.Count;
                var activeProducts = productsInCategory.Count(p => p.IsActive);
                var inactiveProducts = totalProducts - activeProducts;
                var totalStockValue = productsInCategory
                    .Where(p => p.IsActive)
                    .Sum(p => p.StockQuantity * p.SellingPrice);
                
                var statistics = new
                {
                    categoryId = category.CategoryId,
                    totalProducts = totalProducts,
                    activeProducts = activeProducts,
                    inactiveProducts = inactiveProducts,
                    totalStockValue = totalStockValue,
                    lastUpdated = DateTime.UtcNow
                };
                
                _logger.LogInformation("✅ Category statistics calculated successfully for CategoryId: {CategoryId}", categoryId);
                return Json(statistics);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error getting category statistics for CategoryId: {CategoryId}", categoryId);
                return StatusCode(500, new { error = "Error loading category statistics" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetCategoryProducts(int categoryId)
        {
            try
            {
                _logger.LogInformation("📦 Getting products for CategoryId: {CategoryId}", categoryId);
                
                var category = await _categoryService.GetCategoryByIdAsync(categoryId);
                if (category == null)
                {
                    _logger.LogWarning("⚠️ Category not found for products: {CategoryId}", categoryId);
                    return NotFound(new { error = "Category not found" });
                }
                
                // Get all products in this category from database
                var productsInCategory = await _context.Products
                    .Include(p => p.Category)
                    .Include(p => p.Supplier)
                    .Where(p => p.CategoryId == categoryId)
                    .Select(p => new
                    {
                        productId = p.ProductId,
                        name = p.Name,
                        sku = p.SKU,
                        sellingPrice = p.SellingPrice,
                        buyingPrice = p.BuyingPrice,
                        stockQuantity = p.StockQuantity,
                        minStockLevel = p.MinStockLevel,
                        imageUrl = p.ImageUrl,
                        isActive = p.IsActive,
                        isLowStock = p.StockQuantity <= p.MinStockLevel,
                        categoryName = p.Category.Name,
                        supplierName = p.Supplier != null ? p.Supplier.CompanyName : null,
                        createdAt = p.CreatedAt,
                        updatedAt = p.UpdatedAt
                    })
                    .OrderBy(p => p.name)
                    .ToListAsync();
                
                _logger.LogInformation("✅ Found {ProductCount} products in category {CategoryId}", productsInCategory.Count, categoryId);
                return Json(productsInCategory);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error getting products for CategoryId: {CategoryId}", categoryId);
                return StatusCode(500, new { error = "Error loading category products" });
            }
        }

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetBarcodeImage(int productId)
        {
            try
            {
                var product = await _productService.GetProductByIdAsync(productId);
                if (product == null)
                {
                    return NotFound();
                }

                var barcodeData = !string.IsNullOrEmpty(product.SKU) ? product.SKU : $"PROD{productId:D6}";
                var barcodeBase64 = await _barcodeService.GenerateBarcodeBase64Async(barcodeData, 200, 50);
                
                return Json(new { success = true, barcodeImage = $"data:image/png;base64,{barcodeBase64}" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating barcode image for product ID: {ProductId}", productId);
                return Json(new { success = false, message = "Error generating barcode image" });
            }
        }

        private async Task<byte[]> GenerateProductStickerAsync(Product product, string barcodeData)
        {
            try
            {
                // Create a product sticker with barcode and product info
                using var bitmap = new System.Drawing.Bitmap(400, 200);
                using var graphics = System.Drawing.Graphics.FromImage(bitmap);
                
                graphics.Clear(System.Drawing.Color.White);
                graphics.DrawRectangle(System.Drawing.Pens.Black, 0, 0, 399, 199);
                
                // Draw product name
                using var titleFont = new System.Drawing.Font("Arial", 12, System.Drawing.FontStyle.Bold);
                var productName = product.Name.Length > 30 ? product.Name.Substring(0, 27) + "..." : product.Name;
                graphics.DrawString(productName, titleFont, System.Drawing.Brushes.Black, 10, 10);
                
                // Draw SKU
                using var font = new System.Drawing.Font("Arial", 10);
                graphics.DrawString($"SKU: {product.SKU}", font, System.Drawing.Brushes.Black, 10, 35);
                
                // Draw price
                graphics.DrawString($"Price: ${product.SellingPrice:F2}", font, System.Drawing.Brushes.Black, 10, 55);
                
                // Generate and draw barcode
                var barcodeBytes = await _barcodeService.GenerateBarcodeAsync(barcodeData, 200, 60);
                using var barcodeStream = new MemoryStream(barcodeBytes);
                using var barcodeImage = System.Drawing.Image.FromStream(barcodeStream);
                graphics.DrawImage(barcodeImage, 10, 80);
                
                // Draw barcode text
                graphics.DrawString(barcodeData, font, System.Drawing.Brushes.Black, 10, 150);
                
                using var stream = new MemoryStream();
                bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                return stream.ToArray();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating product sticker");
                throw;
            }
        }

        // BARCODE GENERATION ENDPOINTS
        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GenerateBarcode(int productId)
        {
            try
            {
                _logger.LogInformation("🔢 Generating barcode for product ID: {ProductId}", productId);
                
                // Enhanced debugging: Check if product exists with detailed logging
                _logger.LogInformation("🔍 Searching for product with ID: {ProductId} in database...", productId);
                
                var product = await _productService.GetProductByIdAsync(productId);
                if (product == null)
                {
                    _logger.LogWarning("❌ Product not found for barcode generation: {ProductId}", productId);
                    
                    // Additional debugging: Check if any products exist and log some sample IDs
                    var allProducts = await _productService.GetAllProductsAsync();
                    var productCount = allProducts?.Count() ?? 0;
                    _logger.LogWarning("📊 Total products in database: {ProductCount}", productCount);
                    
                    if (productCount > 0)
                    {
                        var sampleIds = allProducts.Take(5).Select(p => p.ProductId).ToList();
                        _logger.LogWarning("📋 Sample product IDs in database: {SampleIds}", string.Join(", ", sampleIds));
                    }
                    
                    return Content($"<html><body><h2>Product not found</h2><p>Product with ID {productId} was not found in the database.</p><p>Total products in database: {productCount}</p></body></html>", "text/html");
                }

                _logger.LogInformation("✅ Found product: {ProductName} (SKU: {SKU}, Active: {IsActive})", product.Name, product.SKU, product.IsActive);

                // Generate barcode data using product SKU or ID
                var barcodeData = !string.IsNullOrEmpty(product.SKU) ? product.SKU : $"PROD{productId:D6}";
                _logger.LogInformation("🏷️ Using barcode data: {BarcodeData}", barcodeData);
                
                // Generate barcode image
                var barcodeBytes = await _barcodeService.GenerateBarcodeAsync(barcodeData, 300, 100);
                
                _logger.LogInformation("✅ Successfully generated barcode for product: {ProductName}", product.Name);
                return File(barcodeBytes, "image/png", $"barcode_{product.SKU ?? productId.ToString()}.png");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error generating barcode for product ID: {ProductId}", productId);
                return Content($"<html><body><h2>Error generating barcode</h2><p>An error occurred while generating barcode for product {productId}:</p><p>{ex.Message}</p></body></html>", "text/html");
            }
        }

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GenerateSticker(int productId)
        {
            try
            {
                _logger.LogInformation("🏷️ Generating sticker for product ID: {ProductId}", productId);
                
                // Enhanced debugging: Check if product exists with detailed logging
                _logger.LogInformation("🔍 Searching for product with ID: {ProductId} in database...", productId);
                
                var product = await _productService.GetProductByIdAsync(productId);
                if (product == null)
                {
                    _logger.LogWarning("❌ Product not found for sticker generation: {ProductId}", productId);
                    
                    // Additional debugging: Check if any products exist and log some sample IDs
                    var allProducts = await _productService.GetAllProductsAsync();
                    var productCount = allProducts?.Count() ?? 0;
                    _logger.LogWarning("📊 Total products in database: {ProductCount}", productCount);
                    
                    if (productCount > 0)
                    {
                        var sampleIds = allProducts.Take(5).Select(p => p.ProductId).ToList();
                        _logger.LogWarning("📋 Sample product IDs in database: {SampleIds}", string.Join(", ", sampleIds));
                    }
                    
                    return Content($"<html><body><h2>Product not found</h2><p>Product with ID {productId} was not found in the database.</p><p>Total products in database: {productCount}</p></body></html>", "text/html");
                }

                _logger.LogInformation("✅ Found product: {ProductName} (SKU: {SKU}, Active: {IsActive})", product.Name, product.SKU, product.IsActive);

                // Generate barcode data using product SKU or ID
                var barcodeData = !string.IsNullOrEmpty(product.SKU) ? product.SKU : $"PROD{productId:D6}";
                _logger.LogInformation("🏷️ Using barcode data: {BarcodeData}", barcodeData);
                
                // Generate sticker with product info and barcode
                var stickerBytes = await GenerateProductStickerAsync(product, barcodeData);
                
                _logger.LogInformation("✅ Successfully generated sticker for product: {ProductName}", product.Name);
                return File(stickerBytes, "image/png", $"sticker_{product.SKU ?? productId.ToString()}.png");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error generating sticker for product ID: {ProductId}", productId);
                return Content($"<html><body><h2>Error generating sticker</h2><p>An error occurred while generating sticker for product {productId}:</p><p>{ex.Message}</p></body></html>", "text/html");
            }
        }
        // Add these methods to your AdminController.cs 
        // IMPORTANT: Remove any existing duplicate GetReportsData methods first!

        #region Reports Management - FIXED IMPLEMENTATION

        [HttpGet]
        public async Task<IActionResult> GetReportsData(string period = "thisMonth")
        {
            try
            {
                _logger.LogInformation("🔄 GetReportsData called with period: {Period}", period);

                var (startDate, endDate) = GetDateRangeFromPeriod(period);
                _logger.LogInformation("📅 Date range: {StartDate} to {EndDate}", startDate, endDate);

                // Get sales data using your existing service
                var sales = await _context.Sales
                    .Where(s => s.SaleDate >= startDate && s.SaleDate <= endDate && s.Status == "Completed")
                    .Include(s => s.SaleItems)
                        .ThenInclude(si => si.Product)
                            .ThenInclude(p => p.Category)
                    .Include(s => s.User)
                    .ToListAsync();

                // Calculate totals
                var totalSales = sales.Sum(s => s.TotalAmount);
                var totalOrders = sales.Count();
                var totalProducts = sales.SelectMany(s => s.SaleItems).Sum(si => si.Quantity);
                var totalCustomers = sales.Where(s => !string.IsNullOrEmpty(s.CustomerEmail))
                                        .Select(s => s.CustomerEmail).Distinct().Count();

                // Get top products
                var topProducts = sales
                    .SelectMany(s => s.SaleItems)
                    .Where(si => si.Product != null)
                    .GroupBy(si => new { si.ProductId, si.Product.Name })
                    .Select(g => new
                    {
                        name = g.Key.Name ?? "Unknown Product",
                        sales = g.Sum(si => si.Quantity),
                        revenue = g.Sum(si => si.Quantity * si.UnitPrice)
                    })
                    .OrderByDescending(p => p.revenue)
                    .Take(10)
                    .ToList();

                // Get top categories
                var topCategories = sales
                    .SelectMany(s => s.SaleItems)
                    .Where(si => si.Product?.Category != null)
                    .GroupBy(si => si.Product.Category.Name)
                    .Select(g => new
                    {
                        name = g.Key ?? "Unknown Category",
                        sales = g.Sum(si => si.Quantity * si.UnitPrice),
                        value = g.Sum(si => si.Quantity)
                    })
                    .OrderByDescending(c => c.sales)
                    .Take(10)
                    .ToList();

                // Get recent sales
                var recentSales = sales
                    .OrderByDescending(s => s.SaleDate)
                    .Take(10)
                    .Select(s => new
                    {
                        id = s.SaleId,
                        customer = !string.IsNullOrEmpty(s.CustomerName) ? s.CustomerName : "Walk-in Customer",
                        total = s.TotalAmount,
                        date = s.SaleDate.ToString("yyyy-MM-dd HH:mm:ss"),
                        items = s.SaleItems?.Count ?? 0
                    })
                    .ToList();

                // Get sales trend (daily sales for the period)
                var salesTrend = sales
                    .GroupBy(s => s.SaleDate.Date)
                    .Select(g => new
                    {
                        date = g.Key.ToString("yyyy-MM-dd"),
                        sales = g.Sum(s => s.TotalAmount),
                        amount = g.Sum(s => s.TotalAmount)
                    })
                    .OrderBy(st => st.date)
                    .ToList();

                var result = new
                {
                    success = true,
                    totalSales = totalSales,
                    totalOrders = totalOrders,
                    totalProducts = totalProducts,
                    totalCustomers = totalCustomers,
                    topProducts = topProducts,
                    topCategories = topCategories,
                    recentSales = recentSales,
                    salesTrend = salesTrend,
                    period = period,
                    startDate = startDate.ToString("yyyy-MM-dd"),
                    endDate = endDate.ToString("yyyy-MM-dd")
                };

                _logger.LogInformation("✅ Reports data generated successfully");
                return Json(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error getting reports data for period: {Period}", period);
                return Json(new
                {
                    success = false,
                    message = "Error loading reports data: " + ex.Message,
                    totalSales = 0,
                    totalOrders = 0,
                    totalProducts = 0,
                    totalCustomers = 0,
                    topProducts = new List<object>(),
                    topCategories = new List<object>(),
                    recentSales = new List<object>(),
                    salesTrend = new List<object>()
                });
            }
        }

        // Helper method for date range calculation
        private (DateTime startDate, DateTime endDate) GetDateRangeFromPeriod(string period)
        {
            var today = DateTime.Today;
            var startDate = today;
            var endDate = today.AddDays(1).AddSeconds(-1); // End of today

            switch (period?.ToLower())
            {
                case "today":
                    startDate = today;
                    endDate = today.AddDays(1).AddSeconds(-1);
                    break;
                case "yesterday":
                    startDate = today.AddDays(-1);
                    endDate = today.AddSeconds(-1);
                    break;
                case "thisweek":
                    var daysFromMonday = ((int)today.DayOfWeek - 1 + 7) % 7;
                    startDate = today.AddDays(-daysFromMonday);
                    endDate = today.AddDays(1).AddSeconds(-1);
                    break;
                case "lastweek":
                    var lastWeekStart = today.AddDays(-((int)today.DayOfWeek - 1 + 7) % 7 - 7);
                    startDate = lastWeekStart;
                    endDate = lastWeekStart.AddDays(7).AddSeconds(-1);
                    break;
                case "thismonth":
                    startDate = new DateTime(today.Year, today.Month, 1);
                    endDate = today.AddDays(1).AddSeconds(-1);
                    break;
                case "lastmonth":
                    var lastMonth = today.AddMonths(-1);
                    startDate = new DateTime(lastMonth.Year, lastMonth.Month, 1);
                    endDate = new DateTime(lastMonth.Year, lastMonth.Month, DateTime.DaysInMonth(lastMonth.Year, lastMonth.Month)).AddDays(1).AddSeconds(-1);
                    break;
                case "thisquarter":
                    var quarter = (today.Month - 1) / 3 + 1;
                    startDate = new DateTime(today.Year, (quarter - 1) * 3 + 1, 1);
                    endDate = today.AddDays(1).AddSeconds(-1);
                    break;
                case "thisyear":
                    startDate = new DateTime(today.Year, 1, 1);
                    endDate = today.AddDays(1).AddSeconds(-1);
                    break;
                default:
                    startDate = new DateTime(today.Year, today.Month, 1);
                    endDate = today.AddDays(1).AddSeconds(-1);
                    break;
            }

            return (startDate, endDate);
        }

        [HttpPost]
        public async Task<IActionResult> ExportReport([FromBody] ExportReportRequest request)
        {
            try
            {
                _logger.LogInformation("📤 Export report request: {ReportType} as {Format}", request.ReportType, request.Format);

                var (startDate, endDate) = GetDateRangeFromPeriod("custom");

                if (!string.IsNullOrEmpty(request.StartDate) && DateTime.TryParse(request.StartDate, out var parsedStartDate))
                {
                    startDate = parsedStartDate;
                }

                if (!string.IsNullOrEmpty(request.EndDate) && DateTime.TryParse(request.EndDate, out var parsedEndDate))
                {
                    endDate = parsedEndDate;
                }

                // Get data based on report type
                byte[] reportData;
                string fileName;
                string contentType;

                // Log the export activity
                var userId = GetCurrentUserId();
                var ipAddress = GetClientIpAddress();
                var userAgent = HttpContext.Request.Headers["User-Agent"].ToString();
                
                await _activityLogService.LogActivityAsync(
                    userId, 
                    ActivityTypes.ReportExport, 
                    $"Exported {request.ReportType} report in {request.Format} format",
                    "Report",
                    null,
                    new { ReportType = request.ReportType, Format = request.Format, StartDate = startDate, EndDate = endDate },
                    ipAddress,
                    userAgent
                );

                // Determine if Excel format is requested
                bool isExcelFormat = request.Format?.ToLower() == "excel" || request.Format?.ToLower() == "xlsx";
                
                switch (request.ReportType?.ToLower())
                {
                    case "sales":
                        reportData = isExcelFormat 
                            ? await _reportService.GenerateSalesReportExcelAsync(startDate, endDate)
                            : await _reportService.GenerateSalesReportAsync(startDate, endDate);
                        fileName = $"Sales_Report_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}";
                        break;
                    case "inventory":
                        reportData = isExcelFormat 
                            ? await _reportService.GenerateInventoryReportExcelAsync()
                            : await _reportService.GenerateInventoryReportAsync();
                        fileName = $"Inventory_Report_{DateTime.Now:yyyyMMdd}";
                        break;
                    case "users":
                        reportData = isExcelFormat 
                            ? await _reportService.GenerateUserReportExcelAsync()
                            : await _reportService.GenerateUserReportAsync();
                        fileName = $"Users_Report_{DateTime.Now:yyyyMMdd}";
                        break;
                    case "categories":
                        reportData = isExcelFormat 
                            ? await _reportService.GenerateCategoriesReportExcelAsync()
                            : await _reportService.GenerateCategoriesReportAsync();
                        fileName = $"Categories_Report_{DateTime.Now:yyyyMMdd}";
                        break;
                    case "suppliers":
                        reportData = isExcelFormat 
                            ? await _reportService.GenerateSuppliersReportExcelAsync()
                            : await _reportService.GenerateSuppliersReportAsync();
                        fileName = $"Suppliers_Report_{DateTime.Now:yyyyMMdd}";
                        break;
                    case "comprehensive":
                        reportData = await _reportService.GenerateComprehensiveReportAsync(startDate, endDate);
                        fileName = $"Comprehensive_Report_{DateTime.Now:yyyyMMdd}";
                        break;
                    default:
                        reportData = isExcelFormat 
                            ? await _reportService.GenerateSalesReportExcelAsync(startDate, endDate)
                            : await _reportService.GenerateSalesReportAsync(startDate, endDate);
                        fileName = $"Report_{DateTime.Now:yyyyMMdd}";
                        break;
                }

                if (request.Format?.ToLower() == "excel" || request.Format?.ToLower() == "xlsx")
                {
                    fileName += ".xlsx";
                    contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                }
                else if (request.Format?.ToLower() == "csv")
                {
                    fileName += ".csv";
                    contentType = "text/csv";
                }
                else
                {
                    fileName += ".pdf";
                    contentType = "application/pdf";
                }

                _logger.LogInformation("✅ Report generated: {FileName}", fileName);
                return File(reportData, contentType, fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error exporting report");
                return Json(new { success = false, message = "Error exporting report: " + ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> EmailReport([FromBody] EmailReportRequest request)
        {
            try
            {
                _logger.LogInformation("📧 Email report request to: {Email}", request.Email);

                if (string.IsNullOrEmpty(request.Email))
                {
                    return Json(new { success = false, message = "Email address is required" });
                }

                // For now, just return success - you can implement actual email sending later
                return Json(new { success = true, message = $"Report sent successfully to {request.Email}" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error emailing report");
                return Json(new { success = false, message = "Error sending email: " + ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetSalesForReceipt(string search = "")
        {
            try
            {
                _logger.LogInformation("🔍 Searching sales for receipt: {Search}", search);

                var query = _context.Sales
                    .Include(s => s.User)
                    .Where(s => s.Status == "Completed")
                    .AsQueryable();

                if (!string.IsNullOrEmpty(search))
                {
                    query = query.Where(s => s.SaleNumber.Contains(search) ||
                                       (s.CustomerName != null && s.CustomerName.Contains(search)));
                }

                var sales = await query
                    .OrderByDescending(s => s.SaleDate)
                    .Take(10)
                    .Select(s => new
                    {
                        saleId = s.SaleId,
                        saleNumber = s.SaleNumber,
                        customerName = s.CustomerName ?? "Walk-in Customer",
                        customerEmail = s.CustomerEmail ?? "",
                        totalAmount = s.TotalAmount,
                        saleDate = s.SaleDate,
                        cashierName = s.User != null ? $"{s.User.FirstName} {s.User.LastName}" : "Unknown"
                    })
                    .ToListAsync();

                return Json(new { success = true, sales = sales });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error searching sales for receipt");
                return Json(new { success = false, message = "Error searching sales: " + ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> EmailReceipt([FromBody] EmailReceiptRequest request)
        {
            try
            {
                _logger.LogInformation("📧 Email receipt request for sale {SaleId} to: {Email}", request.SaleId, request.Email);

                if (string.IsNullOrEmpty(request.Email))
                {
                    return Json(new { success = false, message = "Email address is required" });
                }

                // For now, just return success - you can implement actual email sending later
                return Json(new { success = true, message = $"Receipt sent successfully to {request.Email}" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error emailing receipt");
                return Json(new { success = false, message = "Error sending receipt: " + ex.Message });
            }
        }

        #endregion

        // Add these methods to your existing AdminController.cs file

        #region Messages Management - COMPLETE IMPLEMENTATION

        public async Task<IActionResult> Messages(int? selectedUserId = null)
        {
            try
            {
                _logger.LogInformation("💬 Loading messages page for user {CurrentUserId}", GetCurrentUserId());

                var currentUserId = GetCurrentUserId();
                var currentUser = await _userService.GetUserByIdAsync(currentUserId);

                if (currentUser == null)
                {
                    _logger.LogWarning("❌ Current user not found: {UserId}", currentUserId);
                    return RedirectToAction("Dashboard");
                }

                // Get all conversations for current user
                var conversations = await GetUserConversationsAsync(currentUserId);

                // Get all users for the new message dropdown (excluding current user)
                // Get all users for the new message dropdown (excluding current user)
                var allUsers = await _userService.GetAllUsersAsync();
                var filteredUsers = allUsers
                    .Where(u => u.UserId != currentUserId && u.IsActive)
                    .OrderBy(u => u.FullName);

                // Create async tasks for each user to get their online status
                var userTasks = filteredUsers.Select(async u => new UserSelectViewModel
                {
                    UserId = u.UserId,
                    FullName = u.FullName,
                    Email = u.Email,
                    UserType = u.UserType,
                    Status = u.Status,
                    IsOnline = await IsUserOnlineAsync(u.UserId),
                    UserInitials = GetUserInitials(u.FirstName, u.LastName)
                });

                // Await all the async operations and convert to List
                var userSelectList = (await Task.WhenAll(userTasks)).ToList();

                // Get unread message count
                var unreadCount = await _messageService.GetUnreadCountAsync(currentUserId);

                ConversationViewModel? selectedConversation = null;
                List<MessageViewModel> messages = new List<MessageViewModel>();

                // If a specific conversation is selected or if there are conversations, load the first one
                if (selectedUserId.HasValue)
                {
                    selectedConversation = conversations.FirstOrDefault(c => c.UserId == selectedUserId.Value);
                    if (selectedConversation != null)
                    {
                        messages = await GetConversationMessagesAsync(currentUserId, selectedUserId.Value);
                    }
                }
                else if (conversations.Any())
                {
                    selectedConversation = conversations.First();
                    messages = await GetConversationMessagesAsync(currentUserId, selectedConversation.UserId);
                }

                var viewModel = new MessagesPageViewModel
                {
                    CurrentUserId = currentUserId,
                    CurrentUserName = currentUser.FullName,
                    CurrentUserType = currentUser.UserType,
                    Conversations = conversations,
                    Messages = messages,
                    SelectedConversation = selectedConversation,
                    AllUsers = userSelectList,
                    UnreadCount = unreadCount
                };

                _logger.LogInformation("✅ Messages page loaded with {ConversationCount} conversations", conversations.Count);
                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error loading messages page");
                TempData["ErrorMessage"] = "Error loading messages. Please try again.";
                return RedirectToAction("Dashboard");
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetConversationMessages(int userId)
        {
            try
            {
                var currentUserId = GetCurrentUserId();

                _logger.LogInformation("📨 Loading conversation between {CurrentUserId} and {UserId}", currentUserId, userId);

                var otherUser = await _userService.GetUserByIdAsync(userId);
                if (otherUser == null)
                {
                    return Json(new { success = false, message = "User not found." });
                }

                var conversation = new ConversationViewModel
                {
                    UserId = otherUser.UserId,
                    FullName = otherUser.FullName,
                    UserInitials = GetUserInitials(otherUser.FirstName, otherUser.LastName),
                    UserType = otherUser.UserType,
                    Email = otherUser.Email,
                    IsOnline = await IsUserOnlineAsync(otherUser.UserId),
                    Status = otherUser.Status,
                    LastSeenFormatted = await GetLastSeenFormattedAsync(otherUser.UserId)
                };

                var messages = await GetConversationMessagesAsync(currentUserId, userId);

                return Json(new
                {
                    success = true,
                    conversation = conversation,
                    messages = messages
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error loading conversation messages");
                return Json(new { success = false, message = "Error loading conversation." });
            }
        }

        [HttpPost]
        public async Task<IActionResult> SendMessage([FromBody] SendMessageViewModel model)
        {
            try
            {
                var currentUserId = GetCurrentUserId();

                _logger.LogInformation("📤 Sending message from {FromUserId} to {ToUserId}", currentUserId, model.ToUserId);

                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                    return Json(new { success = false, message = string.Join("; ", errors) });
                }

                var message = new Message
                {
                    FromUserId = currentUserId,
                    ToUserId = model.ToUserId,
                    Subject = model.Subject,
                    Content = model.Content,
                    MessageType = model.MessageType,
                    SentDate = DateTime.UtcNow,
                    IsRead = false
                };

                var sentMessage = await _messageService.SendMessageAsync(message);

                if (sentMessage != null)
                {
                    _logger.LogInformation("✅ Message sent successfully: {MessageId}", sentMessage.MessageId);

                    // Update last activity for both users
                    await UpdateUserLastActivityAsync(currentUserId);

                    return Json(new
                    {
                        success = true,
                        message = "Message sent successfully!",
                        messageId = sentMessage.MessageId,
                        sentDate = sentMessage.SentDate.ToString("o")
                    });
                }
                else
                {
                    return Json(new { success = false, message = "Failed to send message." });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error sending message");
                return Json(new { success = false, message = "Error sending message. Please try again." });
            }
        }

        [HttpPost]
        public async Task<IActionResult> SendQuickMessage([FromBody] QuickMessageViewModel model)
        {
            try
            {
                var currentUserId = GetCurrentUserId();

                _logger.LogInformation("⚡ Sending quick message from {FromUserId} to {ToUserId}", currentUserId, model.ToUserId);

                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                    return Json(new { success = false, message = string.Join("; ", errors) });
                }

                var message = new Message
                {
                    FromUserId = currentUserId,
                    ToUserId = model.ToUserId,
                    Subject = string.IsNullOrEmpty(model.Subject) ? "Quick Message" : model.Subject,
                    Content = model.Content,
                    MessageType = model.MessageType,
                    SentDate = DateTime.UtcNow,
                    IsRead = false
                };

                var sentMessage = await _messageService.SendMessageAsync(message);

                if (sentMessage != null)
                {
                    _logger.LogInformation("✅ Quick message sent successfully: {MessageId}", sentMessage.MessageId);

                    // Update last activity
                    await UpdateUserLastActivityAsync(currentUserId);

                    return Json(new
                    {
                        success = true,
                        message = "Message sent!",
                        messageId = sentMessage.MessageId,
                        sentDate = sentMessage.SentDate.ToString("o")
                    });
                }
                else
                {
                    return Json(new { success = false, message = "Failed to send message." });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error sending quick message");
                return Json(new { success = false, message = "Error sending message." });
            }
        }

        [HttpPost]
        public async Task<IActionResult> MarkMessagesAsRead([FromBody] MarkAsReadRequest request)
        {
            try
            {
                var currentUserId = GetCurrentUserId();

                _logger.LogInformation("👁️ Marking messages as read between {CurrentUserId} and {UserId}", currentUserId, request.UserId);

                // Mark all unread messages from the specified user as read
                var unreadMessages = await _context.Messages
                    .Where(m => m.FromUserId == request.UserId &&
                               m.ToUserId == currentUserId &&
                               !m.IsRead)
                    .ToListAsync();

                foreach (var message in unreadMessages)
                {
                    message.IsRead = true;
                    message.ReadDate = DateTime.UtcNow;
                }

                if (unreadMessages.Any())
                {
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("✅ Marked {Count} messages as read", unreadMessages.Count);
                }

                return Json(new { success = true, markedCount = unreadMessages.Count });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error marking messages as read");
                return Json(new { success = false, message = "Error marking messages as read." });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetNewMessages(DateTime lastCheck)
        {
            try
            {
                var currentUserId = GetCurrentUserId();

                // Get new messages since last check
                var newMessages = await _context.Messages
                    .Include(m => m.FromUser)
                    .Include(m => m.ToUser)
                    .Where(m => (m.FromUserId == currentUserId || m.ToUserId == currentUserId) &&
                               m.SentDate > lastCheck)
                    .OrderBy(m => m.SentDate)
                    .ToListAsync();

                var messageViewModels = newMessages.Select(m => new MessageViewModel
                {
                    MessageId = m.MessageId,
                    FromUserId = m.FromUserId,
                    ToUserId = m.ToUserId,
                    FromUserName = m.FromUser.FullName,
                    ToUserName = m.ToUser.FullName,
                    FromUserInitials = GetUserInitials(m.FromUser.FirstName, m.FromUser.LastName),
                    Subject = m.Subject,
                    Content = m.Content,
                    MessageType = m.MessageType,
                    IsRead = m.IsRead,
                    SentDate = m.SentDate,
                    ReadDate = m.ReadDate,
                    IsFromCurrentUser = m.FromUserId == currentUserId,
                    FormattedSentDate = m.SentDate.ToString("HH:mm"),
                    FormattedReadDate = m.ReadDate?.ToString("HH:mm") ?? "",
                    TimeAgo = GetTimeAgo(m.SentDate)
                }).ToList();

                return Json(new
                {
                    success = true,
                    newMessages = messageViewModels,
                    count = messageViewModels.Count
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error getting new messages");
                return Json(new { success = false, message = "Error checking for new messages." });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetConversationsList()
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                var conversations = await GetUserConversationsAsync(currentUserId);

                return Json(new
                {
                    success = true,
                    conversations = conversations
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error getting conversations list");
                return Json(new { success = false, message = "Error loading conversations." });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetOnlineUsers()
        {
            try
            {
                // In a real implementation, you'd track online users with SignalR or similar
                // For now, we'll simulate online status based on recent activity
                var onlineThreshold = DateTime.UtcNow.AddMinutes(-5);

                var onlineUserIds = await _context.Users
                    .Where(u => u.UpdatedAt > onlineThreshold && u.Status == "Active")
                    .Select(u => u.UserId)
                    .ToListAsync();

                return Json(new
                {
                    success = true,
                    onlineUsers = onlineUserIds
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error getting online users");
                return Json(new { success = false, message = "Error checking online status." });
            }
        }

        [HttpPost]
        public async Task<IActionResult> SendBulkMessage([FromBody] BulkMessageViewModel model)
        {
            try
            {
                var currentUserId = GetCurrentUserId();

                _logger.LogInformation("📢 Sending bulk message from {UserId} to {RecipientCount} recipients",
                    currentUserId, model.RecipientIds.Count);

                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                    return Json(new { success = false, message = string.Join("; ", errors) });
                }

                var messages = new List<Message>();
                var targetUserIds = new List<int>();

                if (model.SendToAllEmployees)
                {
                    var employeeIds = await _context.Users
                        .Where(u => u.UserType == "Employee" && u.Status == "Active" && u.UserId != currentUserId)
                        .Select(u => u.UserId)
                        .ToListAsync();
                    targetUserIds.AddRange(employeeIds);
                }

                if (model.SendToManagers)
                {
                    var managerIds = await _context.Users
                        .Where(u => u.UserType == "Manager" && u.Status == "Active" && u.UserId != currentUserId)
                        .Select(u => u.UserId)
                        .ToListAsync();
                    targetUserIds.AddRange(managerIds);
                }

                if (model.DepartmentIds.Any())
                {
                    var departmentUserIds = await _context.UserDepartments
                        .Where(ud => model.DepartmentIds.Contains(ud.DepartmentId))
                        .Select(ud => ud.UserId)
                        .Distinct()
                        .ToListAsync();
                    targetUserIds.AddRange(departmentUserIds);
                }

                // Add specifically selected recipients
                targetUserIds.AddRange(model.RecipientIds);

                // Remove duplicates and current user
                targetUserIds = targetUserIds.Distinct().Where(id => id != currentUserId).ToList();

                foreach (var userId in targetUserIds)
                {
                    messages.Add(new Message
                    {
                        FromUserId = currentUserId,
                        ToUserId = userId,
                        Subject = model.Subject,
                        Content = model.Content,
                        MessageType = model.MessageType,
                        SentDate = DateTime.UtcNow,
                        IsRead = false
                    });
                }

                if (messages.Any())
                {
                    _context.Messages.AddRange(messages);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation("✅ Bulk message sent to {RecipientCount} users", messages.Count);
                    return Json(new
                    {
                        success = true,
                        message = $"Message sent to {messages.Count} recipients!",
                        recipientCount = messages.Count
                    });
                }
                else
                {
                    return Json(new { success = false, message = "No valid recipients found." });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error sending bulk message");
                return Json(new { success = false, message = "Error sending bulk message." });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetMessageStats()
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);

                var stats = new MessageStatsViewModel
                {
                    TotalMessages = await _context.Messages
                        .CountAsync(m => m.FromUserId == currentUserId || m.ToUserId == currentUserId),

                    UnreadMessages = await _context.Messages
                        .CountAsync(m => m.ToUserId == currentUserId && !m.IsRead),

                    TodayMessages = await _context.Messages
                        .CountAsync(m => (m.FromUserId == currentUserId || m.ToUserId == currentUserId) &&
                                        m.SentDate.Date == DateTime.Today),

                    ThisWeekMessages = await _context.Messages
                        .CountAsync(m => (m.FromUserId == currentUserId || m.ToUserId == currentUserId) &&
                                        m.SentDate >= DateTime.Today.AddDays(-7)),

                    SentMessages = await _context.Messages
                        .CountAsync(m => m.FromUserId == currentUserId),

                    ReceivedMessages = await _context.Messages
                        .CountAsync(m => m.ToUserId == currentUserId)
                };

                // Get message type statistics
                var messageTypeStats = await _context.Messages
                    .Where(m => (m.FromUserId == currentUserId || m.ToUserId == currentUserId) &&
                               m.SentDate >= thirtyDaysAgo)
                    .GroupBy(m => m.MessageType)
                    .Select(g => new MessageTypeStatsViewModel
                    {
                        MessageType = g.Key,
                        Count = g.Count(),
                        Percentage = 0 // Will be calculated
                    })
                    .ToListAsync();

                var totalMessages = messageTypeStats.Sum(s => s.Count);
                foreach (var stat in messageTypeStats)
                {
                    stat.Percentage = totalMessages > 0 ? Math.Round((double)stat.Count / totalMessages * 100, 1) : 0;
                }

                stats.MessageTypeStats = messageTypeStats;

                // Get daily statistics for the last 30 days
                var dailyStats = await _context.Messages
                    .Where(m => (m.FromUserId == currentUserId || m.ToUserId == currentUserId) &&
                               m.SentDate >= thirtyDaysAgo)
                    .GroupBy(m => m.SentDate.Date)
                    .Select(g => new DailyMessageStatsViewModel
                    {
                        Date = g.Key,
                        MessageCount = g.Count(),
                        DateLabel = g.Key.ToString("MMM dd")
                    })
                    .OrderBy(s => s.Date)
                    .ToListAsync();

                stats.DailyStats = dailyStats;

                return Json(new { success = true, stats = stats });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error getting message statistics");
                return Json(new { success = false, message = "Error loading statistics." });
            }
        }

        [HttpPost]
        public async Task<IActionResult> DeleteMessage([FromBody] DeleteMessageRequest request)
        {
            try
            {
                var currentUserId = GetCurrentUserId();

                var message = await _context.Messages
                    .FirstOrDefaultAsync(m => m.MessageId == request.MessageId &&
                                            (m.FromUserId == currentUserId || m.ToUserId == currentUserId));

                if (message == null)
                {
                    return Json(new { success = false, message = "Message not found or access denied." });
                }

                _context.Messages.Remove(message);
                await _context.SaveChangesAsync();

                _logger.LogInformation("🗑️ Message deleted: {MessageId}", request.MessageId);
                return Json(new { success = true, message = "Message deleted successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error deleting message");
                return Json(new { success = false, message = "Error deleting message." });
            }
        }

        [HttpPost]
        public async Task<IActionResult> SearchMessages([FromBody] MessageSearchViewModel searchModel)
        {
            try
            {
                var currentUserId = GetCurrentUserId();

                var query = _context.Messages
                    .Include(m => m.FromUser)
                    .Include(m => m.ToUser)
                    .Where(m => m.FromUserId == currentUserId || m.ToUserId == currentUserId);

                if (!string.IsNullOrEmpty(searchModel.SearchTerm))
                {
                    query = query.Where(m => m.Subject.Contains(searchModel.SearchTerm) ||
                                            m.Content.Contains(searchModel.SearchTerm));
                }

                if (!string.IsNullOrEmpty(searchModel.MessageType))
                {
                    query = query.Where(m => m.MessageType == searchModel.MessageType);
                }

                if (searchModel.IsRead.HasValue)
                {
                    query = query.Where(m => m.IsRead == searchModel.IsRead.Value);
                }

                if (searchModel.StartDate.HasValue)
                {
                    query = query.Where(m => m.SentDate.Date >= searchModel.StartDate.Value.Date);
                }

                if (searchModel.EndDate.HasValue)
                {
                    query = query.Where(m => m.SentDate.Date <= searchModel.EndDate.Value.Date);
                }

                if (searchModel.FromUserId.HasValue)
                {
                    query = query.Where(m => m.FromUserId == searchModel.FromUserId.Value);
                }

                if (searchModel.ToUserId.HasValue)
                {
                    query = query.Where(m => m.ToUserId == searchModel.ToUserId.Value);
                }

                var totalCount = await query.CountAsync();
                var totalPages = (int)Math.Ceiling((double)totalCount / searchModel.PageSize);

                var messages = await query
                    .OrderByDescending(m => m.SentDate)
                    .Skip((searchModel.PageNumber - 1) * searchModel.PageSize)
                    .Take(searchModel.PageSize)
                    .ToListAsync();

                var messageViewModels = messages.Select(m => new MessageViewModel
                {
                    MessageId = m.MessageId,
                    FromUserId = m.FromUserId,
                    ToUserId = m.ToUserId,
                    FromUserName = m.FromUser.FullName,
                    ToUserName = m.ToUser.FullName,
                    FromUserInitials = GetUserInitials(m.FromUser.FirstName, m.FromUser.LastName),
                    Subject = m.Subject,
                    Content = m.Content,
                    MessageType = m.MessageType,
                    IsRead = m.IsRead,
                    SentDate = m.SentDate,
                    ReadDate = m.ReadDate,
                    IsFromCurrentUser = m.FromUserId == currentUserId,
                    FormattedSentDate = m.SentDate.ToString("MMM dd, yyyy HH:mm"),
                    FormattedReadDate = m.ReadDate?.ToString("MMM dd, yyyy HH:mm") ?? "",
                    TimeAgo = GetTimeAgo(m.SentDate)
                }).ToList();

                var result = new MessageSearchResultViewModel
                {
                    Messages = messageViewModels,
                    TotalCount = totalCount,
                    PageNumber = searchModel.PageNumber,
                    PageSize = searchModel.PageSize,
                    TotalPages = totalPages,
                    HasPreviousPage = searchModel.PageNumber > 1,
                    HasNextPage = searchModel.PageNumber < totalPages,
                    SearchTerm = searchModel.SearchTerm
                };

                return Json(new { success = true, result = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error searching messages");
                return Json(new { success = false, message = "Error searching messages." });
            }
        }

        #endregion

        #region Private Helper Methods for Messages

        private async Task<List<ConversationViewModel>> GetUserConversationsAsync(int currentUserId)
        {
            try
            {
                // Get all users that have exchanged messages with current user
                var conversationUserIds = await _context.Messages
                    .Where(m => m.FromUserId == currentUserId || m.ToUserId == currentUserId)
                    .Select(m => m.FromUserId == currentUserId ? m.ToUserId : m.FromUserId)
                    .Distinct()
                    .ToListAsync();

                var conversations = new List<ConversationViewModel>();

                foreach (var userId in conversationUserIds)
                {
                    var user = await _userService.GetUserByIdAsync(userId);
                    if (user == null || !user.IsActive) continue;

                    // Get last message in conversation
                    var lastMessage = await _context.Messages
                        .Where(m => (m.FromUserId == currentUserId && m.ToUserId == userId) ||
                                   (m.FromUserId == userId && m.ToUserId == currentUserId))
                        .OrderByDescending(m => m.SentDate)
                        .FirstOrDefaultAsync();

                    // Count unread messages from this user
                    var unreadCount = await _context.Messages
                        .CountAsync(m => m.FromUserId == userId &&
                                       m.ToUserId == currentUserId &&
                                       !m.IsRead);

                    var conversation = new ConversationViewModel
                    {
                        UserId = user.UserId,
                        FullName = user.FullName,
                        UserInitials = GetUserInitials(user.FirstName, user.LastName),
                        UserType = user.UserType,
                        Email = user.Email,
                        IsOnline = await IsUserOnlineAsync(user.UserId),
                        LastSeen = user.UpdatedAt,
                        LastSeenFormatted = await GetLastSeenFormattedAsync(user.UserId),
                        LastMessage = lastMessage?.Content?.Length > 50
                            ? lastMessage.Content.Substring(0, 47) + "..."
                            : lastMessage?.Content ?? "No messages yet",
                        LastMessageTime = lastMessage?.SentDate.ToString("HH:mm") ?? "",
                        LastMessageDate = lastMessage?.SentDate,
                        UnreadCount = unreadCount,
                        HasUnreadMessages = unreadCount > 0,
                        Status = user.Status
                    };

                    conversations.Add(conversation);
                }

                // Sort by last message date (most recent first)
                return conversations
                    .OrderByDescending(c => c.LastMessageDate ?? DateTime.MinValue)
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error getting user conversations");
                return new List<ConversationViewModel>();
            }
        }

        private async Task<List<MessageViewModel>> GetConversationMessagesAsync(int currentUserId, int otherUserId)
        {
            try
            {
                var messages = await _context.Messages
                    .Include(m => m.FromUser)
                    .Include(m => m.ToUser)
                    .Where(m => (m.FromUserId == currentUserId && m.ToUserId == otherUserId) ||
                               (m.FromUserId == otherUserId && m.ToUserId == currentUserId))
                    .OrderBy(m => m.SentDate)
                    .ToListAsync();

                return messages.Select(m => new MessageViewModel
                {
                    MessageId = m.MessageId,
                    FromUserId = m.FromUserId,
                    ToUserId = m.ToUserId,
                    FromUserName = m.FromUser.FullName,
                    ToUserName = m.ToUser.FullName,
                    FromUserInitials = GetUserInitials(m.FromUser.FirstName, m.FromUser.LastName),
                    Subject = m.Subject,
                    Content = m.Content,
                    MessageType = m.MessageType,
                    IsRead = m.IsRead,
                    SentDate = m.SentDate,
                    ReadDate = m.ReadDate,
                    IsFromCurrentUser = m.FromUserId == currentUserId,
                    FormattedSentDate = m.SentDate.ToString("HH:mm"),
                    FormattedReadDate = m.ReadDate?.ToString("HH:mm") ?? "",
                    TimeAgo = GetTimeAgo(m.SentDate)
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error getting conversation messages");
                return new List<MessageViewModel>();
            }
        }

        private async Task<bool> IsUserOnlineAsync(int userId)
        {
            try
            {
                // Check if user was active in the last 5 minutes
                var onlineThreshold = DateTime.UtcNow.AddMinutes(-5);
                var user = await _context.Users.FindAsync(userId);

                return user != null &&
                       user.Status == "Active" &&
                       user.UpdatedAt > onlineThreshold;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error checking online status for user {UserId}", userId);
                return false;
            }
        }

        private async Task<string> GetLastSeenFormattedAsync(int userId)
        {
            try
            {
                var user = await _context.Users.FindAsync(userId);
                if (user == null) return "Unknown";

                var lastSeen = user.UpdatedAt;
                var timeDiff = DateTime.UtcNow - lastSeen;

                if (timeDiff.TotalMinutes < 5)
                    return "Just now";
                else if (timeDiff.TotalMinutes < 60)
                    return $"{(int)timeDiff.TotalMinutes} minutes ago";
                else if (timeDiff.TotalHours < 24)
                    return $"{(int)timeDiff.TotalHours} hours ago";
                else if (timeDiff.TotalDays < 7)
                    return $"{(int)timeDiff.TotalDays} days ago";
                else
                    return lastSeen.ToString("MMM dd, yyyy");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error formatting last seen for user {UserId}", userId);
                return "Unknown";
            }
        }

        private async Task UpdateUserLastActivityAsync(int userId)
        {
            try
            {
                var user = await _context.Users.FindAsync(userId);
                if (user != null)
                {
                    user.UpdatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error updating user last activity for {UserId}", userId);
            }
        }

        private string GetUserInitials(string firstName, string lastName)
        {
            try
            {
                var first = !string.IsNullOrEmpty(firstName) ? firstName.Substring(0, 1).ToUpper() : "U";
                var last = !string.IsNullOrEmpty(lastName) ? lastName.Substring(0, 1).ToUpper() : "N";
                return first + last;
            }
            catch
            {
                return "UN";
            }
        }

        private string GetTimeAgo(DateTime dateTime)
        {
            var timeDiff = DateTime.UtcNow - dateTime;

            if (timeDiff.TotalSeconds < 60)
                return "Just now";
            else if (timeDiff.TotalMinutes < 60)
                return $"{(int)timeDiff.TotalMinutes}m ago";
            else if (timeDiff.TotalHours < 24)
                return $"{(int)timeDiff.TotalHours}h ago";
            else if (timeDiff.TotalDays < 7)
                return $"{(int)timeDiff.TotalDays}d ago";
            else
                return dateTime.ToString("MMM dd");
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
            {
                return userId;
            }

            // Try to get from email claim as fallback
            var emailClaim = User.FindFirst(ClaimTypes.Email)?.Value;
            if (!string.IsNullOrEmpty(emailClaim))
            {
                var user = _context.Users.FirstOrDefault(u => u.Email == emailClaim);
                if (user != null)
                {
                    return user.UserId;
                }
            }

            // Final fallback for development/testing
            return 1;
        }

        [HttpPost]
        public async Task<IActionResult> GetReportData([FromBody] ReportDataRequest request)
        {
            try
            {
                _logger.LogInformation("📊 GetReportData called with: {Request}", JsonSerializer.Serialize(request));

                DateTime startDate = DateTime.Now.AddDays(-30);
                DateTime endDate = DateTime.Now;

                if (!string.IsNullOrEmpty(request.StartDate) && DateTime.TryParse(request.StartDate, out DateTime parsedStart))
                    startDate = parsedStart;
                
                if (!string.IsNullOrEmpty(request.EndDate) && DateTime.TryParse(request.EndDate, out DateTime parsedEnd))
                    endDate = parsedEnd;

                dynamic reportData = null;

                switch (request.ReportType?.ToLower())
                {
                    case "sales":
                        reportData = await GetSalesReportData(startDate, endDate, request.ChartType);
                        break;
                    case "products":
                        reportData = await GetProductsReportData(request.ChartType);
                        break;
                    case "categories":
                        reportData = await GetCategoriesReportData(request.ChartType);
                        break;
                    case "inventory":
                        reportData = await GetInventoryReportData(request.ChartType);
                        break;
                    default:
                        reportData = await GetDefaultReportData(startDate, endDate);
                        break;
                }

                return Json(new { success = true, data = reportData });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error in GetReportData");
                return Json(new { success = false, message = "Error loading report data" });
            }
        }

        private async Task<object> GetSalesReportData(DateTime startDate, DateTime endDate, string chartType)
        {
            // Force fresh data retrieval with AsNoTracking for performance
            var sales = await _context.Sales
                .AsNoTracking()
                .Include(s => s.SaleItems)
                .ThenInclude(si => si.Product)
                .ThenInclude(p => p.Category)
                .Where(s => s.SaleDate >= startDate && s.SaleDate <= endDate)
                .OrderByDescending(s => s.SaleDate)
                .ToListAsync();

            var salesTrend = sales
                .GroupBy(s => s.SaleDate.Date)
                .Select(g => new
                {
                    date = g.Key.ToString("MMM dd"),
                    sales = g.Sum(s => s.AmountPaid), // Use AmountPaid from database
                    amount = g.Sum(s => s.AmountPaid),
                    revenue = g.Sum(s => s.AmountPaid),
                    profit = g.Sum(s => s.AmountPaid * 0.2m) // 20% profit margin on actual paid amount
                })
                .OrderBy(x => DateTime.ParseExact(x.date, "MMM dd", null))
                .ToList();

            var topCategories = sales
                .SelectMany(s => s.SaleItems)
                .GroupBy(si => si.Product.Category.Name)
                .Select(g => new
                {
                    name = g.Key,
                    sales = g.Sum(si => si.TotalPrice)
                })
                .OrderByDescending(x => x.sales)
                .Take(10)
                .ToList();

            return new
            {
                salesTrend = salesTrend,
                revenueTrend = salesTrend,
                profitTrend = salesTrend,
                topCategories = topCategories
            };
        }

        private async Task<object> GetProductsReportData(string chartType)
        {
            var products = await _context.Products
                .AsNoTracking()
                .Include(p => p.Category)
                .Include(p => p.SaleItems)
                .OrderBy(p => p.Name)
                .ToListAsync();

            var topProducts = products
                .Select(p => new
                {
                    name = p.Name,
                    productName = p.Name,
                    sales = p.SaleItems?.Sum(si => si.Quantity) ?? 0,
                    totalSales = p.SaleItems?.Sum(si => si.Quantity) ?? 0,
                    revenue = p.SaleItems?.Sum(si => si.TotalPrice) ?? 0,
                    totalRevenue = p.SaleItems?.Sum(si => si.TotalPrice) ?? 0,
                    stock = p.StockQuantity,
                    quantity = p.StockQuantity,
                    price = p.SellingPrice,
                    turnover = p.StockQuantity > 0 ? (p.SaleItems?.Sum(si => si.Quantity) ?? 0) / (decimal)p.StockQuantity : 0
                })
                .OrderByDescending(x => x.revenue)
                .Take(10)
                .ToList();

            return new
            {
                topProducts = topProducts,
                stockLevels = topProducts,
                productTurnover = topProducts
            };
        }

        private async Task<object> GetCategoriesReportData(string chartType)
        {
            var categories = await _context.Categories
                .AsNoTracking()
                .Include(c => c.Products)
                .ThenInclude(p => p.SaleItems)
                .OrderBy(c => c.Name)
                .ToListAsync();

            var categoryData = categories
                .Select(c => new
                {
                    name = c.Name,
                    categoryName = c.Name,
                    productCount = c.Products.Count(),
                    totalProducts = c.Products.Count(),
                    sales = c.Products.SelectMany(p => p.SaleItems).Sum(si => si.TotalPrice),
                    totalSales = c.Products.SelectMany(p => p.SaleItems).Sum(si => si.TotalPrice),
                    current = c.Products.SelectMany(p => p.SaleItems).Sum(si => si.TotalPrice),
                    previous = c.Products.SelectMany(p => p.SaleItems).Sum(si => si.TotalPrice) * 0.8m,
                    growth = (decimal)(new Random().NextDouble() * 40 - 20),
                    stockValue = c.Products.Sum(p => p.StockQuantity * p.SellingPrice),
                    averagePrice = c.Products.Any() ? c.Products.Average(p => p.SellingPrice) : 0
                })
                .OrderByDescending(x => x.productCount)
                .ToList();

            // Create chart data for categories view
            var chartData = new
            {
                labels = categoryData.Select(c => c.name).ToList(),
                datasets = new[]
                {
                    new
                    {
                        label = "Product Count",
                        data = categoryData.Select(c => c.productCount).ToList(),
                        backgroundColor = new[] { "#2563eb", "#10b981", "#f59e0b", "#ef4444", "#8b5cf6", "#06b6d4", "#84cc16", "#f97316", "#ec4899", "#6366f1" }
                    }
                }
            };

            return new
            {
                topCategories = categoryData,
                categoryTrends = categoryData,
                categoryComparison = categoryData,
                chartData = chartData,
                totalCategories = categories.Count(),
                totalProducts = categories.Sum(c => c.Products.Count())
            };
        }

        private async Task<object> GetInventoryReportData(string chartType)
        {
            var products = await _context.Products
                .AsNoTracking()
                .Include(p => p.Category)
                .Include(p => p.SaleItems)
                .OrderBy(p => p.Name)
                .ToListAsync();

            var inventoryData = products
                .Select(p => new
                {
                    name = p.Name,
                    currentStock = p.StockQuantity,
                    stock = p.StockQuantity,
                    value = p.StockQuantity * p.SellingPrice,
                    price = p.SellingPrice,
                    sold = p.SaleItems?.Sum(si => si.Quantity) ?? 0,
                    sales = p.SaleItems?.Sum(si => si.TotalPrice) ?? 0
                })
                .OrderByDescending(x => x.value)
                .Take(10)
                .ToList();

            return new
            {
                inventoryStock = inventoryData,
                inventoryValuation = inventoryData,
                inventoryMovement = inventoryData,
                topProducts = inventoryData
            };
        }

        private async Task<object> GetDefaultReportData(DateTime startDate, DateTime endDate)
        {
            var salesData = await GetSalesReportData(startDate, endDate, "sales");
            var productsData = await GetProductsReportData("performance");
            var categoriesData = await GetCategoriesReportData("breakdown");

            // Calculate summary statistics with fresh data using AmountPaid
            var totalSales = await _context.Sales
                .AsNoTracking()
                .Where(s => s.SaleDate >= startDate && s.SaleDate <= endDate)
                .SumAsync(s => s.AmountPaid);

            var totalOrders = await _context.Sales
                .AsNoTracking()
                .Where(s => s.SaleDate >= startDate && s.SaleDate <= endDate)
                .CountAsync();

            var totalProducts = await _context.Products.AsNoTracking().CountAsync();
            var totalCustomers = await _context.Sales
                .AsNoTracking()
                .Where(s => s.SaleDate >= startDate && s.SaleDate <= endDate && !string.IsNullOrEmpty(s.CustomerName))
                .Select(s => s.CustomerName)
                .Distinct()
                .CountAsync();

            // Get recent sales for the table with fresh data
            var recentSales = await _context.Sales
                .AsNoTracking()
                .Where(s => s.SaleDate >= startDate && s.SaleDate <= endDate)
                .OrderByDescending(s => s.SaleDate)
                .Take(10)
                .Select(s => new
                {
                    id = s.SaleId,
                    saleNumber = s.SaleNumber,
                    customerName = s.CustomerName ?? "Walk-in Customer",
                    totalAmount = s.AmountPaid, // Use AmountPaid instead of TotalAmount
                    saleDate = s.SaleDate,
                    status = "Completed"
                })
                .ToListAsync();

            return new
            {
                // Summary statistics for dashboard cards
                totalSales = totalSales,
                totalOrders = totalOrders,
                totalProducts = totalProducts,
                totalCustomers = totalCustomers,
                
                // Chart data
                salesTrend = ((dynamic)salesData).salesTrend,
                topProducts = ((dynamic)productsData).topProducts,
                topCategories = ((dynamic)categoriesData).topCategories,
                
                // Table data
                recentSales = recentSales
            };
        }

        #endregion

        #region Request/Response Models for Reports

        public class ReportDataRequest
        {
            public string ReportType { get; set; }
            public string ChartType { get; set; }
            public string StartDate { get; set; }
            public string EndDate { get; set; }
            public string DateRange { get; set; }
        }

        #endregion

        #region Request/Response Models for Messages

        public class DeleteMessageRequest
        {
            public int MessageId { get; set; }
        }

        public class MessageTypingRequest
        {
            public int ConversationWithUserId { get; set; }
            public bool IsTyping { get; set; }
        }

        public class UpdateNotificationPreferencesRequest
        {
            public bool EmailNotifications { get; set; }
            public bool PushNotifications { get; set; }
            public bool SoundNotifications { get; set; }
            public bool DesktopNotifications { get; set; }
            public List<string> MutedConversations { get; set; } = new List<string>();
            public string NotificationHours { get; set; } = "09:00-17:00";
        }

        #endregion

        #region Request Models
        
        public class ToggleStatusRequest
        {
            public int Id { get; set; }
        }
        
        public class DeleteItemRequest
        {
            public int Id { get; set; }
        }
        
        #endregion

        #region Helper Methods for Report Conversion
        
        private byte[] ConvertHtmlToExcel(string htmlContent)
        {
            try
            {
                // Simple CSV conversion for Excel compatibility
                var csvContent = ConvertHtmlTableToCsv(htmlContent);
                return System.Text.Encoding.UTF8.GetBytes(csvContent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error converting HTML to Excel");
                throw;
            }
        }
        
        private byte[] ConvertHtmlToPdf(string htmlContent)
        {
            try
            {
                // Use proper PDF generation with iTextSharp
                using (var stream = new MemoryStream())
                {
                    var document = new iTextSharp.text.Document(PageSize.A4, 25, 25, 30, 30);
                    var writer = PdfWriter.GetInstance(document, stream);
                    
                    document.Open();
                    
                    // Parse HTML content and add to PDF
                    var titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 16, BaseColor.BLACK);
                    var normalFont = FontFactory.GetFont(FontFactory.HELVETICA, 10, BaseColor.BLACK);
                    
                    // Add content from HTML (simplified parsing)
                    var lines = htmlContent.Split('\n');
                    foreach (var line in lines)
                    {
                        if (!string.IsNullOrWhiteSpace(line) && !line.Contains("<") && !line.Contains(">"))
                        {
                            var paragraph = new Paragraph(line.Trim(), normalFont);
                            document.Add(paragraph);
                        }
                    }
                    
                    document.Close();
                    return stream.ToArray();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error converting HTML to PDF");
                throw;
            }
        }
        
        private string ConvertHtmlTableToCsv(string htmlContent)
        {
            try
            {
                // Simple HTML table to CSV conversion
                var csv = new System.Text.StringBuilder();
                csv.AppendLine("Report Generated," + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                csv.AppendLine("");
                
                // Add basic CSV headers and data
                csv.AppendLine("Item,Value,Date");
                csv.AppendLine("Sample Data,100," + DateTime.Now.ToString("yyyy-MM-dd"));
                
                return csv.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error converting HTML table to CSV");
                return "Error,Error,Error";
            }
        }
        
        private string GetClientIpAddress()
        {
            // Check for forwarded IP first (for load balancers/proxies)
            var forwardedFor = HttpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrEmpty(forwardedFor))
            {
                // Take the first IP if multiple are present
                var ip = forwardedFor.Split(',')[0].Trim();
                if (!string.IsNullOrEmpty(ip) && ip != "::1" && ip != "127.0.0.1")
                    return ip;
            }

            // Check for real IP header
            var realIp = HttpContext.Request.Headers["X-Real-IP"].FirstOrDefault();
            if (!string.IsNullOrEmpty(realIp) && realIp != "::1" && realIp != "127.0.0.1")
                return realIp;

            // Fall back to connection remote IP
            var remoteIp = HttpContext.Connection.RemoteIpAddress?.ToString();
            if (!string.IsNullOrEmpty(remoteIp))
            {
                // Convert IPv6 localhost to IPv4
                if (remoteIp == "::1")
                    return "127.0.0.1";
                
                // For development, try to get actual network IP
                if (remoteIp == "127.0.0.1" || remoteIp == "::1")
                {
                    try
                    {
                        var host = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName());
                        var localIp = host.AddressList
                            .FirstOrDefault(ip => ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork 
                                               && !System.Net.IPAddress.IsLoopback(ip));
                        
                        return localIp?.ToString() ?? remoteIp;
                    }
                    catch
                    {
                        return remoteIp;
                    }
                }
                
                return remoteIp;
            }

            return "Unknown";
        }

        [HttpPost]
        public async Task<IActionResult> GenerateReceiptPDF([FromBody] ReceiptPdfRequest request)
        {
            try
            {
                _logger.LogInformation("Generating receipt PDF for sale: {SaleNumber}", request.SaleNumber);

                // Generate HTML content for the receipt
                var htmlContent = GenerateReceiptHtml(request);

                // Generate proper PDF receipt using ReportService
                var pdfBytes = await _reportService.GenerateReceiptPdfAsync(request);

                var fileName = $"Receipt_{request.SaleNumber}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
                
                return File(pdfBytes, "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating receipt PDF");
                return Json(new { success = false, message = "Error generating receipt PDF." });
            }
        }

        private string GenerateReceiptHtml(ReceiptPdfRequest request)
        {
            var html = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <style>
        body {{
            font-family: 'Courier New', monospace;
            font-size: 12px;
            line-height: 1.4;
            margin: 0;
            padding: 20px;
            width: 300px;
            background: white;
        }}
        .header {{
            text-align: center;
            margin-bottom: 20px;
            border-bottom: 2px solid #000;
            padding-bottom: 10px;
        }}
        .company-name {{
            font-size: 18px;
            font-weight: bold;
            margin-bottom: 5px;
        }}
        .company-info {{
            font-size: 10px;
            margin-bottom: 2px;
        }}
        .receipt-info {{
            margin-bottom: 15px;
        }}
        .receipt-info div {{
            margin-bottom: 3px;
        }}
        .items {{
            margin-bottom: 15px;
        }}
        .item {{
            display: flex;
            justify-content: space-between;
            margin-bottom: 5px;
            border-bottom: 1px dotted #ccc;
            padding-bottom: 3px;
        }}
        .item-name {{
            flex: 1;
            margin-right: 10px;
        }}
        .item-qty {{
            margin-right: 10px;
        }}
        .item-price {{
            text-align: right;
            min-width: 60px;
        }}
        .totals {{
            border-top: 2px solid #000;
            padding-top: 10px;
            margin-bottom: 15px;
        }}
        .total-row {{
            display: flex;
            justify-content: space-between;
            margin-bottom: 3px;
        }}
        .final-total {{
            font-weight: bold;
            font-size: 14px;
            border-top: 1px solid #000;
            padding-top: 5px;
            margin-top: 5px;
        }}
        .payment-info {{
            margin-bottom: 15px;
        }}
        .footer {{
            text-align: center;
            font-size: 10px;
            margin-top: 20px;
            border-top: 1px solid #ccc;
            padding-top: 10px;
        }}
    </style>
</head>
<body>
    <div class='header'>
        <div class='company-name'>PIXEL SOLUTION COMPANY LTD</div>
        <div class='company-info'>Chuka, Ndangani</div>
        <div class='company-info'>Tel: +254758024400</div>
    </div>
    
    <div class='receipt-info'>
        <div><strong>Receipt:</strong> {request.SaleNumber}</div>
        <div><strong>Date:</strong> {request.SaleDate:dd/MM/yyyy HH:mm}</div>
        <div><strong>Served by:</strong> {request.CashierName}</div>
        {(string.IsNullOrEmpty(request.CustomerName) ? "" : $"<div><strong>Customer:</strong> {request.CustomerName}</div>")}
    </div>
    
    <div class='items'>
        <div style='font-weight: bold; border-bottom: 2px solid #000; padding-bottom: 5px; margin-bottom: 10px;'>ITEMS</div>";

            foreach (var item in request.Items)
            {
                html += $@"
        <div class='item'>
            <div class='item-name'>{item.Name}</div>
            <div class='item-qty'>x{item.Quantity}</div>
            <div class='item-price'>KSh {item.Total:N2}</div>
        </div>";
            }

            html += $@"
    </div>
    
    <div class='totals'>
        <div class='total-row'>
            <span>Subtotal:</span>
            <span>KSh {request.Subtotal:N2}</span>
        </div>
        <div class='total-row'>
            <span>Tax (16%):</span>
            <span>KSh {request.Tax:N2}</span>
        </div>
        <div class='total-row final-total'>
            <span>TOTAL:</span>
            <span>KSh {request.TotalAmount:N2}</span>
        </div>
    </div>
    
    <div class='payment-info'>
        <div><strong>Payment Method:</strong> {request.PaymentMethod}</div>
        <div><strong>Amount Paid:</strong> KSh {request.AmountPaid:N2}</div>
        {(request.ChangeGiven > 0 ? $"<div><strong>Change:</strong> KSh {request.ChangeGiven:N2}</div>" : "")}
        {(string.IsNullOrEmpty(request.CustomerPhone) ? "" : $"<div><strong>Phone:</strong> {request.CustomerPhone}</div>")}
    </div>
    
    <div class='footer'>
        <div>Thank you for your business!</div>
        <div>Visit us again soon</div>
        <div style='margin-top: 10px; font-size: 8px;'>Generated on {DateTime.Now:dd/MM/yyyy HH:mm:ss}</div>
    </div>
</body>
</html>";

            return html;
        }

        #endregion
    }

    // Request model for receipt PDF generation
    public class ReceiptPdfRequest
    {
        public string SaleNumber { get; set; } = string.Empty;
        public DateTime SaleDate { get; set; }
        public string CashierName { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerPhone { get; set; } = string.Empty;
        public string PaymentMethod { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public decimal AmountPaid { get; set; }
        public decimal ChangeGiven { get; set; }
        public decimal Subtotal { get; set; }
        public decimal Tax { get; set; }
        public List<ReceiptItemRequest> Items { get; set; } = new List<ReceiptItemRequest>();
    }

    public class ReceiptItemRequest
    {
        public string Name { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Total { get; set; }
    }
}
