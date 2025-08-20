using Microsoft.EntityFrameworkCore;
using PixelSolution.Data;
using PixelSolution.Models;

namespace PixelSolution.Services
{
    public interface ICustomerCartService
    {
        Task<CustomerCart?> AddToCartAsync(int customerId, int productId, int quantity);
        Task<bool> UpdateCartItemAsync(int cartId, int quantity);
        Task<bool> RemoveFromCartAsync(int cartId);
        Task<List<CustomerCart>> GetCartItemsAsync(int customerId);
        Task<decimal> GetCartTotalAsync(int customerId);
        Task<bool> ClearCartAsync(int customerId);
        Task<ProductRequest?> ConvertCartToRequestAsync(int customerId, string deliveryAddress, string notes = "");
    }

    public class CustomerCartService : ICustomerCartService
    {
        private readonly ApplicationDbContext _context;

        public CustomerCartService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<CustomerCart?> AddToCartAsync(int customerId, int productId, int quantity)
        {
            try
            {
                // Check if product exists and has sufficient stock
                var product = await _context.Products.FindAsync(productId);
                if (product == null || !product.IsActive)
                    return null;

                if (product.StockQuantity < quantity)
                    return null; // Insufficient stock

                // Check if item already exists in cart
                var existingCartItem = await _context.CustomerCarts
                    .FirstOrDefaultAsync(cc => cc.CustomerId == customerId && cc.ProductId == productId);

                if (existingCartItem != null)
                {
                    // Update existing cart item
                    var newQuantity = existingCartItem.Quantity + quantity;
                    
                    // Check if new quantity exceeds stock
                    if (product.StockQuantity < newQuantity)
                        return null;

                    existingCartItem.Quantity = newQuantity;
                    existingCartItem.TotalPrice = newQuantity * product.SellingPrice;
                    existingCartItem.UpdatedAt = DateTime.UtcNow;

                    await _context.SaveChangesAsync();
                    return existingCartItem;
                }

                // Create new cart item
                var cartItem = new CustomerCart
                {
                    CustomerId = customerId,
                    ProductId = productId,
                    Quantity = quantity,
                    UnitPrice = product.SellingPrice,
                    TotalPrice = quantity * product.SellingPrice
                };

                _context.CustomerCarts.Add(cartItem);
                await _context.SaveChangesAsync();

                return cartItem;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public async Task<bool> UpdateCartItemAsync(int cartId, int quantity)
        {
            try
            {
                var cartItem = await _context.CustomerCarts
                    .Include(cc => cc.Product)
                    .FirstOrDefaultAsync(cc => cc.CartId == cartId);

                if (cartItem == null)
                    return false;

                // Check stock availability
                if (cartItem.Product.StockQuantity < quantity)
                    return false;

                if (quantity <= 0)
                {
                    // Remove item if quantity is 0 or negative
                    _context.CustomerCarts.Remove(cartItem);
                }
                else
                {
                    cartItem.Quantity = quantity;
                    cartItem.TotalPrice = quantity * cartItem.UnitPrice;
                    cartItem.UpdatedAt = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<bool> RemoveFromCartAsync(int cartId)
        {
            try
            {
                var cartItem = await _context.CustomerCarts.FindAsync(cartId);
                if (cartItem == null)
                    return false;

                _context.CustomerCarts.Remove(cartItem);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<List<CustomerCart>> GetCartItemsAsync(int customerId)
        {
            return await _context.CustomerCarts
                .Include(cc => cc.Product)
                .ThenInclude(p => p.Category)
                .Where(cc => cc.CustomerId == customerId)
                .OrderBy(cc => cc.AddedAt)
                .ToListAsync();
        }

        public async Task<decimal> GetCartTotalAsync(int customerId)
        {
            return await _context.CustomerCarts
                .Where(cc => cc.CustomerId == customerId)
                .SumAsync(cc => cc.TotalPrice);
        }

        public async Task<bool> ClearCartAsync(int customerId)
        {
            try
            {
                var cartItems = await _context.CustomerCarts
                    .Where(cc => cc.CustomerId == customerId)
                    .ToListAsync();

                _context.CustomerCarts.RemoveRange(cartItems);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<ProductRequest?> ConvertCartToRequestAsync(int customerId, string deliveryAddress, string notes = "")
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Get cart items
                var cartItems = await GetCartItemsAsync(customerId);
                if (!cartItems.Any())
                    return null;

                // Generate request number
                var requestNumber = $"REQ-{DateTime.Now:yyyyMMdd}-{new Random().Next(1000, 9999)}";

                // Create product request
                var productRequest = new ProductRequest
                {
                    RequestNumber = requestNumber,
                    CustomerId = customerId,
                    TotalAmount = cartItems.Sum(ci => ci.TotalPrice),
                    Status = "Pending",
                    PaymentStatus = "Unpaid",
                    Notes = notes,
                    DeliveryAddress = deliveryAddress
                };

                _context.ProductRequests.Add(productRequest);
                await _context.SaveChangesAsync();

                // Create product request items
                foreach (var cartItem in cartItems)
                {
                    var requestItem = new ProductRequestItem
                    {
                        ProductRequestId = productRequest.ProductRequestId,
                        ProductId = cartItem.ProductId,
                        Quantity = cartItem.Quantity,
                        UnitPrice = cartItem.UnitPrice,
                        TotalPrice = cartItem.TotalPrice,
                        Status = "Pending"
                    };

                    _context.ProductRequestItems.Add(requestItem);
                }

                // Clear cart
                _context.CustomerCarts.RemoveRange(cartItems);
                
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return productRequest;
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                return null;
            }
        }
    }
}
