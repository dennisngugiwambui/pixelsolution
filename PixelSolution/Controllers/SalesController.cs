using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PixelSolution.Services.Interfaces;
using PixelSolution.Services;
using PixelSolution.ViewModels;
using PixelSolution.Models;
using PixelSolution.Data;
using System.Security.Claims;

namespace PixelSolution.Controllers
{
    [Authorize]
    public class SalesController : Controller
    {
        private readonly ISaleService _saleService;
        private readonly IProductService _productService;
        private readonly IUserService _userService;
        private readonly IReportService _reportService;
        private readonly IReceiptPrintingService _receiptPrintingService;
        private readonly IMpesaService _mpesaService;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<SalesController> _logger;

        public SalesController(
            ISaleService saleService,
            IProductService productService,
            IUserService userService,
            IReportService reportService,
            IReceiptPrintingService receiptPrintingService,
            IMpesaService mpesaService,
            ApplicationDbContext context,
            ILogger<SalesController> logger)
        {
            _saleService = saleService;
            _productService = productService;
            _userService = userService;
            _reportService = reportService;
            _receiptPrintingService = receiptPrintingService;
            _mpesaService = mpesaService;
            _context = context;
            _logger = logger;
        }

        [HttpPost]
        public async Task<IActionResult> ProcessSale([FromBody] CreateSaleViewModel model)
        {
            try
            {
                _logger.LogInformation("Processing sale with {ItemCount} items, payment method: {PaymentMethod}", model.Items.Count, model.PaymentMethod);

                // Variable to store MPESA transaction data for callback tracking
                object? mpesaTransactionData = null;

                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                    var errorMessage = string.Join("; ", errors);
                    _logger.LogWarning("ModelState validation failed: {Errors}", errorMessage);
                    return Json(new { success = false, message = $"Invalid sale data: {errorMessage}" });
                }

                // Get current user ID
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                {
                    _logger.LogError("User authentication failed. UserIdClaim: {UserIdClaim}", userIdClaim ?? "null");
                    return Json(new { success = false, message = "User authentication failed. Please log in again." });
                }
                
                _logger.LogInformation("Processing sale for user ID: {UserId}", userId);
                
                // Debug: Log all user claims to understand the authentication
                var allClaims = User.Claims.Select(c => new { c.Type, c.Value }).ToList();
                _logger.LogInformation("All user claims: {Claims}", System.Text.Json.JsonSerializer.Serialize(allClaims));
                
                // Debug: Check what users actually exist in database
                var allUsers = await _context.Users.Select(u => new { u.UserId, u.FirstName, u.LastName, u.Email }).ToListAsync();
                _logger.LogInformation("All users in database: {Users}", System.Text.Json.JsonSerializer.Serialize(allUsers));
                
                // Get user directly from database using the authenticated user ID
                var currentUser = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);
                if (currentUser == null)
                {
                    _logger.LogError("Authenticated user with ID {UserId} not found in database. This indicates a data inconsistency.", userId);
                    
                    // Try to find user by email from claims as fallback
                    var userEmail = User.FindFirst(ClaimTypes.Email)?.Value;
                    if (!string.IsNullOrEmpty(userEmail))
                    {
                        currentUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == userEmail);
                        if (currentUser != null)
                        {
                            _logger.LogWarning("Found user by email {Email} with different ID {ActualUserId} vs claim ID {ClaimUserId}", 
                                userEmail, currentUser.UserId, userId);
                        }
                    }
                    
                    if (currentUser == null)
                    {
                        return Json(new { success = false, message = $"Authentication error: User data inconsistency. Please log out and log in again." });
                    }
                }
                
                _logger.LogInformation("Validated user: {UserName} (ID: {UserId})", $"{currentUser.FirstName} {currentUser.LastName}", userId);

                // Debug: Log all received product IDs
                _logger.LogInformation("DEBUG: Received sale items: {Items}", 
                    System.Text.Json.JsonSerializer.Serialize(model.Items.Select(i => new { i.ProductId, i.Quantity, i.UnitPrice })));
                
