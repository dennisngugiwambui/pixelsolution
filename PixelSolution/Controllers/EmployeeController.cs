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
    [Route("[controller]")]
    public class EmployeeController : Controller
    {
        private readonly IReportService _reportService;
        private readonly ISaleService _saleService;
        private readonly IProductService _productService;
        private readonly ICategoryService _categoryService;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<EmployeeController> _logger;
        private readonly IActivityLogService _activityLogService;
        private readonly IMpesaService _mpesaService;

        public EmployeeController(
            IReportService reportService, 
            ISaleService saleService,
            IProductService productService,
            ICategoryService categoryService,
            ApplicationDbContext context,
            ILogger<EmployeeController> logger,
            IActivityLogService activityLogService,
            IMpesaService mpesaService)
        {
            _reportService = reportService;
            _saleService = saleService;
            _productService = productService;
            _categoryService = categoryService;
            _context = context;
            _logger = logger;
            _activityLogService = activityLogService;
            _mpesaService = mpesaService;
        }

        [HttpGet("")]
        [HttpGet("Index")]
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

        [HttpGet("Sales")]
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

        [HttpGet("Messages")]
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

        [HttpGet("Settings")]
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

        [HttpGet("GetDashboardData")]
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

        [HttpGet("GetProductsForSale")]
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

        [HttpPost("ProcessSale")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessSale([FromBody] SaleRequest request)
        {
            try
            {
                _logger.LogInformation("Employee processing sale with {ItemCount} items, Payment Method: {PaymentMethod}", 
                    request.Items?.Count ?? 0, request.PaymentMethod);

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
                var currentUser = await _context.Users.FindAsync(employeeId);
                if (currentUser == null)
                {
                    return Json(new { success = false, message = "User not found" });
                }
                
                // Variable to store MPESA transaction data for callback tracking
                object? mpesaTransactionData = null;
                
                // Log employee sale processing
                await _activityLogService.LogActivityAsync(
                    employeeId, 
                    "Sale Processing", 
                    $"Employee processing sale with {request.Items.Count} items, total: KSh {request.TotalAmount:N2}",
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

                // Handle MPESA payments - Initiate STK Push BEFORE creating sale
                if (request.PaymentMethod.Equals("M-Pesa", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogInformation("🔵 Processing MPESA payment for amount: {Amount}, Phone: '{Phone}', Length: {Length}", 
                        request.TotalAmount, request.CustomerPhone, request.CustomerPhone?.Length ?? 0);
                    
                    if (string.IsNullOrEmpty(request.CustomerPhone))
                    {
                        return Json(new { success = false, message = "Phone number is required for MPESA payments." });
                    }

                    // Clean and format phone number
                    var formattedPhone = request.CustomerPhone?.Trim() ?? "";
                    
                    _logger.LogInformation("🔵 After trim: '{Phone}', Length: {Length}", formattedPhone, formattedPhone.Length);
                    
                    // Remove any non-digit characters
                    formattedPhone = new string(formattedPhone.Where(char.IsDigit).ToArray());
                    
                    _logger.LogInformation("🔵 After cleaning (digits only): '{Phone}', Length: {Length}", formattedPhone, formattedPhone.Length);
                    
                    // Format phone number if needed
                    if (!formattedPhone.StartsWith("254"))
                    {
                        // Remove leading 0 if present
                        if (formattedPhone.StartsWith("0"))
                        {
                            formattedPhone = "254" + formattedPhone.Substring(1);
                        }
                        else
                        {
                            formattedPhone = "254" + formattedPhone;
                        }
                    }

                    // Validate phone number format (should be 12 digits starting with 254)
                    if (formattedPhone.Length != 12 || !formattedPhone.StartsWith("254") || !formattedPhone.All(char.IsDigit))
                    {
                        _logger.LogError("Invalid phone format. Original: {Original}, Cleaned: {Cleaned}, Formatted: {Formatted}, Length: {Length}", 
                            request.CustomerPhone, new string(request.CustomerPhone.Where(char.IsDigit).ToArray()), formattedPhone, formattedPhone.Length);
                        return Json(new { success = false, message = $"Invalid phone number format. Expected 12 digits (254XXXXXXXXX), got {formattedPhone.Length} digits: {formattedPhone}" });
                    }

                    // Update the phone number to formatted version
                    request.CustomerPhone = formattedPhone;
                    _logger.LogInformation("Phone number formatted: {FormattedPhone}", formattedPhone);

                    try
                    {
                        // Validate amount (minimum only, no maximum limit)
                        if (request.TotalAmount < 1)
                        {
                            return Json(new { success = false, message = "Amount must be at least KSh 1" });
                        }

                        // Initiate STK Push FIRST - before creating sale record
                        _logger.LogInformation("📱 Initiating MPESA STK Push for phone: {Phone}, amount: {Amount}", request.CustomerPhone, request.TotalAmount);
                        
                        var stkPushResponse = await _mpesaService.InitiateStkPushAsync(
                            request.CustomerPhone,
                            request.TotalAmount,
                            "SALE" + DateTime.Now.ToString("yyyyMMddHHmmss").Substring(0, 8),
                            "Payment for purchase"
                        );
                        
                        _logger.LogInformation("📋 MPESA STK Push response: {Response}", System.Text.Json.JsonSerializer.Serialize(stkPushResponse));
                        
                        if (stkPushResponse == null)
                        {
                            _logger.LogError("❌ MPESA STK Push failed: Null response");
                            return Json(new { 
                                success = false, 
                                message = "MPESA Error: No response from service. Please try again.",
                                status = "stk_failed"
                            });
                        }
                        
                        // Validate STK Push response - ONLY proceed if successful
                        _logger.LogInformation("🔍 MPESA Response Details - Code: {Code}, Description: {Description}, CheckoutRequestID: {CheckoutRequestID}", 
                            stkPushResponse.ResponseCode, stkPushResponse.ResponseDescription, stkPushResponse.CheckoutRequestID);
                        
                        if (stkPushResponse.ResponseCode != "0")
                        {
                            var errorMsg = stkPushResponse.ResponseDescription ?? "Unknown MPESA error";
                            _logger.LogError("❌ MPESA STK Push failed: Code {Code}, Error: {Error}", stkPushResponse.ResponseCode, errorMsg);
                            return Json(new { 
                                success = false, 
                                message = $"MPESA Error ({stkPushResponse.ResponseCode}): {errorMsg}. Please try again.",
                                status = "stk_rejected"
                            });
                        }
                        
                        _logger.LogInformation("✅ MPESA STK Push initiated successfully. CheckoutRequestId: {CheckoutRequestId}", stkPushResponse.CheckoutRequestID);
                        
                        // Store MPESA transaction data for creating sale record
                        mpesaTransactionData = new
                        {
                            CheckoutRequestId = stkPushResponse.CheckoutRequestID,
                            MerchantRequestId = stkPushResponse.MerchantRequestID,
                            PhoneNumber = request.CustomerPhone,
                            Amount = request.TotalAmount,
                            Status = "STK_SENT"
                        };
                    }
                    catch (Exception mpesaEx)
                    {
                        _logger.LogError(mpesaEx, "MPESA STK Push failed: {ErrorMessage}", mpesaEx.Message);
                        
                        var mpesaErrorMessage = "MPESA service error";
                        if (mpesaEx.Message.Contains("401") || mpesaEx.Message.Contains("Unauthorized"))
                        {
                            mpesaErrorMessage = "MPESA authentication failed. Please check API credentials.";
                        }
                        else if (mpesaEx.Message.Contains("400") || mpesaEx.Message.Contains("Bad Request"))
                        {
                            mpesaErrorMessage = "Invalid MPESA request. Please check phone number format.";
                        }
                        else if (mpesaEx.Message.Contains("timeout") || mpesaEx.Message.Contains("network"))
                        {
                            mpesaErrorMessage = "MPESA service timeout. Please try again.";
                        }
                        else
                        {
                            mpesaErrorMessage = $"MPESA error: {mpesaEx.Message}";
                        }
                        
                        // Do NOT create sale if STK push fails
                        return Json(new { success = false, message = mpesaErrorMessage });
                    }
                }

                // Create sale record
                var sale = new Sale
                {
                    UserId = employeeId,
                    SaleDate = DateTime.UtcNow,
                    CashierName = $"{currentUser.FirstName} {currentUser.LastName}",
                    CustomerPhone = request.CustomerPhone ?? "",
                    PaymentMethod = request.PaymentMethod,
                    TotalAmount = request.TotalAmount, // CRITICAL: Set the total amount
                    AmountPaid = request.PaymentMethod.Equals("M-Pesa", StringComparison.OrdinalIgnoreCase) ? 0 : request.TotalAmount,
                    ChangeGiven = request.PaymentMethod.Equals("M-Pesa", StringComparison.OrdinalIgnoreCase) ? 0 : 
                                  (request.PaymentMethod == "Cash" && request.CashReceived.HasValue ? request.CashReceived.Value - request.TotalAmount : 0),
                    Status = request.PaymentMethod.Equals("M-Pesa", StringComparison.OrdinalIgnoreCase) ? "Pending" : "Completed",
                    SaleItems = new List<SaleItem>()
                };

                // Add sale items
                foreach (var item in request.Items)
                {
                    var saleItem = new SaleItem
                    {
                        ProductId = item.ProductId,
                        Quantity = item.Quantity,
                        UnitPrice = item.UnitPrice,
                        TotalPrice = item.Total
                    };
                    sale.SaleItems.Add(saleItem);
                }

                // Create sale using service (handles stock deduction logic)
                var createdSale = await _saleService.CreateSaleAsync(sale);
                _logger.LogInformation("Sale created successfully with ID: {SaleId}, Number: {SaleNumber}, Status: {Status}", 
                    createdSale.SaleId, createdSale.SaleNumber, createdSale.Status);

                // Store MPESA transaction information if this was an MPESA payment
                if (mpesaTransactionData != null && request.PaymentMethod.Equals("M-Pesa", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        var mpesaData = (dynamic)mpesaTransactionData;
                        var mpesaTransaction = new MpesaTransaction
                        {
                            SaleId = createdSale.SaleId,
                            CheckoutRequestId = mpesaData.CheckoutRequestId,
                            MerchantRequestId = mpesaData.MerchantRequestId,
                            PhoneNumber = mpesaData.PhoneNumber,
                            Amount = mpesaData.Amount,
                            Status = "STK_SENT",
                            CreatedAt = DateTime.UtcNow
                        };

                        _context.MpesaTransactions.Add(mpesaTransaction);
                        await _context.SaveChangesAsync();
                        
                        _logger.LogInformation("💾 MPESA transaction stored with ID: {TransactionId} for Sale: {SaleId} - Status: STK_SENT", 
                            mpesaTransaction.MpesaTransactionId, createdSale.SaleId);
                        
                        // Log M-Pesa sale initiation
                        await _activityLogService.LogActivityAsync(
                            employeeId, 
                            "M-Pesa Payment Initiated", 
                            $"Employee initiated M-Pesa payment for sale #{createdSale.SaleNumber}",
                            "Sale",
                            createdSale.SaleId,
                            new { 
                                SaleNumber = createdSale.SaleNumber,
                                TotalAmount = request.TotalAmount,
                                CheckoutRequestId = mpesaData.CheckoutRequestId
                            },
                            HttpContext.Connection.RemoteIpAddress?.ToString(),
                            Request.Headers["User-Agent"]
                        );
                        
                        // Return success with detailed status for frontend
                        return Json(new
                        {
                            success = true,
                            message = "STK Push sent successfully! Check your phone.",
                            saleId = createdSale.SaleId,
                            saleNumber = createdSale.SaleNumber,
                            totalAmount = request.TotalAmount,
                            paymentMethod = "M-Pesa",
                            status = "STK_SENT",
                            waitingForCallback = true,
                            checkoutRequestId = mpesaData.CheckoutRequestId,
                            statusMessages = new
                            {
                                current = "STK Push sent to your phone",
                                next = "Please enter your M-Pesa PIN to complete payment"
                            }
                        });
                    }
                    catch (Exception mpesaDbEx)
                    {
                        _logger.LogError(mpesaDbEx, "❌ Failed to store MPESA transaction data: {ErrorMessage}", mpesaDbEx.Message);
                        return Json(new { 
                            success = false, 
                            message = "Failed to save M-Pesa transaction. Please try again.",
                            status = "db_error"
                        });
                    }
                }

                // For non-M-Pesa payments, log completion
                await _activityLogService.LogActivityAsync(
                    employeeId, 
                    "Sale Completed", 
                    $"Employee completed sale #{createdSale.SaleNumber} for KSh {request.TotalAmount:N2}",
                    "Sale",
                    createdSale.SaleId,
                    new { 
                        SaleNumber = createdSale.SaleNumber,
                        TotalAmount = request.TotalAmount,
                        PaymentMethod = request.PaymentMethod,
                        ItemCount = request.Items.Count
                    },
                    HttpContext.Connection.RemoteIpAddress?.ToString(),
                    Request.Headers["User-Agent"]
                );

                _logger.LogInformation("Employee sale processed successfully. Sale ID: {SaleId}", createdSale.SaleId);

                return Json(new { 
                    success = true, 
                    saleId = createdSale.SaleId, 
                    saleNumber = createdSale.SaleNumber,
                    message = "Sale processed successfully",
                    status = "Completed"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing employee sale");
                return Json(new { success = false, message = "An error occurred while processing the sale: " + ex.Message });
            }
        }

        [HttpGet("CheckPaymentStatus")]
        public async Task<IActionResult> CheckPaymentStatus(int saleId)
        {
            try
            {
                var sale = await _context.Sales.FirstOrDefaultAsync(s => s.SaleId == saleId);
                if (sale == null)
                {
                    return Json(new { success = false, message = "Sale not found" });
                }

                // Check if this is an M-Pesa payment
                if (sale.PaymentMethod.Equals("M-Pesa", StringComparison.OrdinalIgnoreCase))
                {
                    var mpesaTransaction = await _context.MpesaTransactions
                        .FirstOrDefaultAsync(mt => mt.SaleId == saleId);

                    if (mpesaTransaction == null)
                    {
                        return Json(new { 
                            success = false, 
                            status = "Error", 
                            message = "M-Pesa transaction record not found" 
                        });
                    }

                    // If status is still Pending or STK_SENT, query M-Pesa API for actual status
                    if (mpesaTransaction.Status == "Pending" || mpesaTransaction.Status == "STK_SENT")
                    {
                        try
                        {
                            // Query M-Pesa for transaction status
                            var mpesaService = HttpContext.RequestServices.GetService<IMpesaService>();
                            if (mpesaService != null && !string.IsNullOrEmpty(mpesaTransaction.CheckoutRequestId))
                            {
                                var queryResult = await mpesaService.QueryStkPushStatusAsync(mpesaTransaction.CheckoutRequestId);
                                _logger.LogInformation("📊 STK Query Result for {CheckoutRequestId}: {Result}", 
                                    mpesaTransaction.CheckoutRequestId, System.Text.Json.JsonSerializer.Serialize(queryResult));
                                
                                // Parse query result and update database
                                var resultJson = System.Text.Json.JsonSerializer.Serialize(queryResult);
                                var resultDoc = System.Text.Json.JsonDocument.Parse(resultJson);
                                
                                if (resultDoc.RootElement.TryGetProperty("ResultCode", out var resultCode))
                                {
                                    var code = resultCode.GetString();
                                    var resultDesc = resultDoc.RootElement.TryGetProperty("ResultDesc", out var desc) ? desc.GetString() : "Unknown error";
                                    
                                    _logger.LogInformation("🔍 M-Pesa Query - Code: {Code}, Desc: {Desc}", code, resultDesc);
                                    
                                    if (code == "0") // Success
                                    {
                                        // Extract M-Pesa details from CallbackMetadata
                                        string? mpesaReceiptNumber = null;
                                        decimal? amountReceived = null;
                                        string? phoneNumber = null;
                                        
                                        if (resultDoc.RootElement.TryGetProperty("CallbackMetadata", out var metadata))
                                        {
                                            if (metadata.TryGetProperty("Item", out var items))
                                            {
                                                foreach (var item in items.EnumerateArray())
                                                {
                                                    if (item.TryGetProperty("Name", out var nameEl) && item.TryGetProperty("Value", out var valueEl))
                                                    {
                                                        var name = nameEl.GetString();
                                                        switch (name)
                                                        {
                                                            case "MpesaReceiptNumber":
                                                                mpesaReceiptNumber = valueEl.GetString();
                                                                break;
                                                            case "Amount":
                                                                if (valueEl.ValueKind == System.Text.Json.JsonValueKind.Number)
                                                                    amountReceived = valueEl.GetDecimal();
                                                                else if (valueEl.ValueKind == System.Text.Json.JsonValueKind.String && decimal.TryParse(valueEl.GetString(), out var amt))
                                                                    amountReceived = amt;
                                                                break;
                                                            case "PhoneNumber":
                                                                phoneNumber = valueEl.ValueKind == System.Text.Json.JsonValueKind.Number ? valueEl.GetInt64().ToString() : valueEl.GetString();
                                                                break;
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                        
                                        // Update to completed
                                        mpesaTransaction.Status = "Completed";
                                        mpesaTransaction.CompletedAt = DateTime.UtcNow;
                                        mpesaTransaction.MpesaReceiptNumber = mpesaReceiptNumber;
                                        if (amountReceived.HasValue)
                                        {
                                            mpesaTransaction.Amount = amountReceived.Value;
                                        }
                                        
                                        sale.Status = "Completed";
                                        sale.AmountPaid = amountReceived ?? sale.TotalAmount;
                                        sale.ChangeGiven = 0; // M-Pesa has no change
                                        sale.MpesaReceiptNumber = mpesaReceiptNumber;
                                        if (!string.IsNullOrEmpty(phoneNumber))
                                        {
                                            sale.CustomerPhone = phoneNumber;
                                        }
                                        
                                        await _context.SaveChangesAsync();
                                        _logger.LogInformation("✅ Payment completed via STK Query for sale {SaleId}, Receipt: {Receipt}, Amount: {Amount}", 
                                            saleId, mpesaReceiptNumber, amountReceived);
                                    }
                                    else if (code == "1032" || resultDesc.Contains("still under processing") || resultDesc.Contains("request is being processed"))
                                    {
                                        // Transaction still processing - keep status as Pending, don't mark as failed
                                        _logger.LogInformation("⏳ Transaction still processing for sale {SaleId}, will check again", saleId);
                                        // Don't update status - keep it as STK_SENT or Pending
                                    }
                                    else // Actually failed or cancelled
                                    {
                                        // Map M-Pesa error codes to user-friendly messages
                                        var userMessage = code switch
                                        {
                                            "1" => "Wrong PIN entered",
                                            "1032" => "Transaction cancelled by user",
                                            "1037" => "Transaction timeout - no response from user",
                                            "2001" => "Wrong PIN entered",
                                            "1001" => "Unable to process - please try again",
                                            "1019" => "Transaction expired",
                                            "17" => "Initiator authentication failed",
                                            "2006" => "Insufficient balance",
                                            _ => resultDesc
                                        };
                                        
                                        // Only mark as failed if it's a real failure (wrong PIN, cancelled, timeout, etc.)
                                        mpesaTransaction.Status = "Failed";
                                        mpesaTransaction.ErrorMessage = userMessage;
                                        sale.Status = "Failed";
                                        
                                        await _context.SaveChangesAsync();
                                        _logger.LogWarning("❌ Payment failed via STK Query for sale {SaleId}: Code {Code}, Error: {Error}", saleId, code, userMessage);
                                    }
                                }
                            }
                        }
                        catch (Exception queryEx)
                        {
                            _logger.LogWarning(queryEx, "Failed to query STK status for {CheckoutRequestId}", mpesaTransaction.CheckoutRequestId);
                        }
                    }
                    
                    // Return current status
                    return Json(new
                    {
                        success = true,
                        status = mpesaTransaction.Status,
                        saleStatus = sale.Status,
                        message = mpesaTransaction.Status switch
                        {
                            "Completed" => "Payment completed successfully!",
                            "Failed" => $"Payment failed: {mpesaTransaction.ErrorMessage ?? "Unknown error"}",
                            "Pending" => "Waiting for payment confirmation...",
                            "STK_SENT" => "Waiting for payment confirmation...",
                            _ => "Payment status unknown"
                        },
                        mpesaReceiptNumber = mpesaTransaction.MpesaReceiptNumber,
                        completedAt = mpesaTransaction.CompletedAt
                    });
                }

                // For non-M-Pesa payments, return completed status
                return Json(new
                {
                    success = true,
                    status = "Completed",
                    saleStatus = sale.Status,
                    message = "Payment completed"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking payment status for sale {SaleId}: {ErrorMessage}", saleId, ex.Message);
                return Json(new { 
                    success = false, 
                    status = "Error", 
                    message = "Error checking payment status" 
                });
            }
        }

        [HttpGet("GetCategories")]
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

        [HttpGet("GetTodaysSalesStats")]
        public async Task<IActionResult> GetTodaysSalesStats()
        {
            try
            {
                // Get today's sales only using proper date filtering
                var today = DateTime.Today;
                var tomorrow = today.AddDays(1);
                var todaysSales = await _context.Sales
                    .AsNoTracking()
                    .Where(s => s.SaleDate >= today && s.SaleDate < tomorrow && s.Status == "Completed")
                    .ToListAsync();

                _logger.LogInformation($"Employee GetTodaysSalesStats - TODAY'S SALES: {todaysSales.Count()} totaling KSh {todaysSales.Sum(s => s.TotalAmount):N2}");

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

        [HttpGet("GetNotifications")]
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

        [HttpGet("GetReceiptData")]
        public async Task<IActionResult> GetReceiptData(int saleId)
        {
            try
            {
                var sale = await _context.Sales
                    .Include(s => s.SaleItems)
                    .ThenInclude(si => si.Product)
                    .FirstOrDefaultAsync(s => s.SaleId == saleId);

                if (sale == null)
                {
                    return Json(new { success = false, message = "Sale not found" });
                }

                var items = sale.SaleItems.Select(si => new
                {
                    productName = si.Product?.Name ?? "Unknown Product",
                    quantity = si.Quantity,
                    unitPrice = si.UnitPrice,
                    totalPrice = si.TotalPrice
                }).ToList();

                return Json(new
                {
                    success = true,
                    items = items,
                    totalAmount = sale.TotalAmount,
                    amountPaid = sale.AmountPaid,
                    changeGiven = sale.ChangeGiven,
                    mpesaReceiptNumber = sale.MpesaReceiptNumber,
                    paymentMethod = sale.PaymentMethod,
                    customerPhone = sale.CustomerPhone
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting receipt data for sale {SaleId}", saleId);
                return Json(new { success = false, message = "Error loading receipt data" });
            }
        }

        [HttpGet("GetConversationMessages")]
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

        [HttpPost("SendMessage")]
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

        [HttpPost("SendQuickMessage")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendQuickMessage([FromBody] SendQuickMessageRequest request)
        {
            try
            {
                var currentUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
                
                _logger.LogInformation("📤 Employee {UserId} sending message to {ToUserId}: {Content}", 
                    currentUserId, request.ToUserId, request.Content);

                // Validate input
                if (request.ToUserId <= 0 || string.IsNullOrWhiteSpace(request.Content))
                {
                    _logger.LogWarning("Invalid message data: ToUserId={ToUserId}, Content={Content}", 
                        request.ToUserId, request.Content);
                    return Json(new { success = false, message = "Invalid message data" });
                }

                var message = new Message
                {
                    FromUserId = currentUserId,
                    ToUserId = request.ToUserId,
                    Subject = request.Subject ?? "",
                    Content = request.Content,
                    MessageType = request.MessageType ?? "General",
                    SentDate = DateTime.UtcNow,
                    IsRead = false
                };

                _logger.LogInformation("💾 Adding message to database: {MessageId}", message.MessageId);
                _context.Messages.Add(message);
                
                var saveResult = await _context.SaveChangesAsync();
                _logger.LogInformation("✅ Database save result: {SaveResult} rows affected. MessageId: {MessageId}", 
                    saveResult, message.MessageId);

                // Verify the message was saved
                var savedMessage = await _context.Messages.FindAsync(message.MessageId);
                if (savedMessage == null)
                {
                    _logger.LogError("❌ Message was not found in database after save!");
                    return Json(new { success = false, message = "Failed to save message" });
                }

                _logger.LogInformation("🎉 Message successfully saved with ID: {MessageId}", savedMessage.MessageId);
                return Json(new { success = true, messageId = savedMessage.MessageId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error sending quick message for employee");
                return Json(new { success = false, message = "Error sending message: " + ex.Message });
            }
        }

        [HttpPost("MarkMessagesAsRead")]
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

        [HttpGet("GetConversationsList")]
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

        [HttpPost("GenerateReceiptPDF")]
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

        [HttpGet("GetUnreadCount")]
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
        [HttpGet("TestMessagesAndUsers")]
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

        [HttpGet("GetUsers")]
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

        [HttpGet("GetUserOnlineStatus")]
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

        [HttpGet("CheckNewMessages")]
        public async Task<IActionResult> CheckNewMessages(int lastMessageId = 0)
        {
            try
            {
                var currentUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
                
                // Get new messages since lastMessageId
                var newMessages = await _context.Messages
                    .Where(m => m.ToUserId == currentUserId && m.MessageId > lastMessageId)
                    .OrderBy(m => m.SentDate)
                    .Select(m => new
                    {
                        m.MessageId,
                        m.FromUserId,
                        m.Content,
                        m.Subject,
                        m.SentDate,
                        m.IsRead,
                        SenderName = m.FromUser.FirstName + " " + m.FromUser.LastName,
                        SenderInitials = (m.FromUser.FirstName.Substring(0, 1) + m.FromUser.LastName.Substring(0, 1)).ToUpper()
                    })
                    .ToListAsync();

                // Get total unread count
                var unreadCount = await _context.Messages
                    .Where(m => m.ToUserId == currentUserId && !m.IsRead)
                    .CountAsync();

                return Json(new 
                { 
                    success = true, 
                    newMessages = newMessages,
                    unreadCount = unreadCount,
                    hasNewMessages = newMessages.Any()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking new messages for employee");
                return Json(new { success = false, message = "Error checking messages" });
            }
        }

        [HttpPost("VerifyManualMpesaCode")]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> VerifyManualMpesaCode([FromBody] ManualMpesaVerificationRequest request)
        {
            try
            {
                var code = request.MpesaCode.ToUpper().Trim();
                
                if (code.Length < 5)
                {
                    return Json(new { success = false, message = "Please enter at least 5 characters" });
                }

                // Search for unused M-Pesa transaction by last 5 digits or full code
                var query = _context.UnusedMpesaTransactions
                    .Where(t => t.IsUsed == false && t.TillNumber == "6509715");
                
                if (code.Length >= 5)
                {
                    var last5 = code.Length >= 5 ? code.Substring(code.Length - 5) : code;
                    query = query.Where(t => t.TransactionCode.EndsWith(last5) || t.TransactionCode == code);
                }
                
                var transaction = await query.FirstOrDefaultAsync();
                
                if (transaction == null)
                {
                    return Json(new { 
                        success = false, 
                        message = "No unused M-Pesa transaction found with this code" 
                    });
                }
                
                // Verify amount matches (allow 1 shilling difference for rounding)
                if (Math.Abs(transaction.Amount - request.SaleAmount) > 1)
                {
                    return Json(new { 
                        success = false, 
                        message = $"Amount mismatch. Transaction: KSh {transaction.Amount}, Sale: KSh {request.SaleAmount}" 
                    });
                }
                
                // Mark transaction as used
            transaction.IsUsed = true;
            transaction.UsedAt = DateTime.UtcNow;
            
            // Update or create sale
            Sale? sale = null;
            if (request.SaleId > 0)
            {
                sale = await _context.Sales.FindAsync(request.SaleId);
            }
            
            if (sale != null)
            {
                sale.Status = "Completed";
                sale.MpesaReceiptNumber = transaction.TransactionCode;
                sale.AmountPaid = transaction.Amount;
                transaction.SaleId = sale.SaleId;
            }
            else
            {
                _logger.LogWarning("No sale found with ID {SaleId}, transaction recorded but not linked", request.SaleId);
            }
            
            await _context.SaveChangesAsync();
            
            return Json(new { 
                success = true, 
                message = "Payment verified successfully!",
                mpesaReceiptNumber = transaction.TransactionCode,
                amount = transaction.Amount
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying manual M-Pesa code");
            return Json(new { success = false, message = "Error verifying code" });
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

    public class ManualMpesaVerificationRequest
    {
        public string MpesaCode { get; set; } = "";
        public int SaleId { get; set; }
        public decimal SaleAmount { get; set; }
    }
}
