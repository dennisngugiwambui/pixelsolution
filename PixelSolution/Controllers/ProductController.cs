using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PixelSolution.Services.Interfaces;
using PixelSolution.ViewModels;
using PixelSolution.Models;

namespace PixelSolution.Controllers
{
    [Authorize]
    public class ProductsController : Controller
    {
        private readonly IProductService _productService;
        private readonly ICategoryService _categoryService;
        private readonly ISupplierService _supplierService;
        private readonly IReportService _reportService;
        private readonly ILogger<ProductsController> _logger;

        public ProductsController(
            IProductService productService,
            ICategoryService categoryService,
            ISupplierService supplierService,
            IReportService reportService,
            ILogger<ProductsController> logger)
        {
            _productService = productService;
            _categoryService = categoryService;
            _supplierService = supplierService;
            _reportService = reportService;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                var products = await _productService.GetAllProductsAsync();
                return View(products);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading products");
                TempData["Error"] = "Error loading products.";
                return View(new List<Product>());
            }
        }

        [HttpGet]
        [Authorize(Roles = "Admin,Manager")]
        public async Task<IActionResult> Create()
        {
            try
            {
                var categories = await _categoryService.GetActiveCategoriesAsync();
                var suppliers = await _supplierService.GetActiveSuppliersAsync();

                ViewBag.Categories = categories;
                ViewBag.Suppliers = suppliers;

                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading create product form");
                TempData["Error"] = "Error loading form.";
                return RedirectToAction("Index");
            }
        }

