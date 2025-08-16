using System.ComponentModel.DataAnnotations;

namespace PixelSolution.ViewModels
{
    // ======================================
    // ENHANCED USER VIEW MODELS (Non-Auth)
    // ======================================
    public class UserListViewModel
    {
        public int UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string UserType { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string DepartmentNames { get; set; } = string.Empty;
        public int TotalSales { get; set; }
        public decimal TotalSalesAmount { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class DepartmentSelectionViewModel
    {
        public int DepartmentId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsSelected { get; set; }
    }

    // Update existing CreateUserViewModel to support multiple departments
    public class EnhancedCreateUserViewModel
    {
        [Required(ErrorMessage = "First name is required")]
        [StringLength(50, ErrorMessage = "First name cannot exceed 50 characters")]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Last name is required")]
        [StringLength(50, ErrorMessage = "Last name cannot exceed 50 characters")]
        public string LastName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Please enter a valid email address")]
        [StringLength(100, ErrorMessage = "Email cannot exceed 100 characters")]
        public string Email { get; set; } = string.Empty;

        [Phone(ErrorMessage = "Please enter a valid phone number")]
        [StringLength(20, ErrorMessage = "Phone number cannot exceed 20 characters")]
        public string? Phone { get; set; }

        [Required(ErrorMessage = "Password is required")]
        [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be between 8 and 100 characters")]
        [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&])[A-Za-z\d@$!%*?&]",
            ErrorMessage = "Password must contain at least one uppercase letter, one lowercase letter, one number, and one special character")]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Confirm password is required")]
        [Compare("Password", ErrorMessage = "Passwords do not match")]
        public string ConfirmPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "User type is required")]
        public string UserType { get; set; } = "Employee";

        public string Status { get; set; } = "Active";
        public string? Privileges { get; set; }

