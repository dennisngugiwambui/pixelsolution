using PixelSolution.Models;
using PixelSolution.ViewModels;

namespace PixelSolution.Services.Interfaces
{
    public interface IAuthService
    {
        Task<User?> AuthenticateAsync(string email, string password);
        Task<bool> ChangePasswordAsync(int userId, string currentPassword, string newPassword);
        Task<string> GenerateJwtTokenAsync(User user);
        Task<User?> GetUserByIdAsync(int userId);
        bool ValidatePassword(string password, string hash);
        string HashPassword(string password);
        Task<bool> ConvertToHashedPasswordsAsync();
        Task<bool> EnsureUserPasswordIsHashedAsync(string email, string plainPassword);
    }

    public interface IUserService
    {
        Task<IEnumerable<User>> GetAllUsersAsync();
        Task<User?> GetUserByIdAsync(int userId);
        Task<User?> GetUserByEmailAsync(string email);
        Task<User> CreateUserWithDepartmentsAsync(EnhancedCreateUserViewModel model);
        Task<User> UpdateUserWithDepartmentsAsync(EnhancedEditUserViewModel model);
        Task<bool> DeleteUserAsync(int userId);
        Task<bool> ChangeUserStatusAsync(int userId, string status);
        Task<IEnumerable<User>> GetUsersByDepartmentAsync(int departmentId);
        Task<IEnumerable<User>> GetUsersByTypeAsync(string userType);
        Task UpdateUserDepartmentsAsync(int userId, List<int> departmentIds);
    }

    public interface IDepartmentService
    {
        Task<IEnumerable<Department>> GetAllDepartmentsAsync();
        Task<Department?> GetDepartmentByIdAsync(int departmentId);
        Task<Department> CreateDepartmentAsync(Department department);
        Task<Department> UpdateDepartmentAsync(Department department);
        Task<bool> DeleteDepartmentAsync(int departmentId);
    }

    public interface ICategoryService
    {
        Task<IEnumerable<Category>> GetAllCategoriesAsync();
        Task<IEnumerable<Category>> GetActiveCategoriesAsync();
        Task<Category?> GetCategoryByIdAsync(int categoryId);
        Task<Category> CreateCategoryAsync(Category category);
        Task<Category> UpdateCategoryAsync(Category category);
        Task<bool> DeleteCategoryAsync(int categoryId);
        Task<bool> ToggleCategoryStatusAsync(int categoryId);
        Task<List<CategoryListViewModel>> GetCategoriesWithStatsAsync();
    }

    public interface ISupplierService
    {
        Task<IEnumerable<Supplier>> GetAllSuppliersAsync();
        Task<IEnumerable<Supplier>> GetActiveSuppliersAsync();
        Task<Supplier?> GetSupplierByIdAsync(int supplierId);
        Task<Supplier> CreateSupplierAsync(Supplier supplier);
        Task<Supplier> UpdateSupplierAsync(Supplier supplier);
        Task<bool> DeleteSupplierAsync(int supplierId);
        Task<bool> ToggleSupplierStatusAsync(int supplierId);
    }

    public interface IProductService
    {
        Task<IEnumerable<Product>> GetAllProductsAsync();
        Task<IEnumerable<Product>> GetActiveProductsAsync();
        Task<Product?> GetProductByIdAsync(int productId);
        Task<Product?> GetProductBySkuAsync(string sku);
        Task<IEnumerable<Product>> GetProductsByCategoryAsync(int categoryId);
        Task<IEnumerable<Product>> GetProductsBySupplierAsync(int supplierId);
        Task<IEnumerable<Product>> GetLowStockProductsAsync();
        Task<Product> CreateProductAsync(Product product);
        Task<Product> UpdateProductAsync(Product product);
        Task<bool> DeleteProductAsync(int productId);
        Task<bool> UpdateStockAsync(int productId, int quantity);
        Task<bool> ToggleProductStatusAsync(int productId);
        Task<List<ProductListViewModel>> GetProductsWithStatsAsync();
    }

    public interface ISaleService
    {
        Task<IEnumerable<Sale>> GetAllSalesAsync();
        Task<IEnumerable<Sale>> GetSalesByDateRangeAsync(DateTime startDate, DateTime endDate);
        Task<IEnumerable<Sale>> GetSalesByUserAsync(int userId);
        Task<Sale?> GetSaleByIdAsync(int saleId);
        Task<Sale?> GetSaleByNumberAsync(string saleNumber);
        Task<Sale> CreateSaleAsync(Sale sale);
        Task<Sale> UpdateSaleAsync(Sale sale);
        Task<bool> CancelSaleAsync(int saleId);
        Task<decimal> GetTotalSalesAmountAsync(DateTime? startDate = null, DateTime? endDate = null);
        Task<int> GetTotalSalesCountAsync(DateTime? startDate = null, DateTime? endDate = null);
        Task<IEnumerable<object>> GetSalesAnalyticsAsync();
    }