                // Debug: Log all available products in database
                var allDbProducts = await _context.Products.Select(p => new { p.ProductId, p.Name, p.IsActive }).ToListAsync();
                _logger.LogInformation("DEBUG: Available products in database: {Products}", 
                    System.Text.Json.JsonSerializer.Serialize(allDbProducts));

                // Validate stock availability before processing
                foreach (var item in model.Items)
                {
                    _logger.LogInformation("DEBUG: Looking for product with ID {ProductId} (type: {Type})", item.ProductId, item.ProductId.GetType().Name);
                    
                    var product = await _productService.GetProductByIdAsync(item.ProductId);
                    if (product == null)
                    {
                        _logger.LogError("DEBUG: Product with ID {ProductId} not found. Available IDs: {AvailableIds}", 
                            item.ProductId, string.Join(", ", allDbProducts.Select(p => p.ProductId)));
                        return Json(new { success = false, message = $"Product with ID {item.ProductId} not found. Available products: {string.Join(", ", allDbProducts.Where(p => p.IsActive).Select(p => $"{p.ProductId}({p.Name})"))}" });
                    }

                    if (product.StockQuantity < item.Quantity)
                    {
                        return Json(new { success = false, message = $"Insufficient stock for {product.Name}. Available: {product.StockQuantity}, Requested: {item.Quantity}" });
                    }

                    if (!product.IsActive)
                    {
                        return Json(new { success = false, message = $"Product {product.Name} is not available for sale." });
                    }
                }

