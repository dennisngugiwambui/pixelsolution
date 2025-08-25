using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using PixelSolution.Services.Interfaces;
using PixelSolution.ViewModels;
using PixelSolution.Models;
using PixelSolution.Data;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using PixelSolution.Services;

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
        private readonly IActivityLogService _activityLogService;

        public EmployeeController(
            IReportService reportService, 
            ISaleService saleService,
            IProductService productService,
            ICategoryService categoryService,
            ApplicationDbContext context,
            ILogger<EmployeeController> logger,
            IActivityLogService activityLogService)
        {
            _reportService = reportService;
            _saleService = saleService;
            _productService = productService;
            _categoryService = categoryService;
            _context = context;
            _logger = logger;
            _activityLogService = activityLogService;
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "0");
                
                // Log employee dashboard access
                await _activityLogService.LogActivityAsync(
                    userId, 
                    "Dashboard Access", 
                    "Employee accessed dashboard",
                    "Dashboard",
                    null,
                    new { Page = "Employee Dashboard" },
                    HttpContext.Connection.RemoteIpAddress?.ToString(),
                    Request.Headers["User-Agent"]
                );
                
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
                var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "0");
                
                // Log employee sales page access
                await _activityLogService.LogActivityAsync(
                    userId, 
                    "Sales Page Access", 
                    "Employee accessed sales page",
                    "Sales",
                    null,
                    new { Page = "Sales Management" },
                    HttpContext.Connection.RemoteIpAddress?.ToString(),
                    Request.Headers["User-Agent"]
                );
                
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
                
                // Log employee messages access
                await _activityLogService.LogActivityAsync(
                    currentUserId, 
                    "Messages Access", 
                    "Employee accessed messages",
                    "Messages",
                    null,
                    new { Page = "Internal Messages", TargetUserId = userId },
                    HttpContext.Connection.RemoteIpAddress?.ToString(),
                    Request.Headers["User-Agent"]
                );
                
                // Get all users for messaging (excluding current user)
                var allUsers = await _context.Users
                    .Where(u => u.UserId != currentUserId)
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
            try
            {
                // Get all unique user IDs that the current user has conversations with
                var conversationUserIds = await _context.Messages
                    .Where(m => m.FromUserId == currentUserId || m.ToUserId == currentUserId)
                    .Select(m => m.FromUserId == currentUserId ? m.ToUserId : m.FromUserId)
                    .Where(userId => userId != currentUserId)
                    .Distinct()
                    .ToListAsync();

                var conversations = new List<ConversationViewModel>();

                foreach (var userId in conversationUserIds)
                {
                    // Get user details
                    var user = await _context.Users
                        .Where(u => u.UserId == userId)
                        .FirstOrDefaultAsync();

                    if (user == null) continue;

                    // Get last message between current user and this user
                    var lastMessage = await _context.Messages
                        .Where(m => (m.FromUserId == currentUserId && m.ToUserId == userId) ||
                                   (m.FromUserId == userId && m.ToUserId == currentUserId))
                        .OrderByDescending(m => m.SentDate)
                        .FirstOrDefaultAsync();

                    if (lastMessage == null) continue;

                    // Get unread count (messages from this user to current user that are unread)
                    var unreadCount = await _context.Messages
                        .Where(m => m.FromUserId == userId && m.ToUserId == currentUserId && !m.IsRead)
                        .CountAsync();

                    conversations.Add(new ConversationViewModel
                    {
                        UserId = user.UserId,
                        FullName = user.FirstName + " " + user.LastName,
                        UserInitials = (user.FirstName.Substring(0, 1) + user.LastName.Substring(0, 1)).ToUpper(),
                        LastMessage = lastMessage.Content.Length > 50 ? lastMessage.Content.Substring(0, 50) + "..." : lastMessage.Content,
                        LastMessageTime = lastMessage.SentDate.ToString("MMM dd, HH:mm"),
                        IsOnline = user.IsActive, // Use IsActive as online status
                        UnreadCount = unreadCount,
                        LastSeen = DateTime.Now.AddMinutes(-30), // Mock data
                        LastSeenFormatted = "30 min ago"
                    });
                }

                // Sort by last message time (most recent first)
                return conversations.OrderByDescending(c => c.LastMessageTime).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetUserConversationsAsync");
                return new List<ConversationViewModel>();
            }
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

        public async Task<IActionResult> Settings()
        {
            try
            {
                var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "0");
                
                // Log employee settings access
                await _activityLogService.LogActivityAsync(
                    userId, 
                    "Settings Access", 
                    "Employee accessed settings page",
                    "Settings",
                    null,
                    new { Page = "Employee Settings" },
                    HttpContext.Connection.RemoteIpAddress?.ToString(),
                    Request.Headers["User-Agent"]
                );
                
                var user = await _context.Users.FindAsync(userId);
                
                if (user == null)
                {
                    return NotFound();
                }
                
                return View(user);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading settings for employee");
                return View();
            }
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

                var employeeId = int.Parse(userId);
                
                // Log employee sale processing
                await _activityLogService.LogActivityAsync(
                    employeeId, 
                    "Sale Processing", 
                    $"Employee processing sale with {request.Items.Count} items, total: {request.TotalAmount:C}",
                    "Sale",
                    null,
                    new { 
                        ItemCount = request.Items.Count, 
                        TotalAmount = request.TotalAmount,
                        PaymentMethod = request.PaymentMethod,
                        CustomerPhone = request.CustomerPhone
                    },
                    HttpContext.Connection.RemoteIpAddress?.ToString(),
                    Request.Headers["User-Agent"]
                );

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

                // Log successful sale completion
                await _activityLogService.LogActivityAsync(
                    employeeId, 
                    "Sale Completed", 
                    $"Employee completed sale #{sale.SaleNumber} for {request.TotalAmount:C}",
                    "Sale",
                    sale.SaleId,
                    new { 
                        SaleNumber = sale.SaleNumber,
                        TotalAmount = request.TotalAmount,
                        PaymentMethod = request.PaymentMethod,
                        ItemCount = request.Items.Count
                    },
                    HttpContext.Connection.RemoteIpAddress?.ToString(),
                    Request.Headers["User-Agent"]
                );

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
                _logger.LogError(ex, "Error getting unread count");
                return Json(new { success = false, message = "Error getting unread count" });
            }
        }

        // Test endpoint to debug messages and users
        [HttpGet]
        public async Task<IActionResult> TestMessagesAndUsers()
        {
            try
            {
                var currentUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
                
                // Get all messages where current user is sender or receiver
                var userMessages = await _context.Messages
                    .Where(m => m.FromUserId == currentUserId || m.ToUserId == currentUserId)
                    .Select(m => new {
                        MessageId = m.MessageId,
                        FromUserId = m.FromUserId,
                        ToUserId = m.ToUserId,
                        Subject = m.Subject,
                        Content = m.Content.Length > 50 ? m.Content.Substring(0, 50) + "..." : m.Content,
                        SentDate = m.SentDate,
                        IsRead = m.IsRead
                    })
                    .OrderByDescending(m => m.SentDate)
                    .ToListAsync();

                // Get all users (for debugging dropdown issue)
                var allUsers = await _context.Users
                    .Where(u => u.UserId != currentUserId)
                    .Select(u => new {
                        UserId = u.UserId,
                        FullName = u.FirstName + " " + u.LastName,
                        Email = u.Email,
                        UserType = u.UserType,
                        IsActive = u.IsActive,
                        Status = u.Status
                    })
                    .ToListAsync();

                // Get current user info
                var currentUser = await _context.Users
                    .Where(u => u.UserId == currentUserId)
                    .Select(u => new {
                        UserId = u.UserId,
                        FullName = u.FirstName + " " + u.LastName,
                        Email = u.Email,
                        UserType = u.UserType
                    })
                    .FirstOrDefaultAsync();

                return Json(new { 
                    success = true, 
                    currentUser = currentUser,
                    messagesCount = userMessages.Count,
                    messages = userMessages,
                    availableUsersCount = allUsers.Count,
                    availableUsers = allUsers
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in test endpoint");
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetUsers()
        {
            try
            {
                var currentUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
                
                var users = await _context.Users
                    .Where(u => u.UserId != currentUserId && u.IsActive == true)
                    .Select(u => new {
                        UserId = u.UserId,
                        FullName = u.FirstName + " " + u.LastName,
                        Email = u.Email,
                        UserType = u.UserType,
                        UserInitials = (u.FirstName.Substring(0, 1) + u.LastName.Substring(0, 1)).ToUpper()
                    })
                    .OrderBy(u => u.FullName)
                    .ToListAsync();

                return Json(new { success = true, users });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting users for employee");
                return Json(new { success = false, message = "Error loading users" });
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