        [Required(ErrorMessage = "At least one department must be selected")]
        public List<int> DepartmentIds { get; set; } = new List<int>();
        public List<DepartmentSelectionViewModel> AvailableDepartments { get; set; } = new List<DepartmentSelectionViewModel>();
    }

    public class EnhancedEditUserViewModel
    {
        public int UserId { get; set; }

        [Required(ErrorMessage = "First name is required")]
        [StringLength(50, ErrorMessage = "First name cannot exceed 50 characters")]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Last name is required")]
        [StringLength(50, ErrorMessage = "Last name cannot exceed 50 characters")]
        public string LastName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Please enter a valid email address")]
        [StringLength(100, ErrorMessage = "Email cannot exceed 100 characters")]
        public string Email { get; set; } = string.Empty;

        [Phone(ErrorMessage = "Please enter a valid phone number")]
        [StringLength(20, ErrorMessage = "Phone number cannot exceed 20 characters")]
        public string? Phone { get; set; }

        [Required(ErrorMessage = "User type is required")]
        public string UserType { get; set; } = "Employee";

        public string Status { get; set; } = "Active";
        public string? Privileges { get; set; }
        public List<int> DepartmentIds { get; set; } = new List<int>();

        [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be between 8 and 100 characters")]
        [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&])[A-Za-z\d@$!%*?&]",
            ErrorMessage = "Password must contain at least one uppercase letter, one lowercase letter, one number, and one special character")]
        public string? NewPassword { get; set; }

        [Compare("NewPassword", ErrorMessage = "Passwords do not match")]
        public string? ConfirmNewPassword { get; set; }

        public List<DepartmentSelectionViewModel> AvailableDepartments { get; set; } = new List<DepartmentSelectionViewModel>();
    }

    // ======================================
    // DEPARTMENT VIEW MODELS
    // ======================================
    public class DepartmentListViewModel
    {
        public int DepartmentId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int UserCount { get; set; }
        public int ActiveUserCount { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    // ======================================
    // CATEGORY VIEW MODELS
    // ======================================
    public class CategoryListViewModel
    {
        public int CategoryId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? ImageUrl { get; set; }
        public bool IsActive { get; set; }
        public int ProductCount { get; set; }
        public int ActiveProductCount { get; set; }
        public decimal TotalStockValue { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    // ======================================
    // SUPPLIER VIEW MODELS
    // ======================================
    public class SupplierListViewModel
    {
        public int SupplierId { get; set; }
        public string CompanyName { get; set; } = string.Empty;
        public string ContactPerson { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string? Address { get; set; }
        public string Status { get; set; } = string.Empty;
        public int ProductCount { get; set; }
        public int PurchaseRequestCount { get; set; }
        public decimal TotalPurchaseValue { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    // ======================================
    // PRODUCT VIEW MODELS
    // ======================================
    public class ProductListViewModel
    {
        public int ProductId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string SKU { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public string? SupplierName { get; set; }
        public decimal BuyingPrice { get; set; }
        public decimal SellingPrice { get; set; }
        public int StockQuantity { get; set; }
        public int MinStockLevel { get; set; }
        public string? ImageUrl { get; set; }
        public bool IsActive { get; set; }
        public bool IsLowStock { get; set; }
        public decimal ProfitMargin { get; set; }
        public decimal ProfitPercentage { get; set; }
        public int TotalSold { get; set; }
        public decimal TotalRevenue { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? Barcode { get; set; }
    }

    public class ProductSearchViewModel
    {
        public int ProductId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string SKU { get; set; } = string.Empty;
        public decimal SellingPrice { get; set; }
        public int StockQuantity { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public string? ImageUrl { get; set; }
        public bool IsActive { get; set; }
        public string? SearchTerm { get; set; }
        public int? CategoryId { get; set; }
        public int? SupplierId { get; set; }
        public bool? IsLowStock { get; set; }
        public decimal? MinPrice { get; set; }
        public decimal? MaxPrice { get; set; }
    }

    public class ProductDetailsViewModel
    {
        public int ProductId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string SKU { get; set; } = string.Empty;
        public decimal BuyingPrice { get; set; }
        public decimal SellingPrice { get; set; }
        public int StockQuantity { get; set; }
        public int MinStockLevel { get; set; }
        public string? ImageUrl { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public int? CategoryId { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public int? SupplierId { get; set; }
        public string SupplierName { get; set; } = string.Empty;
        public decimal ProfitMargin { get; set; }
        public decimal ProfitPercentage { get; set; }
        public bool IsLowStock { get; set; }
        
        // Navigation properties for view compatibility
        public CategoryViewModel? Category { get; set; }
        public SupplierViewModel? Supplier { get; set; }
    }
    
    public class CategoryViewModel
    {
        public int CategoryId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }
    
    public class SupplierViewModel
    {
        public int SupplierId { get; set; }
        public string CompanyName { get; set; } = string.Empty;
        public string ContactPerson { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
    }

    // ======================================
    // SALES VIEW MODELS
    // ======================================
    public class SalesPageViewModel
    {
        public List<ProductSearchViewModel> Products { get; set; } = new List<ProductSearchViewModel>();
        public List<SaleListViewModel> RecentSales { get; set; } = new List<SaleListViewModel>();
        public decimal TodaysSales { get; set; }
        public int TodaysTransactions { get; set; }
        public decimal AverageTransaction { get; set; }
    }
 

    public class SaleDetailsViewModel
    {
        public int SaleId { get; set; }
        public string SaleNumber { get; set; } = string.Empty;
        public string CashierName { get; set; } = string.Empty; // Added CashierName
        public string CustomerName { get; set; } = string.Empty;
        public string? CustomerPhone { get; set; }
        public string? CustomerEmail { get; set; }
        public string PaymentMethod { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public decimal AmountPaid { get; set; }
        public decimal ChangeGiven { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime SaleDate { get; set; }
        public List<SaleItemDetailsViewModel> Items { get; set; } = new List<SaleItemDetailsViewModel>();
    }

    public class SaleListViewModel
    {
        public int SaleId { get; set; }
        public string SaleNumber { get; set; } = string.Empty;
        public DateTime SaleDate { get; set; }
        public decimal TotalAmount { get; set; }
        public string PaymentMethod { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string? CustomerPhone { get; set; }
        public int ItemCount { get; set; }
        public string CashierName { get; set; } = string.Empty; // Added CashierName
        public decimal ChangeGiven { get; set; }
        public decimal AmountPaid { get; set; }
        public int TotalQuantity { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    public class SaleItemDetailsViewModel
    {
        public int SaleItemId { get; set; }
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string ProductSKU { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalPrice { get; set; }
    }

    public class CreateSaleViewModel
    {
        public List<CreateSaleItemViewModel> Items { get; set; } = new List<CreateSaleItemViewModel>();
        public decimal TotalAmount { get; set; }
        public string PaymentMethod { get; set; } = string.Empty;
        public decimal AmountPaid { get; set; }
        public decimal ChangeGiven { get; set; }
        public string? CustomerName { get; set; }
        public string? CustomerPhone { get; set; }
        public string? CustomerEmail { get; set; }
    }

    public class CreateSaleItemViewModel
    {
        public int ProductId { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalPrice { get; set; }
    }

    // ======================================
    // DASHBOARD VIEW MODELS
    // ======================================
    public class DashboardViewModel
    {
        public DashboardStatsViewModel Stats { get; set; } = new DashboardStatsViewModel();
        public List<SaleChartDataViewModel> SalesChartData { get; set; } = new List<SaleChartDataViewModel>();
        public List<TopProductViewModel> TopProducts { get; set; } = new List<TopProductViewModel>();
        public List<RecentSaleViewModel> RecentSales { get; set; } = new List<RecentSaleViewModel>();
        public List<LowStockProductViewModel> LowStockProducts { get; set; } = new List<LowStockProductViewModel>();
    }

    public class DashboardStatsViewModel
    {
        public decimal TodaySales { get; set; }
        public int TodayOrders { get; set; }
        public decimal ThisMonthSales { get; set; }
        public decimal LastMonthSales { get; set; }
        public decimal SalesGrowth { get; set; }
        public int ProductsSoldToday { get; set; }
        public int NewCustomersThisMonth { get; set; }
        public int LowStockProducts { get; set; }
        public int PendingPurchaseRequests { get; set; }
        public int UnreadMessages { get; set; }
    }

    public class SaleChartDataViewModel
    {
        public DateTime Date { get; set; }
        public string DateLabel { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public int OrderCount { get; set; }
    }

    public class TopProductViewModel
    {
        public int ProductId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string SKU { get; set; } = string.Empty;
        public int QuantitySold { get; set; }
        public decimal Revenue { get; set; }
        public decimal AvgPrice { get; set; }
    }

    public class RecentSaleViewModel
    {
        public int SaleId { get; set; }
        public string SaleNumber { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string ProductNames { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime SaleDate { get; set; }
    }

    public class LowStockProductViewModel
    {
        public int ProductId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string SKU { get; set; } = string.Empty;
        public int StockQuantity { get; set; }
        public int MinStockLevel { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public string? SupplierName { get; set; }
    }

    // ======================================
    // PRINTING & BARCODE VIEW MODELS
    // ======================================
    public class PrintReceiptViewModel
    {
        public int SaleId { get; set; }
        public string PrinterName { get; set; } = string.Empty;
        public int Copies { get; set; } = 1;
        public bool PrinterAvailable { get; set; }
        public string ReceiptHtml { get; set; } = string.Empty;
    }

    public class GenerateBarcodeViewModel
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string SKU { get; set; } = string.Empty;
        public string BarcodeType { get; set; } = "QR"; // QR or CODE128
        public int Width { get; set; } = 200;
        public int Height { get; set; } = 200;
        public bool IncludeText { get; set; } = true;
    }

    // ======================================
    // REPORT VIEW MODELS
    // ======================================
    public class SalesReportViewModel
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public decimal TotalSales { get; set; }
        public int TotalTransactions { get; set; }
        public decimal AverageTransaction { get; set; }
        public List<SalesReportItemViewModel> DailySales { get; set; } = new List<SalesReportItemViewModel>();
        public List<CategorySalesViewModel> CategorySales { get; set; } = new List<CategorySalesViewModel>();
        public List<UserSalesViewModel> UserSales { get; set; } = new List<UserSalesViewModel>();
    }

    public class SalesReportItemViewModel
    {
        public DateTime Date { get; set; }
        public decimal Amount { get; set; }
        public int TransactionCount { get; set; }
    }

    public class CategorySalesViewModel
    {
        public string CategoryName { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public int Quantity { get; set; }
    }

    public class UserSalesViewModel
    {
        public string UserName { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public int TransactionCount { get; set; }
    }

    // ======================================
    // INVENTORY VIEW MODELS
    // ======================================
    public class UpdateStockViewModel
    {
        [Required(ErrorMessage = "Product ID is required")]
        public int ProductId { get; set; }

        [Required(ErrorMessage = "New quantity is required")]
        [Range(0, int.MaxValue, ErrorMessage = "Quantity must be non-negative")]
        public int NewQuantity { get; set; }

        [StringLength(200, ErrorMessage = "Reason cannot exceed 200 characters")]
        public string? Reason { get; set; }
    }

    public class CreateDepartmentViewModel
    {
        [Required(ErrorMessage = "Department name is required")]
        [StringLength(100, ErrorMessage = "Department name cannot exceed 100 characters")]
        public string Name { get; set; } = string.Empty;

        [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
        public string Description { get; set; } = string.Empty;
    }

    public class EditDepartmentViewModel
    {
        public int DepartmentId { get; set; }

        [Required(ErrorMessage = "Department name is required")]
        [StringLength(100, ErrorMessage = "Department name cannot exceed 100 characters")]
        public string Name { get; set; } = string.Empty;

        [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
        public string Description { get; set; } = string.Empty;
    }
    // ======================================
    // MESSAGES PAGE VIEW MODELS
    // ======================================

    public class MessagesPageViewModel
    {
        public int CurrentUserId { get; set; }
        public List<ConversationViewModel> Conversations { get; set; } = new List<ConversationViewModel>();
        public List<MessageViewModel> Messages { get; set; } = new List<MessageViewModel>();
        public ConversationViewModel? SelectedConversation { get; set; }
        public List<UserSelectViewModel> AllUsers { get; set; } = new List<UserSelectViewModel>();
        public int UnreadCount { get; set; }
        public string CurrentUserName { get; set; } = string.Empty;
        public string CurrentUserType { get; set; } = string.Empty;
    }

    public class ConversationViewModel
    {
        public int UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string UserInitials { get; set; } = string.Empty;
        public string UserType { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public bool IsOnline { get; set; }
        public DateTime? LastSeen { get; set; }
        public string LastSeenFormatted { get; set; } = string.Empty;
        public string LastMessage { get; set; } = string.Empty;
        public string LastMessageTime { get; set; } = string.Empty;
        public DateTime? LastMessageDate { get; set; }
        public int UnreadCount { get; set; }
        public bool HasUnreadMessages { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    public class MessageViewModel
    {
        public int MessageId { get; set; }
        public int FromUserId { get; set; }
        public int ToUserId { get; set; }
        public string FromUserName { get; set; } = string.Empty;
        public string ToUserName { get; set; } = string.Empty;
        public string FromUserInitials { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string MessageType { get; set; } = "General";
        public bool IsRead { get; set; }
        public DateTime SentDate { get; set; }
        public DateTime? ReadDate { get; set; }
        public bool IsFromCurrentUser { get; set; }
        public string FormattedSentDate { get; set; } = string.Empty;
        public string FormattedReadDate { get; set; } = string.Empty;
        public string TimeAgo { get; set; } = string.Empty;
    }

    public class UserSelectViewModel
    {
        public int UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string UserType { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public bool IsOnline { get; set; }
        public string UserInitials { get; set; } = string.Empty;
    }

    public class SendMessageViewModel
    {
        [Required(ErrorMessage = "Recipient is required")]
        public int ToUserId { get; set; }

        [Required(ErrorMessage = "Subject is required")]
        [StringLength(200, ErrorMessage = "Subject cannot exceed 200 characters")]
        public string Subject { get; set; } = string.Empty;

        [Required(ErrorMessage = "Message content is required")]
        [StringLength(2000, ErrorMessage = "Content cannot exceed 2000 characters")]
        public string Content { get; set; } = string.Empty;

        public string MessageType { get; set; } = "General"; // General, Reminder, Promotion
    }

    public class QuickMessageViewModel
    {
        [Required(ErrorMessage = "Recipient is required")]
        public int ToUserId { get; set; }

        [Required(ErrorMessage = "Message content is required")]
        [StringLength(2000, ErrorMessage = "Content cannot exceed 2000 characters")]
        public string Content { get; set; } = string.Empty;

        public string Subject { get; set; } = string.Empty;
        public string MessageType { get; set; } = "General";
    }

    public class MessageStatusUpdateViewModel
    {
        public int MessageId { get; set; }
        public bool IsRead { get; set; }
        public DateTime? ReadDate { get; set; }
    }

    public class ConversationMessagesViewModel
    {
        public ConversationViewModel Conversation { get; set; } = new ConversationViewModel();
        public List<MessageViewModel> Messages { get; set; } = new List<MessageViewModel>();
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class MarkAsReadRequest
    {
        public int UserId { get; set; }
    }

    public class OnlineUsersResponse
    {
        public bool Success { get; set; }
        public List<int> OnlineUsers { get; set; } = new List<int>();
        public string Message { get; set; } = string.Empty;
    }

    public class NewMessagesResponse
    {
        public bool Success { get; set; }
        public List<MessageViewModel> NewMessages { get; set; } = new List<MessageViewModel>();
        public string Message { get; set; } = string.Empty;
    }

    public class ConversationsListResponse
    {
        public bool Success { get; set; }
        public List<ConversationViewModel> Conversations { get; set; } = new List<ConversationViewModel>();
        public string Message { get; set; } = string.Empty;
    }

    // ======================================
    // BULK MESSAGING VIEW MODELS
    // ======================================

    public class BulkMessageViewModel
    {
        [Required(ErrorMessage = "Recipients are required")]
        public List<int> RecipientIds { get; set; } = new List<int>();

        [Required(ErrorMessage = "Subject is required")]
        [StringLength(200, ErrorMessage = "Subject cannot exceed 200 characters")]
        public string Subject { get; set; } = string.Empty;

        [Required(ErrorMessage = "Message content is required")]
        [StringLength(2000, ErrorMessage = "Content cannot exceed 2000 characters")]
        public string Content { get; set; } = string.Empty;

        [Required(ErrorMessage = "Message type is required")]
        public string MessageType { get; set; } = "General";

        public bool SendToAllEmployees { get; set; }
        public bool SendToManagers { get; set; }
        public List<int> DepartmentIds { get; set; } = new List<int>();
    }

    public class MessageStatsViewModel
    {
        public int TotalMessages { get; set; }
        public int UnreadMessages { get; set; }
        public int TodayMessages { get; set; }
        public int ThisWeekMessages { get; set; }
        public int SentMessages { get; set; }
        public int ReceivedMessages { get; set; }
        public List<MessageTypeStatsViewModel> MessageTypeStats { get; set; } = new List<MessageTypeStatsViewModel>();
        public List<DailyMessageStatsViewModel> DailyStats { get; set; } = new List<DailyMessageStatsViewModel>();
    }

    public class MessageTypeStatsViewModel
    {
        public string MessageType { get; set; } = string.Empty;
        public int Count { get; set; }
        public double Percentage { get; set; }
    }

    public class DailyMessageStatsViewModel
    {
        public DateTime Date { get; set; }
        public int MessageCount { get; set; }
        public string DateLabel { get; set; } = string.Empty;
    }

    // ======================================
    // MESSAGE SEARCH AND FILTER VIEW MODELS
    // ======================================

    public class MessageSearchViewModel
    {
        public string SearchTerm { get; set; } = string.Empty;
        public string MessageType { get; set; } = string.Empty;
        public bool? IsRead { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int? FromUserId { get; set; }
        public int? ToUserId { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }

    public class MessageSearchResultViewModel
    {
        public List<MessageViewModel> Messages { get; set; } = new List<MessageViewModel>();
        public int TotalCount { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
        public bool HasPreviousPage { get; set; }
        public bool HasNextPage { get; set; }
        public string SearchTerm { get; set; } = string.Empty;
    }

    // ======================================
    // TYPING STATUS VIEW MODELS
    // ======================================

    public class TypingStatusViewModel
    {
        public int UserId { get; set; }
        public int ConversationWithUserId { get; set; }
        public bool IsTyping { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    // ======================================
    // MESSAGE ATTACHMENTS (Future Enhancement)
    // ======================================

    public class MessageAttachmentViewModel
    {
        public int AttachmentId { get; set; }
        public int MessageId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string FileType { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public DateTime UploadedAt { get; set; }
    }

    // ======================================
    // NOTIFICATION PREFERENCES
    // ======================================

    public class NotificationPreferencesViewModel
    {
        public int UserId { get; set; }
        public bool EmailNotifications { get; set; } = true;
        public bool PushNotifications { get; set; } = true;
        public bool SoundNotifications { get; set; } = true;
        public bool DesktopNotifications { get; set; } = true;
        public List<string> MutedConversations { get; set; } = new List<string>();
        public string NotificationHours { get; set; } = "09:00-17:00"; // Business hours
    }

}