                // Handle MPESA payments - Initiate STK Push BEFORE creating sale
                if (model.PaymentMethod.Equals("M-Pesa", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogInformation("Processing MPESA payment for amount: {Amount}, Phone: {Phone}", model.TotalAmount, model.CustomerPhone);
                    
                    if (string.IsNullOrEmpty(model.CustomerPhone))
                    {
                        return Json(new { success = false, message = "Phone number is required for MPESA payments." });
                    }

                    // Format phone number exactly like MpesaTestController
                    var formattedPhone = FormatPhoneNumberForMpesa(model.CustomerPhone);
                    
                    // Validate phone number format (should be 12 digits starting with 254)
                    if (string.IsNullOrEmpty(formattedPhone) || formattedPhone.Length != 12 || !formattedPhone.StartsWith("254"))
                    {
                        _logger.LogError("Invalid phone format. Original: {Original}, Formatted: {Formatted}, Length: {Length}", 
                            model.CustomerPhone, formattedPhone, formattedPhone?.Length ?? 0);
                        return Json(new { success = false, message = $"Invalid phone number format. Expected 12 digits (254XXXXXXXXX), got {formattedPhone?.Length ?? 0} digits: {formattedPhone}" });
                    }
                    _logger.LogInformation("Formatted phone number: {FormattedPhone}", formattedPhone);

                    try
                    {
                        // Step 1: Validate amount (minimum only, no maximum limit)
                        if (model.TotalAmount < 1)
                        {
                            return Json(new { success = false, message = "Amount must be at least KSh 1" });
                        }

                        // Step 2: Initiate STK Push FIRST - before creating sale record
                        _logger.LogInformation("📱 Initiating MPESA STK Push for phone: {Phone}, amount: {Amount}", formattedPhone, model.TotalAmount);
                        
                        var stkPushResponse = await _mpesaService.InitiateStkPushAsync(
                            formattedPhone,
                            model.TotalAmount,
                            "SALE" + DateTime.Now.ToString("yyyyMMddHHmmss").Substring(0, 8), // Account reference like test
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
                        
                        // Step 3: Validate STK Push response - ONLY proceed if successful
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
                        
                        // Step 4: Store MPESA transaction data for creating sale record
                        mpesaTransactionData = new
                        {
                            CheckoutRequestId = stkPushResponse.CheckoutRequestID,
                            MerchantRequestId = stkPushResponse.MerchantRequestID,
                            PhoneNumber = formattedPhone,
                            Amount = model.TotalAmount,
                            Status = "STK_SENT" // Initial status
                        };
                    }
                    catch (Exception mpesaEx)
                    {
                        _logger.LogError(mpesaEx, "MPESA STK Push failed: {ErrorMessage}\nStack Trace: {StackTrace}", mpesaEx.Message, mpesaEx.StackTrace);
                        
                        // Provide more specific error information
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

                // Use the already validated user for cashier name
                var cashierName = $"{currentUser.FirstName} {currentUser.LastName}";
                _logger.LogInformation("Sale will be processed by cashier: {CashierName}", cashierName);

                // Create sale object
                var sale = new Sale
                {
                    UserId = currentUser.UserId, // Use the validated database user ID, not claims ID
                    CashierName = cashierName,
                    CustomerName = model.CustomerName ?? string.Empty,
                    CustomerPhone = model.CustomerPhone ?? string.Empty,
                    CustomerEmail = model.CustomerEmail ?? string.Empty,
                    PaymentMethod = model.PaymentMethod,
                    AmountPaid = model.PaymentMethod.Equals("M-Pesa", StringComparison.OrdinalIgnoreCase) ? 0 : model.AmountPaid, // M-Pesa amount set to 0 until callback confirms
                    ChangeGiven = model.PaymentMethod.Equals("M-Pesa", StringComparison.OrdinalIgnoreCase) ? 0 : model.ChangeGiven,
                    Status = model.PaymentMethod.Equals("M-Pesa", StringComparison.OrdinalIgnoreCase) ? "Pending" : "Completed",
                    SaleItems = new List<SaleItem>()
                };

                // Add sale items
                foreach (var item in model.Items)
                {
                    var saleItem = new SaleItem
                    {
                        ProductId = item.ProductId,
                        Quantity = item.Quantity,
                        UnitPrice = item.UnitPrice,
                        TotalPrice = item.TotalPrice
                    };
                    sale.SaleItems.Add(saleItem);
                }

                _logger.LogInformation("About to create sale with {ItemCount} items for user {UserId}", sale.SaleItems.Count, userId);
                
                // Process the sale (this will handle stock deduction and generate sale number)
                Sale createdSale;
                try
                {
                    createdSale = await _saleService.CreateSaleAsync(sale);
                    _logger.LogInformation("Sale created successfully with ID: {SaleId}, Number: {SaleNumber}", createdSale.SaleId, createdSale.SaleNumber);
                }
                catch (Exception saleEx)
                {
                    _logger.LogError(saleEx, "Failed to create sale in database: {ErrorMessage}\nInner Exception: {InnerException}\nStack Trace: {StackTrace}", 
                        saleEx.Message, saleEx.InnerException?.Message ?? "None", saleEx.StackTrace);
                    
                    var detailedError = saleEx.Message;
                    if (saleEx.InnerException != null)
                    {
                        detailedError += $" | Inner: {saleEx.InnerException.Message}";
                    }
                    
                    return Json(new { success = false, message = $"Database error: {detailedError}" });
                }

                // Step 5: Store MPESA transaction information if this was an MPESA payment
                if (mpesaTransactionData != null && model.PaymentMethod.Equals("M-Pesa", StringComparison.OrdinalIgnoreCase))
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
                            Status = "STK_SENT", // Initial status after STK push sent
                            CreatedAt = DateTime.UtcNow
                        };

                        _context.MpesaTransactions.Add(mpesaTransaction);
                        await _context.SaveChangesAsync();
                        
                        _logger.LogInformation("💾 MPESA transaction stored with ID: {TransactionId} for Sale: {SaleId} - Status: STK_SENT", 
                            mpesaTransaction.MpesaTransactionId, createdSale.SaleId);
                        
                        // Step 6: Return success with detailed status for frontend
                        return Json(new
                        {
                            success = true,
                            message = "STK Push sent successfully! Check your phone.",
                            saleId = createdSale.SaleId,
                            saleNumber = createdSale.SaleNumber,
                            totalAmount = model.TotalAmount,
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

                // This code should not be reached for M-Pesa payments as they return earlier
                // Only non-M-Pesa payments reach this point
                
                // For non-M-Pesa payments, generate and print receipt immediately
                bool receiptPrinted = false;
                try
                {
                    receiptPrinted = await _receiptPrintingService.PrintSalesReceiptAsync(createdSale.SaleId);
                    _logger.LogInformation("Receipt printing result: {ReceiptPrinted}", receiptPrinted);
                }
                catch (Exception receiptEx)
                {
                    _logger.LogWarning(receiptEx, "Receipt printing failed but sale was successful: {ErrorMessage}", receiptEx.Message);
                    // Don't fail the entire sale if receipt printing fails
                }

                _logger.LogInformation("Sale {SaleNumber} completed successfully. Receipt printed: {ReceiptPrinted}",
                    createdSale.SaleNumber, receiptPrinted);

                return Json(new
                {
                    success = true,
                    message = "Sale completed successfully!",
                    saleId = createdSale.SaleId,
                    saleNumber = createdSale.SaleNumber,
                    receiptPrinted = receiptPrinted,
                    totalAmount = createdSale.TotalAmount,
                    changeGiven = createdSale.ChangeGiven,
                    cashierName = createdSale.CashierName,
                    status = "Completed"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CRITICAL ERROR in ProcessSale: {ErrorMessage}\nStack Trace: {StackTrace}\nInner Exception: {InnerException}", 
                    ex.Message, ex.StackTrace, ex.InnerException?.Message ?? "None");
                
                // Return more specific error information for debugging
                var errorDetails = $"Error: {ex.Message}";
                if (ex.InnerException != null)
                {
                    errorDetails += $" | Inner: {ex.InnerException.Message}";
                }
                
                return Json(new { 
                    success = false, 
                    message = $"Sale processing failed: {errorDetails}",
                    errorType = ex.GetType().Name,
                    debugInfo = ex.StackTrace?.Split('\n').FirstOrDefault()?.Trim()
                });
            }
        }

        [HttpGet]
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

        [HttpPost]
        public async Task<IActionResult> ReprintReceipt([FromBody] dynamic data)
        {
            try
            {
                int saleId = data.saleId;
                var sale = await _saleService.GetSaleByIdAsync(saleId);

                if (sale == null)
                {
                    return Json(new { success = false, message = "Sale not found." });
                }

                var receiptPrinted = await _receiptPrintingService.PrintSalesReceiptAsync(saleId);

                return Json(new
                {
                    success = receiptPrinted,
                    message = receiptPrinted ? "Receipt reprinted successfully!" : "Failed to reprint receipt."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reprinting receipt");
                return Json(new { success = false, message = "Error reprinting receipt. Please try again." });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetReceiptHtml(int saleId)
        {
            try
            {
                var sale = await _saleService.GetSaleByIdAsync(saleId);
                if (sale == null)
                {
                    return NotFound("Sale not found");
                }

                var receiptBytes = await _reportService.GenerateSalesReceiptAsync(saleId);
                var receiptHtml = System.Text.Encoding.UTF8.GetString(receiptBytes);

                return Content(receiptHtml, "text/html");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating receipt HTML for sale {SaleId}", saleId);
                return BadRequest("Error generating receipt");
            }
        }


        [HttpGet]
        public async Task<IActionResult> ValidateStock([FromQuery] int productId, [FromQuery] int quantity)
        {
            try
            {
                var product = await _productService.GetProductByIdAsync(productId);

                if (product == null)
                {
                    return Json(new { success = false, message = "Product not found." });
                }

                if (!product.IsActive)
                {
                    return Json(new { success = false, message = "Product is not active." });
                }

                var isAvailable = product.StockQuantity >= quantity;

                return Json(new
                {
                    success = true,
                    isAvailable = isAvailable,
                    availableStock = product.StockQuantity,
                    requestedQuantity = quantity,
                    productName = product.Name
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating stock for product {ProductId}", productId);
                return Json(new { success = false, message = "Error validating stock." });
            }
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            try
            {
                var sales = await _saleService.GetAllSalesAsync();
                return View(sales);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading sales");
                TempData["Error"] = "Error loading sales data.";
                return View(new List<Sale>());
            }
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            try
            {
                var sale = await _saleService.GetSaleByIdAsync(id);
                if (sale == null)
                {
                    TempData["Error"] = "Sale not found.";
                    return RedirectToAction("Index");
                }

                return View(sale);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading sale details for ID {SaleId}", id);
                TempData["Error"] = "Error loading sale details.";
                return RedirectToAction("Index");
            }
        }

        [HttpGet]
        public async Task<IActionResult> Receipt(int id)
        {
            try
            {
                var sale = await _saleService.GetSaleByIdAsync(id);
                if (sale == null)
                {
                    return NotFound();
                }

                var receiptBytes = await _reportService.GenerateSalesReceiptAsync(id);
                var receiptHtml = System.Text.Encoding.UTF8.GetString(receiptBytes);

                return Content(receiptHtml, "text/html");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating receipt for sale {SaleId}", id);
                return BadRequest("Error generating receipt");
            }
        }

        [HttpPost]
        public async Task<IActionResult> Cancel(int id)
        {
            try
            {
                var result = await _saleService.CancelSaleAsync(id);
                if (result)
                {
                    return Json(new { success = true, message = "Sale cancelled successfully!" });
                }
                else
                {
                    return Json(new { success = false, message = "Cannot cancel this sale." });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling sale {SaleId}", id);
                return Json(new { success = false, message = "Error cancelling sale. Please try again." });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetProductDetails(int productId)
        {
            try
            {
                var product = await _productService.GetProductByIdAsync(productId);
                if (product == null)
                {
                    return Json(new { success = false, message = "Product not found" });
                }

                return Json(new
                {
                    success = true,
                    product = new
                    {
                        productId = product.ProductId,
                        name = product.Name,
                        sku = product.SKU,
                        sellingPrice = product.SellingPrice,
                        stockQuantity = product.StockQuantity,
                        minStockLevel = product.MinStockLevel,
                        category = product.Category?.Name ?? "No Category",
                        isLowStock = product.StockQuantity <= product.MinStockLevel
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting product details for ID {ProductId}", productId);
                return Json(new { success = false, message = "Error loading product details" });
            }
        }

        /// <summary>
        /// Format phone number for M-Pesa (matches MpesaTestController validation)
        /// </summary>
        private string FormatPhoneNumberForMpesa(string phoneNumber)
        {
            if (string.IsNullOrEmpty(phoneNumber)) return string.Empty;
            
            // Remove all non-digit characters
            var cleanNumber = new string(phoneNumber.Where(char.IsDigit).ToArray());
            
            // Handle different formats
            if (cleanNumber.StartsWith("254"))
            {
                // Already has country code
                return cleanNumber.Length == 12 ? cleanNumber : string.Empty;
            }
            else if (cleanNumber.StartsWith("0"))
            {
                // Remove leading 0 and add 254
                var withoutZero = cleanNumber.Substring(1);
                return withoutZero.Length == 9 ? "254" + withoutZero : string.Empty;
            }
            else if (cleanNumber.Length == 9 && cleanNumber.StartsWith("7"))
            {
                // 9 digits starting with 7
                return "254" + cleanNumber;
            }
            
            return string.Empty;
        }

        [HttpGet]
        public async Task<IActionResult> SearchProducts(string search)
        {
            try
            {
                var products = await _productService.GetActiveProductsAsync();

                if (!string.IsNullOrEmpty(search))
                {
                    products = products.Where(p =>
                        p.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                        p.SKU.Contains(search, StringComparison.OrdinalIgnoreCase))
                        .ToList();
                }

                var result = products.Take(10).Select(p => new
                {
                    productId = p.ProductId,
                    name = p.Name,
                    sku = p.SKU,
                    sellingPrice = p.SellingPrice,
                    stockQuantity = p.StockQuantity,
                    category = p.Category?.Name ?? "No Category"
                });

                return Json(new { success = true, products = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching products with term {Search}", search);
                return Json(new { success = false, message = "Error searching products" });
            }
        }
    }
}