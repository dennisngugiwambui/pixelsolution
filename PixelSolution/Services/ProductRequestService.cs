using Microsoft.EntityFrameworkCore;
using PixelSolution.Data;
using PixelSolution.Models;

namespace PixelSolution.Services
{
    public interface IProductRequestService
    {
        Task<List<ProductRequest>> GetAllRequestsAsync();
        Task<List<ProductRequest>> GetCustomerRequestsAsync(int customerId);
        Task<ProductRequest?> GetRequestByIdAsync(int requestId);
        Task<bool> UpdateRequestStatusAsync(int requestId, string status, int processedByUserId);
        Task<bool> ProcessDeliveryAsync(int requestId, int processedByUserId);
        Task<Sale?> ConvertRequestToSaleAsync(int requestId, int cashierUserId, string paymentMethod);
        Task<bool> CancelRequestAsync(int requestId, string reason);
    }

    public class ProductRequestService : IProductRequestService
    {
        private readonly ApplicationDbContext _context;

        public ProductRequestService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<ProductRequest>> GetAllRequestsAsync()
        {
            return await _context.ProductRequests
                .Include(pr => pr.Customer)
                .Include(pr => pr.ProcessedByUser)
                .Include(pr => pr.ProductRequestItems)
                .ThenInclude(pri => pri.Product)
                .OrderByDescending(pr => pr.RequestDate)
                .ToListAsync();
        }

        public async Task<List<ProductRequest>> GetCustomerRequestsAsync(int customerId)
        {
            return await _context.ProductRequests
                .Include(pr => pr.ProductRequestItems)
                .ThenInclude(pri => pri.Product)
                .Where(pr => pr.CustomerId == customerId)
                .OrderByDescending(pr => pr.RequestDate)
                .ToListAsync();
        }

        public async Task<ProductRequest?> GetRequestByIdAsync(int requestId)
        {
            return await _context.ProductRequests
                .Include(pr => pr.Customer)
                .Include(pr => pr.ProcessedByUser)
                .Include(pr => pr.ProductRequestItems)
                .ThenInclude(pri => pri.Product)
                .FirstOrDefaultAsync(pr => pr.ProductRequestId == requestId);
        }

        public async Task<bool> UpdateRequestStatusAsync(int requestId, string status, int processedByUserId)
        {
            try
            {
                var request = await _context.ProductRequests.FindAsync(requestId);
                if (request == null)
                    return false;

                request.Status = status;
                request.ProcessedByUserId = processedByUserId;

                if (status == "Delivered")
                {
                    request.DeliveryDate = DateTime.UtcNow;
                }
                else if (status == "Completed")
                {
                    request.CompletedDate = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<bool> ProcessDeliveryAsync(int requestId, int processedByUserId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var request = await GetRequestByIdAsync(requestId);
                if (request == null || request.Status != "Processing")
                    return false;

                // Check stock availability for all items
                foreach (var item in request.ProductRequestItems)
                {
                    var product = await _context.Products.FindAsync(item.ProductId);
                    if (product == null || product.StockQuantity < item.Quantity)
                    {
                        item.Status = "OutOfStock";
                        continue;
                    }

                    // Reserve stock (reduce quantity)
                    product.StockQuantity -= item.Quantity;
                    item.Status = "Fulfilled";
                }

                // Update request status
                request.Status = "Delivered";
                request.DeliveryDate = DateTime.UtcNow;
                request.ProcessedByUserId = processedByUserId;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return true;
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                return false;
            }
        }

        public async Task<Sale?> ConvertRequestToSaleAsync(int requestId, int cashierUserId, string paymentMethod)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var request = await GetRequestByIdAsync(requestId);
                if (request == null || request.Status != "Delivered")
                    return null;

                // Generate sale number
                var saleNumber = $"SALE-{DateTime.Now:yyyyMMdd}-{new Random().Next(1000, 9999)}";

                // Get cashier info
                var cashier = await _context.Users.FindAsync(cashierUserId);
                if (cashier == null)
                    return null;

                // Create sale
                var sale = new Sale
                {
                    SaleNumber = saleNumber,
                    UserId = cashierUserId,
                    CashierName = cashier.FullName,
                    CustomerName = request.Customer.FullName,
                    CustomerPhone = request.Customer.Phone,
                    CustomerEmail = request.Customer.Email,
                    PaymentMethod = paymentMethod,
                    TotalAmount = request.TotalAmount,
                    AmountPaid = request.TotalAmount,
                    ChangeGiven = 0,
                    Status = "Completed"
                };

                _context.Sales.Add(sale);
                await _context.SaveChangesAsync();

                // Create sale items
                foreach (var requestItem in request.ProductRequestItems.Where(ri => ri.Status == "Fulfilled"))
                {
                    var saleItem = new SaleItem
                    {
                        SaleId = sale.SaleId,
                        ProductId = requestItem.ProductId,
                        Quantity = requestItem.Quantity,
                        UnitPrice = requestItem.UnitPrice,
                        TotalPrice = requestItem.TotalPrice
                    };

                    _context.SaleItems.Add(saleItem);
                }

                // Update request status
                request.Status = "Completed";
                request.CompletedDate = DateTime.UtcNow;
                request.PaymentStatus = "Paid";

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return sale;
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                return null;
            }
        }

        public async Task<bool> CancelRequestAsync(int requestId, string reason)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var request = await GetRequestByIdAsync(requestId);
                if (request == null)
                    return false;

                // If request was delivered, restore stock
                if (request.Status == "Delivered")
                {
                    foreach (var item in request.ProductRequestItems.Where(ri => ri.Status == "Fulfilled"))
                    {
                        var product = await _context.Products.FindAsync(item.ProductId);
                        if (product != null)
                        {
                            product.StockQuantity += item.Quantity;
                        }
                    }
                }

                request.Status = "Cancelled";
                request.Notes = $"{request.Notes}\n\nCancellation Reason: {reason}";

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return true;
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                return false;
            }
        }
    }
}