    public interface IPurchaseRequestService
    {
        Task<IEnumerable<PurchaseRequest>> GetAllPurchaseRequestsAsync();
        Task<IEnumerable<PurchaseRequest>> GetPurchaseRequestsByStatusAsync(string status);
        Task<IEnumerable<PurchaseRequest>> GetPurchaseRequestsByUserAsync(int userId);
        Task<PurchaseRequest?> GetPurchaseRequestByIdAsync(int purchaseRequestId);
        Task<PurchaseRequest> CreatePurchaseRequestAsync(PurchaseRequest purchaseRequest);
        Task<PurchaseRequest> UpdatePurchaseRequestAsync(PurchaseRequest purchaseRequest);
        Task<bool> ApprovePurchaseRequestAsync(int purchaseRequestId);
        Task<bool> CancelPurchaseRequestAsync(int purchaseRequestId);
        Task<string> GenerateRequestNumberAsync();
    }

    public interface IMessageService
    {
        Task<IEnumerable<Message>> GetAllMessagesAsync();
        Task<IEnumerable<Message>> GetMessagesByUserAsync(int userId);
        Task<IEnumerable<Message>> GetUnreadMessagesAsync(int userId);
        Task<Message?> GetMessageByIdAsync(int messageId);
        Task<Message> SendMessageAsync(Message message);
        Task<bool> MarkAsReadAsync(int messageId);
        Task<bool> DeleteMessageAsync(int messageId);
        Task<int> GetUnreadCountAsync(int userId);
        Task<bool> SendReminderToEmployeesAsync(int fromUserId, string subject, string content);
        Task<bool> SendPromotionMessageAsync(int fromUserId, string subject, string content);
    }

    public interface IReportService
    {
        Task<DashboardViewModel> GetDashboardDataAsync();
        Task<DashboardViewModel> GetEmployeeDashboardDataAsync(int employeeId);
        Task<SidebarCountsViewModel> GetSidebarCountsAsync();
        Task<SidebarCountsViewModel> GetEmployeeSidebarCountsAsync(int employeeId);
        Task<List<SaleChartDataViewModel>> GetSalesChartDataAsync();
        Task<List<TopProductViewModel>> GetTopProductsAsync();
        Task<SalesPageViewModel> GetSalesPageDataAsync();
        Task<List<Category>> GetCategoriesAsync();
        Task<byte[]> GeneratePdfReportAsync(string reportType, DateTime? startDate = null, DateTime? endDate = null);
        Task<byte[]> GenerateExcelReportAsync(string reportType, DateTime? startDate = null, DateTime? endDate = null);
        Task<byte[]> GetSalesReportAsync(DateTime startDate, DateTime endDate);
        Task<byte[]> GetInventoryReportAsync();
        Task<byte[]> GetSupplierReportAsync();
        Task<byte[]> GetUserActivityReportAsync();
        Task<byte[]> GenerateSalesReceiptAsync(int saleId);
        Task<byte[]> GenerateSalesReportExcelAsync(DateTime startDate, DateTime endDate);
        Task<byte[]> GenerateSalesReportAsync(DateTime startDate, DateTime endDate);
        Task<byte[]> GenerateInventoryReportExcelAsync();
        Task<byte[]> GenerateInventoryReportAsync();
        Task<byte[]> GenerateUserReportExcelAsync();
        Task<byte[]> GenerateUserReportAsync();
        Task<byte[]> GenerateCategoriesReportExcelAsync();
        Task<byte[]> GenerateCategoriesReportAsync();
        Task<byte[]> GenerateSuppliersReportExcelAsync();
        Task<byte[]> GenerateSuppliersReportAsync();
        Task<byte[]> GenerateComprehensiveReportAsync(DateTime? startDate = null, DateTime? endDate = null);
        Task<byte[]> GenerateReceiptPdfAsync(int saleId);
        Task<byte[]> GenerateReceiptPdfAsync(ReceiptPdfRequest request);
        Task<byte[]> GenerateReceiptPDFAsync(string receiptHtml);
        Task<byte[]> GeneratePurchaseRequestReceiptAsync(int purchaseRequestId);
    }

    public interface ISalesService
    {
        Task<ProcessSaleResult> ProcessSaleAsync(ProcessSaleRequest request, int userId);
    }

    public interface IBarcodeService
    {
        Task<byte[]> GenerateBarcodeAsync(string data, int width = 200, int height = 50);
        Task<string> GenerateBarcodeBase64Async(string data, int width = 200, int height = 50);
        bool ValidateBarcode(string barcode);
        string GenerateProductBarcode(int productId);
        Task<string> GenerateProductBarcodeAsync(int productId, int width = 200, int height = 50);
        Task<byte[]> GenerateQRCodeAsync(string data, int width = 200, int height = 200);
        Task<byte[]> GenerateCode128BarcodeAsync(string data, int width = 200, int height = 50);
        Task<byte[]> GenerateProductStickerAsync(int productId, bool includePrice, int width = 300, int height = 200);
    }

    public interface IReceiptPrintingService
    {
        Task<bool> PrintReceiptAsync(byte[] receiptData, string printerName = null);
        Task<bool> PrintReceiptAsync(int saleId, string printerName = null);
        Task<bool> PrintSalesReceiptAsync(int saleId, string printerName = null);
        Task<bool> PrintPurchaseRequestReceiptAsync(int purchaseRequestId, string printerName = null);
        Task<List<string>> GetAvailablePrintersAsync();
        Task<bool> IsPrinterAvailableAsync(string printerName);
    }
}