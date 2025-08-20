using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PixelSolution.Data;
using PixelSolution.Models;
using PixelSolution.Services;

namespace PixelSolution.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CustomerApiController : ControllerBase
    {
        private readonly ICustomerCartService _cartService;
        private readonly IProductRequestService _requestService;
        private readonly ApplicationDbContext _context;

        public CustomerApiController(
            ICustomerCartService cartService,
            IProductRequestService requestService,
            ApplicationDbContext context)
        {
            _cartService = cartService;
            _requestService = requestService;
            _context = context;
        }

        // Cart Management APIs
        [HttpPost("cart/add")]
        public async Task<IActionResult> AddToCart([FromBody] AddToCartRequest request)
        {
            var cartItem = await _cartService.AddToCartAsync(request.CustomerId, request.ProductId, request.Quantity);
            if (cartItem == null)
                return BadRequest(new { message = "Unable to add item to cart. Check stock availability." });

            return Ok(new { message = "Item added to cart successfully", cartItem });
        }

        [HttpPut("cart/{cartId}")]
        public async Task<IActionResult> UpdateCartItem(int cartId, [FromBody] UpdateCartRequest request)
        {
            var success = await _cartService.UpdateCartItemAsync(cartId, request.Quantity);
            if (!success)
                return BadRequest(new { message = "Unable to update cart item" });

            return Ok(new { message = "Cart item updated successfully" });
        }

        [HttpDelete("cart/{cartId}")]
        public async Task<IActionResult> RemoveFromCart(int cartId)
        {
            var success = await _cartService.RemoveFromCartAsync(cartId);
            if (!success)
                return NotFound(new { message = "Cart item not found" });

            return Ok(new { message = "Item removed from cart successfully" });
        }

        [HttpGet("cart/{customerId}")]
        public async Task<IActionResult> GetCartItems(int customerId)
        {
            var cartItems = await _cartService.GetCartItemsAsync(customerId);
            var total = await _cartService.GetCartTotalAsync(customerId);

            return Ok(new { cartItems, total });
        }

        [HttpDelete("cart/{customerId}/clear")]
        public async Task<IActionResult> ClearCart(int customerId)
        {
            var success = await _cartService.ClearCartAsync(customerId);
            if (!success)
                return BadRequest(new { message = "Unable to clear cart" });

            return Ok(new { message = "Cart cleared successfully" });
        }

        [HttpPost("cart/{customerId}/checkout")]
        public async Task<IActionResult> CheckoutCart(int customerId, [FromBody] CheckoutRequest request)
        {
            var productRequest = await _cartService.ConvertCartToRequestAsync(
                customerId, request.DeliveryAddress, request.Notes);

            if (productRequest == null)
                return BadRequest(new { message = "Unable to process checkout" });

            return Ok(new { message = "Order placed successfully", requestId = productRequest.ProductRequestId });
        }

        // Product Request APIs
        [HttpGet("requests/{customerId}")]
        public async Task<IActionResult> GetCustomerRequests(int customerId)
        {
            var requests = await _requestService.GetCustomerRequestsAsync(customerId);
            return Ok(requests);
        }

        [HttpGet("request/{requestId}")]
        public async Task<IActionResult> GetRequestById(int requestId)
        {
            var request = await _requestService.GetRequestByIdAsync(requestId);
            if (request == null)
                return NotFound(new { message = "Request not found" });

            return Ok(request);
        }

        [HttpPost("request/{requestId}/cancel")]
        public async Task<IActionResult> CancelRequest(int requestId, [FromBody] CancelRequestRequest request)
        {
            var success = await _requestService.CancelRequestAsync(requestId, request.Reason);
            if (!success)
                return BadRequest(new { message = "Unable to cancel request" });

            return Ok(new { message = "Request cancelled successfully" });
        }

        // Customer Management
        [HttpPost("register")]
        public async Task<IActionResult> RegisterCustomer([FromBody] RegisterCustomerRequest request)
        {
            try
            {
                var existingCustomer = await _context.Customers
                    .FirstOrDefaultAsync(c => c.Email == request.Email);

                if (existingCustomer != null)
                    return BadRequest(new { message = "Customer with this email already exists" });

                var customer = new Customer
                {
                    FirstName = request.FirstName,
                    LastName = request.LastName,
                    Email = request.Email,
                    Phone = request.Phone,
                    Address = request.Address,
                    City = request.City
                };

                _context.Customers.Add(customer);
                await _context.SaveChangesAsync();

                return Ok(new { message = "Customer registered successfully", customerId = customer.CustomerId });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = "Registration failed", error = ex.Message });
            }
        }

        [HttpGet("customer/{customerId}")]
        public async Task<IActionResult> GetCustomer(int customerId)
        {
            var customer = await _context.Customers.FindAsync(customerId);
            if (customer == null)
                return NotFound(new { message = "Customer not found" });

            return Ok(customer);
        }

        [HttpGet("products")]
        public async Task<IActionResult> GetProducts([FromQuery] string? category = null, [FromQuery] string? search = null)
        {
            var query = _context.Products
                .Include(p => p.Category)
                .Where(p => p.IsActive && p.StockQuantity > 0);

            if (!string.IsNullOrEmpty(category))
            {
                query = query.Where(p => p.Category.Name.Contains(category));
            }

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(p => p.Name.Contains(search) || p.Description.Contains(search));
            }

            var products = await query.ToListAsync();
            return Ok(products);
        }
    }

    // Request Models
    public class AddToCartRequest
    {
        public int CustomerId { get; set; }
        public int ProductId { get; set; }
        public int Quantity { get; set; }
    }

    public class UpdateCartRequest
    {
        public int Quantity { get; set; }
    }

    public class CheckoutRequest
    {
        public string DeliveryAddress { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
    }

    public class CancelRequestRequest
    {
        public string Reason { get; set; } = string.Empty;
    }

    public class RegisterCustomerRequest
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
    }
}
