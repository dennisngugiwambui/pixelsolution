using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using PixelSolution.Services.Interfaces;
using PixelSolution.ViewModels;
using PixelSolution.Models;
using PixelSolution.Data;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace PixelSolution.Controllers
{
    [Authorize(Roles = "Employee,Admin,Manager")]
    public class EmployeeController : Controller
    {
        private readonly IReportService _reportService;
        private readonly ISaleService _saleService;
        private readonly IProductService _productService;
        private readonly ICategoryService _categoryService;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<EmployeeController> _logger;

        public EmployeeController(
            IReportService reportService, 
            ISaleService saleService,
            IProductService productService,
            ICategoryService categoryService,
            ApplicationDbContext context,
            ILogger<EmployeeController> logger)
        {
            _reportService = reportService;
            _saleService = saleService;
            _productService = productService;
            _categoryService = categoryService;
            _context = context;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "0");
                var dashboardData = await _reportService.GetEmployeeDashboardDataAsync(userId);
                return View(dashboardData);
            }
            catch (Exception ex)
            {
                // Log error and return view with empty data
                var emptyData = new DashboardViewModel
                {
                    Stats = new DashboardStatsViewModel(),
                    Charts = new DashboardChartsViewModel(),
                    RecentSales = new List<RecentSaleViewModel>(),
                    SidebarCounts = new SidebarCountsViewModel()
                };
                return View(emptyData);
            }
        }

        public async Task<IActionResult> Sales()
        {
            try
            {
                var salesData = await _reportService.GetSalesPageDataAsync();
                return View(salesData);
            }
            catch (Exception ex)
            {
                var emptySalesData = new SalesPageViewModel
                {
                    TodaysSales = 0,
                    TodaysTransactions = 0,
                    AverageTransaction = 0
                };
                return View(emptySalesData);
            }
        }

        public async Task<IActionResult> Messages(int? userId)
        {
            try
            {
                var currentUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
                
                // Get all users for messaging (excluding current user)
                var allUsers = await _context.Users
                    .Where(u => u.UserId != currentUserId && u.IsActive)
                    .Select(u => new UserSelectViewModel
                    {
                        UserId = u.UserId,
                        FullName = u.FirstName + " " + u.LastName,
                        Email = u.Email,
                        UserType = u.UserType,
                        Status = u.Status,
                        IsOnline = u.IsActive,
                        UserInitials = u.FirstName.Substring(0, 1) + u.LastName.Substring(0, 1)
                    })
                    .ToListAsync();

                // Get conversations for current user
                var conversations = await GetUserConversationsAsync(currentUserId);

                ConversationViewModel? selectedConversation = null;
                List<MessageViewModel> messages = new List<MessageViewModel>();

                if (userId.HasValue && userId.Value > 0)
                {
                    selectedConversation = conversations.FirstOrDefault(c => c.UserId == userId.Value);
                    if (selectedConversation != null)
                    {
                        messages = await GetConversationMessagesAsync(currentUserId, userId.Value);
                    }
                }

                // Calculate unread message count for current user
                var unreadCount = await _context.Messages
                    .Where(m => m.ToUserId == currentUserId && !m.IsRead)
                    .CountAsync();

                var viewModel = new MessagesPageViewModel
                {
                    CurrentUserId = currentUserId,
                    Conversations = conversations,
                    SelectedConversation = selectedConversation,
                    Messages = messages,
                    AllUsers = allUsers,
                    UnreadCount = unreadCount
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading messages for employee");
                
                // Return empty model to prevent null reference
                var emptyModel = new MessagesPageViewModel
                {
                    CurrentUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0"),
                    Conversations = new List<ConversationViewModel>(),
                    Messages = new List<MessageViewModel>(),
                    AllUsers = new List<UserSelectViewModel>()
                };
                
                return View(emptyModel);
            }
        }

        private async Task<List<ConversationViewModel>> GetUserConversationsAsync(int currentUserId)
        {
            var sentMessages = _context.Messages
                .Where(m => m.FromUserId == currentUserId)
                .GroupBy(m => m.ToUserId)
                .Select(g => new { UserId = g.Key, LastMessage = g.OrderByDescending(m => m.SentDate).FirstOrDefault() });

            var receivedMessages = _context.Messages
                .Where(m => m.ToUserId == currentUserId)
                .GroupBy(m => m.FromUserId)
                .Select(g => new { UserId = g.Key, LastMessage = g.OrderByDescending(m => m.SentDate).FirstOrDefault() });

            var allConversations = await sentMessages.Union(receivedMessages)
                .GroupBy(c => c.UserId)
                .Select(g => g.OrderByDescending(c => c.LastMessage.SentDate).First())
                .Join(_context.Users,
                    c => c.UserId,
                    u => u.UserId,
                    (c, u) => new ConversationViewModel
                    {
                        UserId = u.UserId,
                        FullName = u.FirstName + " " + u.LastName,
                        UserInitials = (u.FirstName.Substring(0, 1) + u.LastName.Substring(0, 1)).ToUpper(),
                        LastMessage = c.LastMessage.Content.Length > 50 ? c.LastMessage.Content.Substring(0, 50) + "..." : c.LastMessage.Content,
                        LastMessageTime = c.LastMessage.SentDate.ToString("MMM dd, HH:mm"),
                        IsOnline = false, // You can implement online status logic here
                        UnreadCount = _context.Messages.Count(m => m.FromUserId == u.UserId && m.ToUserId == currentUserId && !m.IsRead),
                        LastSeen = DateTime.Now.AddMinutes(-30), // Mock data
                        LastSeenFormatted = "30 min ago"
                    })
                .OrderByDescending(c => c.LastMessageTime)
                .ToListAsync();

            return allConversations;
        }

        private async Task<List<MessageViewModel>> GetConversationMessagesAsync(int currentUserId, int otherUserId)
        {
            var messages = await _context.Messages
                .Where(m => (m.FromUserId == currentUserId && m.ToUserId == otherUserId) ||
                           (m.FromUserId == otherUserId && m.ToUserId == currentUserId))
                .OrderBy(m => m.SentDate)
                .Select(m => new MessageViewModel
                {
                    MessageId = m.MessageId,
                    Content = m.Content,
                    Subject = m.Subject,
                    MessageType = m.MessageType,
                    SentDate = m.SentDate,
                    IsRead = m.IsRead,
                    ReadDate = m.ReadDate,
                    IsFromCurrentUser = m.FromUserId == currentUserId
                })
                .ToListAsync();

            // Mark messages as read
            var unreadMessages = await _context.Messages
                .Where(m => m.FromUserId == otherUserId && m.ToUserId == currentUserId && !m.IsRead)
                .ToListAsync();

            foreach (var message in unreadMessages)
            {
                message.IsRead = true;
                message.ReadDate = DateTime.Now;
            }

            if (unreadMessages.Any())
            {
                await _context.SaveChangesAsync();
            }

            return messages;
        }

        public IActionResult Settings()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetDashboardData()
        {
            try
            {
                var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "0");
                var data = await _reportService.GetEmployeeDashboardDataAsync(userId);
                return Json(data);
            }
            catch (Exception ex)
            {
                return Json(new { error = "Failed to load dashboard data" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetProductsForSale()
        {
            try
            {
                _logger.LogInformation("Loading products for Employee sales");
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

                _logger.LogInformation("Successfully loaded {ProductCount} products for Employee", productList.Count);
                return Json(new { success = true, products = productList });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting products for Employee sale");
                return Json(new { success = false, message = "Error loading products." });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessSale([FromBody] SaleRequest request)
        {
            try
            {
                _logger.LogInformation("Employee processing sale with {ItemCount} items", request.Items?.Count ?? 0);

                if (request.Items == null || !request.Items.Any())
                {
                    return Json(new { success = false, message = "No items in sale" });
                }

                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Json(new { success = false, message = "User not authenticated" });
                }

                // Validate stock availability
                foreach (var item in request.Items)
                {
                    var product = await _context.Products.FindAsync(item.ProductId);
                    if (product == null)
                    {
                        return Json(new { success = false, message = $"Product with ID {item.ProductId} not found" });
                    }

                    if (product.StockQuantity < item.Quantity)
                    {
                        return Json(new { success = false, message = $"Insufficient stock for {product.Name}. Available: {product.StockQuantity}, Requested: {item.Quantity}" });
                    }

                    if (!product.IsActive)
                    {
                        return Json(new { success = false, message = $"Product {product.Name} is not active" });
                    }
                }

                // Create sale record
                var sale = new Sale
                {
                    UserId = int.Parse(userId),
                    SaleDate = DateTime.Now,
                    SaleNumber = $"SAL{DateTime.Now:yyyyMMddHHmmss}",
                    TotalAmount = request.TotalAmount,
                    AmountPaid = request.TotalAmount,
                    PaymentMethod = request.PaymentMethod,
                    CustomerPhone = request.CustomerPhone ?? "",
                    CashierName = User.Identity?.Name ?? "Employee",
                    ChangeGiven = request.PaymentMethod == "cash" && request.CashReceived.HasValue 
                        ? request.CashReceived.Value - request.TotalAmount : 0
                };

                _context.Sales.Add(sale);
                await _context.SaveChangesAsync(); // Save to get the SaleId

                // Create sale items
                foreach (var item in request.Items)
                {
                    var saleItem = new SaleItem
                    {
                        SaleId = sale.SaleId,
                        ProductId = item.ProductId,
                        Quantity = item.Quantity,
                        UnitPrice = item.UnitPrice,
                        TotalPrice = item.Total
                    };
                    _context.SaleItems.Add(saleItem);

                    // Update stock quantities
                    var product = await _context.Products.FindAsync(item.ProductId);
                    if (product != null)
                    {
                        product.StockQuantity -= item.Quantity;
                        _context.Products.Update(product);
                    }
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation("Employee sale processed successfully. Sale ID: {SaleId}", sale.SaleId);

                return Json(new { success = true, saleId = sale.SaleId, message = "Sale processed successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing employee sale");
                return Json(new { success = false, message = "An error occurred while processing the sale" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetCategories()
        {
            try
            {
                var categories = await _categoryService.GetActiveCategoriesAsync();
                return Json(new { 
                    success = true, 
                    categories = categories.Select(c => new { categoryId = c.CategoryId, name = c.Name })
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting categories for Employee");
                return Json(new { 
                    success = false, 
                    categories = new List<object>(),
                    message = "Failed to load categories: " + ex.Message
                });
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

                var stats = new
                {
                    totalSales = todaysSales.Sum(s => s.TotalAmount),
                    transactionCount = todaysSales.Count(),
                    averageTransaction = todaysSales.Any() ? todaysSales.Average(s => s.TotalAmount) : 0
                };

                return Json(new { success = true, stats = stats });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting today's sales stats for Employee");
                return Json(new { success = false, message = "Error loading sales statistics." });
            }
        }

        [HttpGet]
        public IActionResult GetNotifications()
        {
            try
            {
                // Return empty notifications for now to prevent errors
                return Json(new { notifications = 0, messages = 0 });
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetConversationMessages(int userId)
        {
            try
            {
                var currentUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
                
                var messages = await GetConversationMessagesAsync(currentUserId, userId);
                var conversation = await _context.Users
                    .Where(u => u.UserId == userId)
                    .Select(u => new ConversationViewModel
                    {
                        UserId = u.UserId,
                        FullName = u.FirstName + " " + u.LastName,
                        UserInitials = (u.FirstName.Substring(0, 1) + u.LastName.Substring(0, 1)).ToUpper(),
                        IsOnline = false,
                        LastSeenFormatted = "Recently"
                    })
                    .FirstOrDefaultAsync();

                return Json(new { success = true, messages, conversation });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting conversation messages for employee");
                return Json(new { success = false, message = "Error loading messages" });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendMessage([FromBody] SendMessageRequest request)
        {
            try
            {
                var currentUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
                
                var message = new Message
                {
                    FromUserId = currentUserId,
                    ToUserId = request.ToUserId,
                    Subject = request.Subject ?? "",
                    Content = request.Content,
                    MessageType = request.MessageType ?? "General",
                    SentDate = DateTime.Now,
                    IsRead = false
                };

                _context.Messages.Add(message);
                await _context.SaveChangesAsync();

                return Json(new { success = true, messageId = message.MessageId, message = "Message sent successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending message for employee");
                return Json(new { success = false, message = "Error sending message" });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendQuickMessage([FromBody] SendQuickMessageRequest request)
        {
            try
            {
                var currentUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
                
                var message = new Message
                {
                    FromUserId = currentUserId,
                    ToUserId = request.ToUserId,
                    Subject = request.Subject ?? "",
                    Content = request.Content,
                    MessageType = request.MessageType ?? "General",
                    SentDate = DateTime.Now,
                    IsRead = false
                };

                _context.Messages.Add(message);
                await _context.SaveChangesAsync();

                return Json(new { success = true, messageId = message.MessageId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending quick message for employee");
                return Json(new { success = false, message = "Error sending message" });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkMessagesAsRead([FromBody] MarkMessagesRequest request)
        {
            try
            {
                var currentUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
                
                var unreadMessages = await _context.Messages
                    .Where(m => m.FromUserId == request.UserId && m.ToUserId == currentUserId && !m.IsRead)
                    .ToListAsync();

                foreach (var message in unreadMessages)
                {
                    message.IsRead = true;
                    message.ReadDate = DateTime.Now;
                }

                if (unreadMessages.Any())
                {
                    await _context.SaveChangesAsync();
                }

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking messages as read for employee");
                return Json(new { success = false, message = "Error updating messages" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetConversationsList()
        {
            try
            {
                var currentUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
                var conversations = await GetUserConversationsAsync(currentUserId);
                
                return Json(new { success = true, conversations });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting conversations list for employee");
                return Json(new { success = false, message = "Error loading conversations" });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GenerateReceiptPDF([FromBody] ReceiptRequest request)
        {
            try
            {
                _logger.LogInformation("Employee generating receipt PDF");

                if (string.IsNullOrEmpty(request.ReceiptHtml))
                {
                    return BadRequest("Receipt HTML is required");
                }

                // Use the report service to generate PDF
                var pdfBytes = await _reportService.GenerateReceiptPDFAsync(request.ReceiptHtml);

                var fileName = request.FileName ?? $"Receipt_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";

                return File(pdfBytes, "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating receipt PDF for employee");
                return StatusCode(500, "Error generating PDF");
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetUnreadCount()
        {
            try
            {
                var currentUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
                
                var unreadCount = await _context.Messages
                    .Where(m => m.ToUserId == currentUserId && !m.IsRead)
                    .CountAsync();

                return Json(new { success = true, unreadCount = unreadCount });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting unread count for employee");
                return Json(new { success = false, message = "Error getting unread count" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetUserOnlineStatus(int userId)
        {
            try
            {
                var user = await _context.Users
                    .Where(u => u.UserId == userId)
                    .Select(u => new
                    {
                        u.UserId,
                        u.IsActive,
                        u.CreatedAt
                    })
                    .FirstOrDefaultAsync();

                if (user == null)
                {
                    return Json(new { success = false, message = "User not found" });
                }

                // Simple online status logic - for now, just return based on user status
                // In a real implementation, you would track actual login sessions
                var isOnline = user.IsActive;

                var lastSeen = user.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss");

                return Json(new 
                { 
                    success = true, 
                    isOnline = isOnline,
                    lastSeen = lastSeen
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user online status for userId: {UserId}", userId);
                return Json(new { success = false, message = "Error getting user status" });
            }
        }
    }

    // Request models for Employee messaging
    public class SendMessageRequest
    {
        public int ToUserId { get; set; }
        public string Subject { get; set; } = "";
        public string Content { get; set; } = "";
        public string MessageType { get; set; } = "General";
    }

    public class SendQuickMessageRequest
    {
        public int ToUserId { get; set; }
        public string Subject { get; set; } = "";
        public string Content { get; set; } = "";
        public string MessageType { get; set; } = "General";
    }

    public class MarkMessagesRequest
    {
        public int UserId { get; set; }
    }

    // Request models for Employee sales
    public class SaleRequest
    {
        public List<SaleItemRequest> Items { get; set; } = new List<SaleItemRequest>();
        public string PaymentMethod { get; set; } = "";
        public decimal TotalAmount { get; set; }
        public string? CustomerPhone { get; set; }
        public decimal? CashReceived { get; set; }
    }

    public class SaleItemRequest
    {
        public int ProductId { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Total { get; set; }
    }

    public class ReceiptRequest
    {
        public string ReceiptHtml { get; set; } = "";
        public string? FileName { get; set; }
    }
}
