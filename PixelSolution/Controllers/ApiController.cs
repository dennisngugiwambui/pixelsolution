using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PixelSolution.Services.Interfaces;
using System.Security.Claims;

namespace PixelSolution.Controllers
{
    [Route("api/[action]")]
    [ApiController]
    [Authorize]
    public class ApiController : ControllerBase
    {
        private readonly IReportService _reportService;
        private readonly ISaleService _saleService;
        private readonly IProductService _productService;
        private readonly IMessageService _messageService;
        private readonly IPurchaseRequestService _purchaseRequestService;
        private readonly ILogger<ApiController> _logger;

        public ApiController(
            IReportService reportService,
            ISaleService saleService,
            IProductService productService,
            IMessageService messageService,
            IPurchaseRequestService purchaseRequestService,
            ILogger<ApiController> logger)
        {
            _reportService = reportService;
            _saleService = saleService;
            _productService = productService;
            _messageService = messageService;
            _purchaseRequestService = purchaseRequestService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Dashboard()
        {
            try
            {
                var dashboardData = await _reportService.GetDashboardDataAsync();
                return Ok(dashboardData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading dashboard data");
                return StatusCode(500, new { error = "Failed to load dashboard data" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> Stats()
        {
            try
            {
                var today = DateTime.Today;
                var thisMonth = new DateTime(today.Year, today.Month, 1);

                var todaySales = await _saleService.GetTotalSalesAmountAsync(today, today.AddDays(1));
                var todaySalesCount = await _saleService.GetTotalSalesCountAsync(today, today.AddDays(1));
                var thisMonthSales = await _saleService.GetTotalSalesAmountAsync(thisMonth);
                var thisMonthSalesCount = await _saleService.GetTotalSalesCountAsync(thisMonth);

                var lowStockProducts = await _productService.GetLowStockProductsAsync();
                var pendingPurchaseRequests = await _purchaseRequestService.GetPurchaseRequestsByStatusAsync("Pending");

                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var unreadMessages = 0;
                if (int.TryParse(userIdClaim, out int userId))
                {
                    unreadMessages = await _messageService.GetUnreadCountAsync(userId);
                }

                return Ok(new
                {
                    totalSales = todaySales,
                    totalOrders = todaySalesCount,
                    productsSold = 0, // This would need to be calculated from SaleItems
                    newCustomers = 0, // This would need to be calculated based on your business logic
                    lowStockAlerts = lowStockProducts.Count(),
                    pendingRequests = pendingPurchaseRequests.Count(),
                    unreadMessages = unreadMessages,
                    thisMonthSales = thisMonthSales,
                    thisMonthOrders = thisMonthSalesCount
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading stats");
                return StatusCode(500, new { error = "Failed to load statistics" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> RecentSales(int count = 10)
        {
            try
            {
                var sales = await _saleService.GetAllSalesAsync();
                var recentSales = sales.Take(count).Select(s => new
                {
                    id = s.SaleId,
                    saleNumber = s.SaleNumber,
                    customerName = string.IsNullOrEmpty(s.CustomerName) ? "Walk-in Customer" : s.CustomerName,
                    productName = string.Join(", ", s.SaleItems.Take(2).Select(si => si.Product.Name)),
                    amount = s.TotalAmount,
                    status = s.Status,
                    date = s.SaleDate,
                    salesPerson = s.User.FullName
                });

                return Ok(new { success = true, sales = recentSales });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading recent sales");
                return StatusCode(500, new { error = "Failed to load recent sales" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> ChartData()
        {
            try
            {
                var salesAnalytics = await _saleService.GetSalesAnalyticsAsync();

                // Generate sample product data for the doughnut chart
                var productData = new[] { 78, 62, 51, 29, 15 };

                return Ok(new
                {
                    success = true,
                    salesData = salesAnalytics,
                    productData = productData
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading chart data");
                return StatusCode(500, new { error = "Failed to load chart data" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> Notifications()
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!int.TryParse(userIdClaim, out int userId))
                {
                    return BadRequest(new { error = "Invalid user" });
                }

                var unreadMessages = await _messageService.GetUnreadMessagesAsync(userId);
                var lowStockProducts = await _productService.GetLowStockProductsAsync();
                var pendingPurchaseRequests = await _purchaseRequestService.GetPurchaseRequestsByStatusAsync("Pending");

                var notifications = new List<object>();

                // Add message notifications
                foreach (var message in unreadMessages.Take(5))
                {
                    notifications.Add(new
                    {
                        id = message.MessageId,
                        type = "message",
                        title = message.Subject,
                        message = message.Content.Length > 100
                            ? message.Content.Substring(0, 100) + "..."
                            : message.Content,
                        from = message.FromUser.FullName,
                        date = message.SentDate,
                        icon = "fas fa-envelope"
                    });
                }

                // Add low stock notifications
                foreach (var product in lowStockProducts.Take(3))
                {
                    notifications.Add(new
                    {
                        id = product.ProductId,
                        type = "low-stock",
                        title = "Low Stock Alert",
                        message = $"{product.Name} is running low. Current stock: {product.StockQuantity}",
                        from = "System",
                        date = DateTime.UtcNow,
                        icon = "fas fa-exclamation-triangle"
                    });
                }

                // Add pending purchase request notifications (for admins/managers)
                if (User.IsInRole("Admin") || User.IsInRole("Manager"))
                {
                    foreach (var request in pendingPurchaseRequests.Take(3))
                    {
                        notifications.Add(new
                        {
                            id = request.PurchaseRequestId,
                            type = "purchase-request",
                            title = "Pending Purchase Request",
                            message = $"Purchase request {request.RequestNumber} requires approval",
                            from = request.User.FullName,
                            date = request.RequestDate,
                            icon = "fas fa-file-invoice"
                        });
                    }
                }

                return Ok(new
                {
                    success = true,
                    notifications = notifications.OrderByDescending(n => ((DateTime)n.GetType().GetProperty("date")!.GetValue(n)!)).Take(10),
                    counts = new
                    {
                        messages = unreadMessages.Count(),
                        lowStock = lowStockProducts.Count(),
                        pendingRequests = pendingPurchaseRequests.Count()
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading notifications");
                return StatusCode(500, new { error = "Failed to load notifications" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> MarkNotificationRead(int id, string type)
        {
            try
            {
                if (type == "message")
                {
                    var result = await _messageService.MarkAsReadAsync(id);
                    return Ok(new { success = result });
                }

                // For other notification types, you might want to implement
                // a separate notifications table to track read status
                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking notification as read");
                return StatusCode(500, new { error = "Failed to mark notification as read" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> Search(string query, string type = "all")
        {
            try
            {
                var results = new List<object>();

                if (type == "all" || type == "products")
                {
                    var products = await _productService.GetActiveProductsAsync();
                    var productResults = products
                        .Where(p => p.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                                   p.SKU.Contains(query, StringComparison.OrdinalIgnoreCase))
                        .Take(5)
                        .Select(p => new
                        {
                            id = p.ProductId,
                            type = "product",
                            title = p.Name,
                            subtitle = $"SKU: {p.SKU} | Stock: {p.StockQuantity}",
                            url = $"/Products/Details/{p.ProductId}",
                            icon = "fas fa-cube"
                        });
                    results.AddRange(productResults);
                }

                if (type == "all" || type == "sales")
                {
                    var sales = await _saleService.GetAllSalesAsync();
                    var salesResults = sales
                        .Where(s => s.SaleNumber.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                                   (!string.IsNullOrEmpty(s.CustomerName) && s.CustomerName.Contains(query, StringComparison.OrdinalIgnoreCase)))
                        .Take(5)
                        .Select(s => new
                        {
                            id = s.SaleId,
                            type = "sale",
                            title = $"Sale {s.SaleNumber}",
                            subtitle = $"Customer: {s.CustomerName ?? "Walk-in"} | Amount: KSh {s.TotalAmount:N2}",
                            url = $"/Sales/Details/{s.SaleId}",
                            icon = "fas fa-receipt"
                        });
                    results.AddRange(salesResults);
                }

                return Ok(new
                {
                    success = true,
                    results = results.Take(10),
                    query = query
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error performing search for query: {Query}", query);
                return StatusCode(500, new { error = "Search failed" });
            }
        }

        [HttpGet]
        public IActionResult Health()
        {
            return Ok(new
            {
                status = "healthy",
                timestamp = DateTime.UtcNow,
                version = "1.0.0",
                environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"
            });
        }
    }
}