        [HttpPost]
        [Authorize(Roles = "Admin,Manager")]
        public async Task<IActionResult> Create(CreateProductViewModel model)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var categories = await _categoryService.GetActiveCategoriesAsync();
                    var suppliers = await _supplierService.GetActiveSuppliersAsync();
                    ViewBag.Categories = categories;
                    ViewBag.Suppliers = suppliers;
                    return View(model);
                }

                // Check if SKU already exists
                var existingProduct = await _productService.GetProductBySkuAsync(model.SKU);
                if (existingProduct != null)
                {
                    ModelState.AddModelError("SKU", "A product with this SKU already exists.");
                    var categories = await _categoryService.GetActiveCategoriesAsync();
                    var suppliers = await _supplierService.GetActiveSuppliersAsync();
                    ViewBag.Categories = categories;
                    ViewBag.Suppliers = suppliers;
                    return View(model);
                }

                var product = new Product
                {
                    Name = model.Name,
                    Description = model.Description,
                    SKU = model.SKU,
                    CategoryId = model.CategoryId,
                    SupplierId = model.SupplierId,
                    BuyingPrice = model.BuyingPrice,
                    SellingPrice = model.SellingPrice,
                    StockQuantity = model.StockQuantity,
                    MinStockLevel = model.MinStockLevel,
                    ImageUrl = model.ImageUrl ?? string.Empty,
                    IsActive = true
                };

                await _productService.CreateProductAsync(product);
                TempData["Success"] = "Product created successfully!";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating product");
                TempData["Error"] = "Error creating product. Please try again.";
                var categories = await _categoryService.GetActiveCategoriesAsync();
                var suppliers = await _supplierService.GetActiveSuppliersAsync();
                ViewBag.Categories = categories;
                ViewBag.Suppliers = suppliers;
                return View(model);
            }
        }

        [HttpGet]
        [Authorize(Roles = "Admin,Manager")]
        public async Task<IActionResult> Edit(int id)
        {
            try
            {
                var product = await _productService.GetProductByIdAsync(id);
                if (product == null)
                {
                    TempData["Error"] = "Product not found.";
                    return RedirectToAction("Index");
                }

                var categories = await _categoryService.GetActiveCategoriesAsync();
                var suppliers = await _supplierService.GetActiveSuppliersAsync();
                ViewBag.Categories = categories;
                ViewBag.Suppliers = suppliers;

                var model = new EditProductViewModel
                {
                    ProductId = product.ProductId,
                    Name = product.Name,
                    Description = product.Description,
                    SKU = product.SKU,
                    CategoryId = product.CategoryId,
                    SupplierId = product.SupplierId,
                    BuyingPrice = product.BuyingPrice,
                    SellingPrice = product.SellingPrice,
                    StockQuantity = product.StockQuantity,
                    MinStockLevel = product.MinStockLevel,
                    ImageUrl = product.ImageUrl,
                    IsActive = product.IsActive
                };

                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading edit product form for ID {ProductId}", id);
                TempData["Error"] = "Error loading product details.";
                return RedirectToAction("Index");
            }
        }

        [HttpPost]
        [Authorize(Roles = "Admin,Manager")]
        public async Task<IActionResult> Edit(EditProductViewModel model)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var categories = await _categoryService.GetActiveCategoriesAsync();
                    var suppliers = await _supplierService.GetActiveSuppliersAsync();
                    ViewBag.Categories = categories;
                    ViewBag.Suppliers = suppliers;
                    return View(model);
                }

                var product = await _productService.GetProductByIdAsync(model.ProductId);
                if (product == null)
                {
                    TempData["Error"] = "Product not found.";
                    return RedirectToAction("Index");
                }

                // Check if SKU already exists for another product
                var existingProduct = await _productService.GetProductBySkuAsync(model.SKU);
                if (existingProduct != null && existingProduct.ProductId != model.ProductId)
                {
                    ModelState.AddModelError("SKU", "A product with this SKU already exists.");
                    var categories = await _categoryService.GetActiveCategoriesAsync();
                    var suppliers = await _supplierService.GetActiveSuppliersAsync();
                    ViewBag.Categories = categories;
                    ViewBag.Suppliers = suppliers;
                    return View(model);
                }

                product.Name = model.Name;
                product.Description = model.Description;
                product.SKU = model.SKU;
                product.CategoryId = model.CategoryId;
                product.SupplierId = model.SupplierId;
                product.BuyingPrice = model.BuyingPrice;
                product.SellingPrice = model.SellingPrice;
                product.StockQuantity = model.StockQuantity;
                product.MinStockLevel = model.MinStockLevel;
                product.ImageUrl = model.ImageUrl ?? string.Empty;
                product.IsActive = model.IsActive;

                await _productService.UpdateProductAsync(product);
                TempData["Success"] = "Product updated successfully!";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating product {ProductId}", model.ProductId);
                TempData["Error"] = "Error updating product. Please try again.";
                var categories = await _categoryService.GetActiveCategoriesAsync();
                var suppliers = await _supplierService.GetActiveSuppliersAsync();
                ViewBag.Categories = categories;
                ViewBag.Suppliers = suppliers;
                return View(model);
            }
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            try
            {
                var product = await _productService.GetProductByIdAsync(id);
                if (product == null)
                {
                    TempData["Error"] = "Product not found.";
                    return RedirectToAction("Index");
                }

                return View(product);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading product details for ID {ProductId}", id);
                TempData["Error"] = "Error loading product details.";
                return RedirectToAction("Index");
            }
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var result = await _productService.DeleteProductAsync(id);
                if (result)
                {
                    return Json(new { success = true, message = "Product deleted successfully!" });
                }
                else
                {
                    return Json(new { success = false, message = "Cannot delete product as it has associated sales records." });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting product {ProductId}", id);
                return Json(new { success = false, message = "Error deleting product. Please try again." });
            }
        }

        [HttpPost]
        [Authorize(Roles = "Admin,Manager")]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            try
            {
                var result = await _productService.ToggleProductStatusAsync(id);
                if (result)
                {
                    return Json(new { success = true, message = "Product status updated successfully!" });
                }
                else
                {
                    return Json(new { success = false, message = "Product not found." });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling product status {ProductId}", id);
                return Json(new { success = false, message = "Error updating product status. Please try again." });
            }
        }

        [HttpPost]
        [Authorize(Roles = "Admin,Manager")]
        public async Task<IActionResult> UpdateStock(int id, int quantity)
        {
            try
            {
                if (quantity < 0)
                {
                    return Json(new { success = false, message = "Stock quantity cannot be negative." });
                }

                var result = await _productService.UpdateStockAsync(id, quantity);
                if (result)
                {
                    return Json(new { success = true, message = "Stock updated successfully!" });
                }
                else
                {
                    return Json(new { success = false, message = "Product not found." });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating stock for product {ProductId}", id);
                return Json(new { success = false, message = "Error updating stock. Please try again." });
            }
        }

        [HttpGet]
        public async Task<IActionResult> LowStock()
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
                        category = p.Category.Name,
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

        [HttpGet]
        public async Task<IActionResult> Search(string search, int? categoryId)
        {
            try
            {
                var products = await _productService.GetActiveProductsAsync();

                if (!string.IsNullOrEmpty(search))
                {
                    products = products.Where(p =>
                        p.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                        p.SKU.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                        p.Description.Contains(search, StringComparison.OrdinalIgnoreCase))
                        .ToList();
                }

                if (categoryId.HasValue)
                {
                    products = products.Where(p => p.CategoryId == categoryId.Value).ToList();
                }

                var result = products.Select(p => new
                {
                    productId = p.ProductId,
                    name = p.Name,
                    sku = p.SKU,
                    description = p.Description,
                    category = p.Category.Name,
                    supplier = p.Supplier?.CompanyName,
                    buyingPrice = p.BuyingPrice,
                    sellingPrice = p.SellingPrice,
                    stockQuantity = p.StockQuantity,
                    minStockLevel = p.MinStockLevel,
                    isLowStock = p.IsLowStock,
                    isActive = p.IsActive,
                    profitMargin = p.ProfitMargin,
                    profitPercentage = p.ProfitPercentage
                });

                return Json(new { success = true, products = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching products");
                return Json(new { success = false, message = "Error searching products." });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetByCategory(int categoryId)
        {
            try
            {
                var products = await _productService.GetProductsByCategoryAsync(categoryId);
                var result = products.Select(p => new
                {
                    productId = p.ProductId,
                    name = p.Name,
                    sku = p.SKU,
                    sellingPrice = p.SellingPrice,
                    stockQuantity = p.StockQuantity
                });

                return Json(new { success = true, products = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting products by category {CategoryId}", categoryId);
                return Json(new { success = false, message = "Error loading products." });
            }
        }

        [HttpGet]
        public async Task<IActionResult> Inventory()
        {
            try
            {
                var report = await _reportService.GetInventoryReportAsync();
                return Json(report);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating inventory report");
                return Json(new { success = false, message = "Error generating inventory report." });
            }
        }
    }
}