using Microsoft.EntityFrameworkCore;
using PixelSolution.Data;
using PixelSolution.Models;

namespace PixelSolution.Services
{
    public class SalesService : ISalesService
    {
        private readonly ApplicationDbContext _context;

        public SalesService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<ProcessSaleResult> ProcessSaleAsync(ProcessSaleRequest request, int userId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            
            try
            {
                // Get user information
                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                {
                    return new ProcessSaleResult
                    {
                        Success = false,
                        ErrorMessage = "User not found"
                    };
                }

                // Generate sale number
                var saleNumber = await GenerateSaleNumberAsync();

                // Create sale record
                var sale = new Sale
                {
                    SaleNumber = saleNumber,
                    UserId = userId,
                    CashierName = $"{user.FirstName} {user.LastName}",
                    CustomerName = request.CustomerName,
                    CustomerPhone = request.CustomerPhone,
                    CustomerEmail = request.CustomerEmail,
                    PaymentMethod = request.PaymentMethod,
                    TotalAmount = request.TotalAmount,
                    AmountPaid = request.AmountPaid,
                    ChangeGiven = request.ChangeGiven,
                    Status = "Completed",
                    SaleDate = DateTime.UtcNow
                };

                _context.Sales.Add(sale);
                await _context.SaveChangesAsync();

                // Process sale items and update stock
                foreach (var item in request.Items)
                {
                    // Get product and check stock
                    var product = await _context.Products.FindAsync(item.ProductId);
                    if (product == null)
                    {
                        await transaction.RollbackAsync();
                        return new ProcessSaleResult
                        {
                            Success = false,
                            ErrorMessage = $"Product with ID {item.ProductId} not found"
                        };
                    }

                    if (product.StockQuantity < item.Quantity)
                    {
                        await transaction.RollbackAsync();
                        return new ProcessSaleResult
                        {
                            Success = false,
                            ErrorMessage = $"Insufficient stock for {product.Name}. Available: {product.StockQuantity}, Requested: {item.Quantity}"
                        };
                    }

                    // Create sale item
                    var saleItem = new SaleItem
                    {
                        SaleId = sale.SaleId,
                        ProductId = item.ProductId,
                        Quantity = item.Quantity,
                        UnitPrice = item.UnitPrice,
                        TotalPrice = item.TotalPrice
                    };

                    _context.SaleItems.Add(saleItem);

                    // Update product stock
                    product.StockQuantity -= item.Quantity;
                    product.UpdatedAt = DateTime.UtcNow;
                    _context.Products.Update(product);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return new ProcessSaleResult
                {
                    Success = true,
                    SaleId = sale.SaleId,
                    SaleNumber = sale.SaleNumber,
                    ReceiptData = new
                    {
                        saleNumber = sale.SaleNumber,
                        cashierName = sale.CashierName,
                        totalAmount = sale.TotalAmount,
                        amountPaid = sale.AmountPaid,
                        changeGiven = sale.ChangeGiven,
                        paymentMethod = sale.PaymentMethod,
                        saleDate = sale.SaleDate
                    }
                };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return new ProcessSaleResult
                {
                    Success = false,
                    ErrorMessage = $"Error processing sale: {ex.Message}"
                };
            }
        }

        private async Task<string> GenerateSaleNumberAsync()
        {
            var today = DateTime.Today;
            var todayPrefix = today.ToString("yyyyMMdd");
            
            var lastSaleToday = await _context.Sales
                .Where(s => s.SaleNumber.StartsWith(todayPrefix))
                .OrderByDescending(s => s.SaleNumber)
                .FirstOrDefaultAsync();

            int sequenceNumber = 1;
            if (lastSaleToday != null)
            {
                var lastSequence = lastSaleToday.SaleNumber.Substring(8);
                if (int.TryParse(lastSequence, out int lastNum))
                {
                    sequenceNumber = lastNum + 1;
                }
            }

            return $"{todayPrefix}{sequenceNumber:D4}";
        }
    }
}
