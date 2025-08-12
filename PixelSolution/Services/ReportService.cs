using Microsoft.EntityFrameworkCore;
using PixelSolution.Data;
using PixelSolution.Models;
using PixelSolution.Services.Interfaces;
using System.Text;

namespace PixelSolution.Services
{
    public class ReportService : IReportService
    {
        private readonly ApplicationDbContext _context;

        public ReportService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<object> GetDashboardDataAsync()
        {
            try
            {
                var today = DateTime.Today;
                var thisMonth = new DateTime(today.Year, today.Month, 1);
                var lastMonth = thisMonth.AddMonths(-1);
                var thisYear = new DateTime(today.Year, 1, 1);

                // Today's sales
                var todaySales = await _context.Sales
                    .Where(s => s.SaleDate.Date == today && s.Status == "Completed")
                    .SumAsync(s => s.TotalAmount);

                var todaySalesCount = await _context.Sales
                    .Where(s => s.SaleDate.Date == today && s.Status == "Completed")
                    .CountAsync();

                // This month's sales
                var thisMonthSales = await _context.Sales
                    .Where(s => s.SaleDate >= thisMonth && s.Status == "Completed")
                    .SumAsync(s => s.TotalAmount);

                var lastMonthSales = await _context.Sales
                    .Where(s => s.SaleDate >= lastMonth && s.SaleDate < thisMonth && s.Status == "Completed")
                    .SumAsync(s => s.TotalAmount);

                // Calculate growth percentage
                var salesGrowth = lastMonthSales > 0
                    ? ((thisMonthSales - lastMonthSales) / lastMonthSales) * 100
                    : 0;

                // Products sold today
                var todayProductsSold = await _context.SaleItems
                    .Where(si => si.Sale.SaleDate.Date == today && si.Sale.Status == "Completed")
                    .SumAsync(si => si.Quantity);

                // New customers this month
                var newCustomers = await _context.Sales
                    .Where(s => s.SaleDate >= thisMonth && !string.IsNullOrEmpty(s.CustomerEmail))
                    .Select(s => s.CustomerEmail)
                    .Distinct()
                    .CountAsync();

                // Low stock products
                var lowStockProducts = await _context.Products
                    .Where(p => p.IsActive && p.StockQuantity <= p.MinStockLevel)
                    .CountAsync();

                // Pending purchase requests
                var pendingPurchaseRequests = await _context.PurchaseRequests
                    .Where(pr => pr.Status == "Pending")
                    .CountAsync();

                // Unread messages count
                var unreadMessages = await _context.Messages
                    .Where(m => !m.IsRead)
                    .CountAsync();

                // Recent sales for chart
                var salesChartData = await GetSalesChartDataAsync();

                // Top products
                var topProducts = await GetTopProductsAsync();

                // Recent sales
                var recentSales = await _context.Sales
                    .Include(s => s.User)
                    .Include(s => s.SaleItems)
                        .ThenInclude(si => si.Product)
                    .OrderByDescending(s => s.SaleDate)
                    .Take(10)
                    .Select(s => new
                    {
                        s.SaleId,
                        s.SaleNumber,
                        CustomerName = string.IsNullOrEmpty(s.CustomerName) ? "Walk-in Customer" : s.CustomerName,
                        ProductName = string.Join(", ", s.SaleItems.Select(si => si.Product.Name).Take(2)),
                        s.TotalAmount,
                        s.Status,
                        s.SaleDate,
                        SalesPerson = s.User.FullName
                    })
                    .ToListAsync();

                return new
                {
                    stats = new
                    {
                        todaySales = new
                        {
                            value = todaySales,
                            count = todaySalesCount,
                            growth = salesGrowth > 0 ? $"+{salesGrowth:F1}%" : $"{salesGrowth:F1}%"
                        },
                        thisMonthSales = new
                        {
                            value = thisMonthSales,
                            growth = salesGrowth
                        },
                        productsSold = todayProductsSold,
                        newCustomers = newCustomers,
                        lowStockAlerts = lowStockProducts,
                        pendingRequests = pendingPurchaseRequests,
                        unreadMessages = unreadMessages
                    },
                    charts = new
                    {
                        salesData = salesChartData,
                        topProducts = topProducts
                    },
                    recentSales = recentSales
                };
            }
            catch (Exception ex)
            {
                throw new Exception($"Error generating dashboard data: {ex.Message}", ex);
            }
        }

        public async Task<object> GetSalesReportAsync(DateTime startDate, DateTime endDate)
        {
            try
            {
                var sales = await _context.Sales
                    .Include(s => s.User)
                    .Include(s => s.SaleItems)
                        .ThenInclude(si => si.Product)
                            .ThenInclude(p => p.Category)
                    .Where(s => s.SaleDate >= startDate && s.SaleDate <= endDate && s.Status == "Completed")
                    .ToListAsync();

                var totalSales = sales.Sum(s => s.TotalAmount);
                var totalTransactions = sales.Count;
                var averageTransaction = totalTransactions > 0 ? totalSales / totalTransactions : 0;

                var salesByDate = sales
                    .GroupBy(s => s.SaleDate.Date)
                    .Select(g => new
                    {
                        date = g.Key.ToString("yyyy-MM-dd"),
                        amount = g.Sum(s => s.TotalAmount),
                        transactions = g.Count()
                    })
                    .OrderBy(x => x.date)
                    .ToList();

                var salesByCategory = sales
                    .SelectMany(s => s.SaleItems)
                    .GroupBy(si => si.Product.Category.Name)
                    .Select(g => new
                    {
                        category = g.Key,
                        amount = g.Sum(si => si.TotalPrice),
                        quantity = g.Sum(si => si.Quantity)
                    })
                    .OrderByDescending(x => x.amount)
                    .ToList();

                var salesByUser = sales
                    .GroupBy(s => s.User.FullName)
                    .Select(g => new
                    {
                        user = g.Key,
                        amount = g.Sum(s => s.TotalAmount),
                        transactions = g.Count()
                    })
                    .OrderByDescending(x => x.amount)
                    .ToList();

                return new
                {
                    summary = new
                    {
                        totalSales = totalSales,
                        totalTransactions = totalTransactions,
                        averageTransaction = averageTransaction,
                        period = new
                        {
                            startDate = startDate.ToString("yyyy-MM-dd"),
                            endDate = endDate.ToString("yyyy-MM-dd")
                        }
                    },
                    salesByDate = salesByDate,
                    salesByCategory = salesByCategory,
                    salesByUser = salesByUser
                };
            }
            catch (Exception ex)
            {
                throw new Exception($"Error generating sales report: {ex.Message}", ex);
            }
        }

        public async Task<object> GetInventoryReportAsync()
        {
            try
            {
                var products = await _context.Products
                    .Include(p => p.Category)
                    .Include(p => p.Supplier)
                    .Where(p => p.IsActive)
                    .ToListAsync();

                var totalProducts = products.Count;
                var totalStockValue = products.Sum(p => p.StockQuantity * p.BuyingPrice);
                var lowStockProducts = products.Where(p => p.StockQuantity <= p.MinStockLevel).ToList();
                var outOfStockProducts = products.Where(p => p.StockQuantity == 0).ToList();

                var stockByCategory = products
                    .GroupBy(p => p.Category.Name)
                    .Select(g => new
                    {
                        category = g.Key,
                        products = g.Count(),
                        totalStock = g.Sum(p => p.StockQuantity),
                        stockValue = g.Sum(p => p.StockQuantity * p.BuyingPrice)
                    })
                    .OrderByDescending(x => x.stockValue)
                    .ToList();

                var topValueProducts = products
                    .OrderByDescending(p => p.StockQuantity * p.BuyingPrice)
                    .Take(10)
                    .Select(p => new
                    {
                        p.ProductId,
                        p.Name,
                        p.SKU,
                        category = p.Category.Name,
                        p.StockQuantity,
                        p.BuyingPrice,
                        stockValue = p.StockQuantity * p.BuyingPrice,
                        p.MinStockLevel,
                        isLowStock = p.StockQuantity <= p.MinStockLevel
                    })
                    .ToList();

                return new
                {
                    summary = new
                    {
                        totalProducts = totalProducts,
                        totalStockValue = totalStockValue,
                        lowStockCount = lowStockProducts.Count,
                        outOfStockCount = outOfStockProducts.Count
                    },
                    stockByCategory = stockByCategory,
                    topValueProducts = topValueProducts,
                    lowStockProducts = lowStockProducts.Select(p => new
                    {
                        p.ProductId,
                        p.Name,
                        p.SKU,
                        category = p.Category.Name,
                        p.StockQuantity,
                        p.MinStockLevel,
                        supplier = p.Supplier?.CompanyName
                    }).ToList()
                };
            }
            catch (Exception ex)
            {
                throw new Exception($"Error generating inventory report: {ex.Message}", ex);
            }
        }

        public async Task<object> GetSupplierReportAsync()
        {
            try
            {
                var suppliers = await _context.Suppliers
                    .Include(s => s.Products)
                    .Include(s => s.PurchaseRequests)
                    .ToListAsync();

                var supplierPerformance = suppliers.Select(s => new
                {
                    s.SupplierId,
                    s.CompanyName,
                    s.ContactPerson,
                    s.Email,
                    s.Phone,
                    s.Status,
                    productCount = s.Products.Count(p => p.IsActive),
                    totalPurchaseRequests = s.PurchaseRequests.Count,
                    pendingRequests = s.PurchaseRequests.Count(pr => pr.Status == "Pending"),
                    approvedRequests = s.PurchaseRequests.Count(pr => pr.Status == "Approved"),
                    totalPurchaseValue = s.PurchaseRequests
                        .Where(pr => pr.Status == "Approved")
                        .Sum(pr => pr.TotalAmount)
                }).ToList();

                return new
                {
                    summary = new
                    {
                        totalSuppliers = suppliers.Count,
                        activeSuppliers = suppliers.Count(s => s.Status == "Active"),
                        inactiveSuppliers = suppliers.Count(s => s.Status == "Inactive")
                    },
                    supplierPerformance = supplierPerformance.OrderByDescending(s => s.totalPurchaseValue)
                };
            }
            catch (Exception ex)
            {
                throw new Exception($"Error generating supplier report: {ex.Message}", ex);
            }
        }

        public async Task<object> GetUserActivityReportAsync()
        {
            try
            {
                var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);

                var userSales = await _context.Sales
                    .Include(s => s.User)
                    .Where(s => s.SaleDate >= thirtyDaysAgo && s.Status == "Completed")
                    .GroupBy(s => new { s.UserId, s.User.FullName, s.User.UserType })
                    .Select(g => new
                    {
                        userId = g.Key.UserId,
                        userName = g.Key.FullName,
                        userType = g.Key.UserType,
                        totalSales = g.Sum(s => s.TotalAmount),
                        transactionCount = g.Count(),
                        averageTransaction = g.Average(s => s.TotalAmount)
                    })
                    .OrderByDescending(x => x.totalSales)
                    .ToListAsync();

                var userMessages = await _context.Messages
                    .Include(m => m.FromUser)
                    .Where(m => m.SentDate >= thirtyDaysAgo)
                    .GroupBy(m => new { m.FromUserId, m.FromUser.FullName })
                    .Select(g => new
                    {
                        userId = g.Key.FromUserId,
                        userName = g.Key.FullName,
                        messagesSent = g.Count(),
                        remindersSent = g.Count(m => m.MessageType == "Reminder"),
                        promotionsSent = g.Count(m => m.MessageType == "Promotion")
                    })
                    .ToListAsync();

                return new
                {
                    salesActivity = userSales,
                    messageActivity = userMessages,
                    period = new
                    {
                        startDate = thirtyDaysAgo.ToString("yyyy-MM-dd"),
                        endDate = DateTime.UtcNow.ToString("yyyy-MM-dd")
                    }
                };
            }
            catch (Exception ex)
            {
                throw new Exception($"Error generating user activity report: {ex.Message}", ex);
            }
        }

        public async Task<byte[]> GenerateSalesReceiptAsync(int saleId)
        {
            try
            {
                var sale = await _context.Sales
                    .Include(s => s.User)
                    .Include(s => s.SaleItems)
                        .ThenInclude(si => si.Product)
                            .ThenInclude(p => p.Category)
                    .FirstOrDefaultAsync(s => s.SaleId == saleId);

                if (sale == null)
                    throw new ArgumentException("Sale not found");

                var receipt = GenerateReceiptHtml(sale);
                return Encoding.UTF8.GetBytes(receipt);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error generating sales receipt: {ex.Message}", ex);
            }
        }

        public async Task<byte[]> GeneratePurchaseRequestReceiptAsync(int purchaseRequestId)
        {
            try
            {
                var purchaseRequest = await _context.PurchaseRequests
                    .Include(pr => pr.User)
                    .Include(pr => pr.Supplier)
                    .Include(pr => pr.PurchaseRequestItems)
                        .ThenInclude(pri => pri.Product)
                            .ThenInclude(p => p.Category)
                    .FirstOrDefaultAsync(pr => pr.PurchaseRequestId == purchaseRequestId);

                if (purchaseRequest == null)
                    throw new ArgumentException("Purchase request not found");

                var receipt = GeneratePurchaseRequestHtml(purchaseRequest);
                return Encoding.UTF8.GetBytes(receipt);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error generating purchase request receipt: {ex.Message}", ex);
            }
        }

        private async Task<object> GetSalesChartDataAsync()
        {
            var sevenDaysAgo = DateTime.Today.AddDays(-7);

            var salesData = await _context.Sales
                .Where(s => s.SaleDate >= sevenDaysAgo && s.Status == "Completed")
                .GroupBy(s => s.SaleDate.Date)
                .Select(g => new
                {
                    date = g.Key,
                    amount = g.Sum(s => s.TotalAmount)
                })
                .OrderBy(x => x.date)
                .ToListAsync();

            // Fill missing dates with zero
            var result = new List<object>();
            for (int i = 6; i >= 0; i--)
            {
                var date = DateTime.Today.AddDays(-i);
                var dayData = salesData.FirstOrDefault(s => s.date == date);
                result.Add(new
                {
                    date = date.ToString("MMM dd"),
                    amount = dayData?.amount ?? 0
                });
            }

            return result;
        }

        private async Task<object> GetTopProductsAsync()
        {
            var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);

            var topProducts = await _context.SaleItems
                .Include(si => si.Product)
                .Include(si => si.Sale)
                .Where(si => si.Sale.SaleDate >= thirtyDaysAgo && si.Sale.Status == "Completed")
                .GroupBy(si => new { si.ProductId, si.Product.Name })
                .Select(g => new
                {
                    productId = g.Key.ProductId,
                    productName = g.Key.Name,
                    quantitySold = g.Sum(si => si.Quantity),
                    revenue = g.Sum(si => si.TotalPrice)
                })
                .OrderByDescending(x => x.revenue)
                .Take(5)
                .ToListAsync();

            return topProducts;
        }

        private string GenerateReceiptHtml(Sale sale)
        {
            var html = new StringBuilder();

            html.AppendLine("<!DOCTYPE html>");
            html.AppendLine("<html><head>");
            html.AppendLine("<title>Sales Receipt</title>");
            html.AppendLine("<style>");
            html.AppendLine("body { font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; }");
            html.AppendLine(".header { text-align: center; border-bottom: 2px solid #333; padding-bottom: 20px; margin-bottom: 20px; }");
            html.AppendLine(".receipt-info { margin-bottom: 20px; }");
            html.AppendLine("table { width: 100%; border-collapse: collapse; margin-bottom: 20px; }");
            html.AppendLine("th, td { padding: 8px; text-align: left; border-bottom: 1px solid #ddd; }");
            html.AppendLine("th { background-color: #f5f5f5; }");
            html.AppendLine(".total-row { font-weight: bold; background-color: #f0f0f0; }");
            html.AppendLine(".footer { text-align: center; margin-top: 30px; font-size: 12px; color: #666; }");
            html.AppendLine("</style>");
            html.AppendLine("</head><body>");

            // Header
            html.AppendLine("<div class='header'>");
            html.AppendLine("<h1>PIXELSOLUTION LTD</h1>");
            html.AppendLine("<p>Advanced Sales Management System</p>");
            html.AppendLine("<p>Nairobi, Kenya | Phone: +254700000000</p>");
            html.AppendLine("</div>");

            // Receipt Info
            html.AppendLine("<div class='receipt-info'>");
            html.AppendLine($"<p><strong>Receipt No:</strong> {sale.SaleNumber}</p>");
            html.AppendLine($"<p><strong>Date:</strong> {sale.SaleDate:dd/MM/yyyy HH:mm}</p>");
            html.AppendLine($"<p><strong>Sales Person:</strong> {sale.User.FullName}</p>");
            if (!string.IsNullOrEmpty(sale.CustomerName))
            {
                html.AppendLine($"<p><strong>Customer:</strong> {sale.CustomerName}</p>");
            }
            html.AppendLine("</div>");

            // Items Table
            html.AppendLine("<table>");
            html.AppendLine("<thead>");
            html.AppendLine("<tr><th>Item</th><th>Qty</th><th>Price</th><th>Total</th></tr>");
            html.AppendLine("</thead>");
            html.AppendLine("<tbody>");

            foreach (var item in sale.SaleItems)
            {
                html.AppendLine("<tr>");
                html.AppendLine($"<td>{item.Product.Name}</td>");
                html.AppendLine($"<td>{item.Quantity}</td>");
                html.AppendLine($"<td>KSh {item.UnitPrice:N2}</td>");
                html.AppendLine($"<td>KSh {item.TotalPrice:N2}</td>");
                html.AppendLine("</tr>");
            }

            html.AppendLine("<tr class='total-row'>");
            html.AppendLine($"<td colspan='3'><strong>TOTAL</strong></td>");
            html.AppendLine($"<td><strong>KSh {sale.TotalAmount:N2}</strong></td>");
            html.AppendLine("</tr>");

            if (sale.AmountPaid > 0)
            {
                html.AppendLine("<tr>");
                html.AppendLine($"<td colspan='3'>Amount Paid</td>");
                html.AppendLine($"<td>KSh {sale.AmountPaid:N2}</td>");
                html.AppendLine("</tr>");

                if (sale.ChangeGiven > 0)
                {
                    html.AppendLine("<tr>");
                    html.AppendLine($"<td colspan='3'>Change</td>");
                    html.AppendLine($"<td>KSh {sale.ChangeGiven:N2}</td>");
                    html.AppendLine("</tr>");
                }
            }

            html.AppendLine("</tbody>");
            html.AppendLine("</table>");

            // Footer
            html.AppendLine("<div class='footer'>");
            html.AppendLine("<p>Thank you for your business!</p>");
            html.AppendLine("<p>All sales are final.</p>");
            html.AppendLine($"<p>Generated on: {DateTime.Now:dd/MM/yyyy HH:mm}</p>");
            html.AppendLine("</div>");

            html.AppendLine("</body></html>");

            return html.ToString();
        }

        private string GeneratePurchaseRequestHtml(PurchaseRequest purchaseRequest)
        {
            var html = new StringBuilder();

            html.AppendLine("<!DOCTYPE html>");
            html.AppendLine("<html><head>");
            html.AppendLine("<title>Purchase Request</title>");
            html.AppendLine("<style>");
            html.AppendLine("body { font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; }");
            html.AppendLine(".header { text-align: center; border-bottom: 2px solid #333; padding-bottom: 20px; margin-bottom: 20px; }");
            html.AppendLine(".request-info { margin-bottom: 20px; }");
            html.AppendLine("table { width: 100%; border-collapse: collapse; margin-bottom: 20px; }");
            html.AppendLine("th, td { padding: 8px; text-align: left; border-bottom: 1px solid #ddd; }");
            html.AppendLine("th { background-color: #f5f5f5; }");
            html.AppendLine(".total-row { font-weight: bold; background-color: #f0f0f0; }");
            html.AppendLine(".footer { text-align: center; margin-top: 30px; font-size: 12px; color: #666; }");
            html.AppendLine("</style>");
            html.AppendLine("</head><body>");

            // Header
            html.AppendLine("<div class='header'>");
            html.AppendLine("<h1>PURCHASE REQUEST</h1>");
            html.AppendLine("<h2>PIXELSOLUTION LTD</h2>");
            html.AppendLine("</div>");

            // Request Info
            html.AppendLine("<div class='request-info'>");
            html.AppendLine($"<p><strong>Request No:</strong> {purchaseRequest.RequestNumber}</p>");
            html.AppendLine($"<p><strong>Date:</strong> {purchaseRequest.RequestDate:dd/MM/yyyy}</p>");
            html.AppendLine($"<p><strong>Requested By:</strong> {purchaseRequest.User.FullName}</p>");
            html.AppendLine($"<p><strong>Supplier:</strong> {purchaseRequest.Supplier.CompanyName}</p>");
            html.AppendLine($"<p><strong>Contact:</strong> {purchaseRequest.Supplier.ContactPerson} - {purchaseRequest.Supplier.Phone}</p>");
            html.AppendLine($"<p><strong>Status:</strong> {purchaseRequest.Status}</p>");
            html.AppendLine("</div>");

            // Items Table
            html.AppendLine("<table>");
            html.AppendLine("<thead>");
            html.AppendLine("<tr><th>Item</th><th>Qty</th><th>Unit Price</th><th>Total</th></tr>");
            html.AppendLine("</thead>");
            html.AppendLine("<tbody>");

            foreach (var item in purchaseRequest.PurchaseRequestItems)
            {
                html.AppendLine("<tr>");
                html.AppendLine($"<td>{item.Product.Name}</td>");
                html.AppendLine($"<td>{item.Quantity}</td>");
                html.AppendLine($"<td>KSh {item.UnitPrice:N2}</td>");
                html.AppendLine($"<td>KSh {item.TotalPrice:N2}</td>");
                html.AppendLine("</tr>");
            }

            html.AppendLine("<tr class='total-row'>");
            html.AppendLine($"<td colspan='3'><strong>TOTAL AMOUNT</strong></td>");
            html.AppendLine($"<td><strong>KSh {purchaseRequest.TotalAmount:N2}</strong></td>");
            html.AppendLine("</tr>");

            html.AppendLine("</tbody>");
            html.AppendLine("</table>");

            if (!string.IsNullOrEmpty(purchaseRequest.Notes))
            {
                html.AppendLine("<div>");
                html.AppendLine("<p><strong>Notes:</strong></p>");
                html.AppendLine($"<p>{purchaseRequest.Notes}</p>");
                html.AppendLine("</div>");
            }

            // Footer
            html.AppendLine("<div class='footer'>");
            html.AppendLine($"<p>Generated on: {DateTime.Now:dd/MM/yyyy HH:mm}</p>");
            html.AppendLine("</div>");

            html.AppendLine("</body></html>");

            return html.ToString();
        }

        // Generate comprehensive Sales Report as PDF
        public async Task<byte[]> GenerateSalesReportAsync(DateTime startDate, DateTime endDate)
        {
            var sales = await _context.Sales
                .Include(s => s.User)
                .Include(s => s.SaleItems)
                    .ThenInclude(si => si.Product)
                .Where(s => s.SaleDate >= startDate && s.SaleDate <= endDate && s.Status == "Completed")
                .OrderByDescending(s => s.SaleDate)
                .ToListAsync();

            var html = GenerateSalesReportHtml(sales, startDate, endDate);
            return Encoding.UTF8.GetBytes(html);
        }

        // Generate other report methods
        public async Task<byte[]> GenerateInventoryReportAsync()
        {
            var products = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.Supplier)
                .Where(p => p.IsActive)
                .OrderBy(p => p.Name)
                .ToListAsync();

            var html = GenerateInventoryReportHtml(products);
            return Encoding.UTF8.GetBytes(html);
        }

        public async Task<byte[]> GenerateUserReportAsync()
        {
            var users = await _context.Users
                .Where(u => u.Status == "Active")
                .OrderBy(u => u.FullName)
                .ToListAsync();

            var html = GenerateUserReportHtml(users);
            return Encoding.UTF8.GetBytes(html);
        }

        public async Task<byte[]> GenerateCategoriesReportAsync()
        {
            var categories = await _context.Categories
                .Include(c => c.Products)
                .Where(c => c.IsActive)
                .OrderBy(c => c.Name)
                .ToListAsync();

            var html = GenerateCategoriesReportHtml(categories);
            return Encoding.UTF8.GetBytes(html);
        }

        // HTML Generation Methods
        private string GenerateSalesReportHtml(List<Sale> sales, DateTime startDate, DateTime endDate)
        {
            var html = new StringBuilder();
            var totalRevenue = sales.Sum(s => s.TotalAmount);

            html.AppendLine("<html><body>");
            html.AppendLine($"<h1>Sales Report ({startDate:dd/MM/yyyy} - {endDate:dd/MM/yyyy})</h1>");
            html.AppendLine($"<p>Total Sales: {sales.Count}</p>");
            html.AppendLine($"<p>Total Revenue: KSh {totalRevenue:N2}</p>");
            html.AppendLine("<table border='1'>");
            html.AppendLine("<tr><th>Date</th><th>Sale #</th><th>Customer</th><th>Amount</th><th>Cashier</th></tr>");

            foreach (var sale in sales)
            {
                html.AppendLine($"<tr><td>{sale.SaleDate:dd/MM/yyyy}</td><td>{sale.SaleNumber}</td><td>{sale.CustomerName ?? "Walk-in"}</td><td>KSh {sale.TotalAmount:N2}</td><td>{sale.User?.FullName ?? "Unknown"}</td></tr>");
            }

            html.AppendLine("</table></body></html>");
            return html.ToString();
        }

        private string GenerateInventoryReportHtml(List<Product> products)
        {
            var html = new StringBuilder();
            var totalValue = products.Sum(p => p.StockQuantity * p.SellingPrice);

            html.AppendLine("<html><body>");
            html.AppendLine($"<h1>Inventory Report</h1>");
            html.AppendLine($"<p>Total Products: {products.Count}</p>");
            html.AppendLine($"<p>Total Stock Value: KSh {totalValue:N2}</p>");
            html.AppendLine("<table border='1'>");
            html.AppendLine("<tr><th>Product</th><th>SKU</th><th>Category</th><th>Stock</th><th>Unit Price</th><th>Stock Value</th></tr>");

            foreach (var product in products)
            {
                var stockValue = product.StockQuantity * product.SellingPrice;
                html.AppendLine($"<tr><td>{product.Name}</td><td>{product.SKU}</td><td>{product.Category?.Name ?? "N/A"}</td><td>{product.StockQuantity}</td><td>KSh {product.SellingPrice:N2}</td><td>KSh {stockValue:N2}</td></tr>");
            }

            html.AppendLine("</table></body></html>");
            return html.ToString();
        }

        private string GenerateUserReportHtml(List<User> users)
        {
            var html = new StringBuilder();

            html.AppendLine("<html><body>");
            html.AppendLine($"<h1>Users Report</h1>");
            html.AppendLine($"<p>Total Active Users: {users.Count}</p>");
            html.AppendLine("<table border='1'>");
            html.AppendLine("<tr><th>Name</th><th>Email</th><th>Phone</th><th>Type</th><th>Created</th></tr>");

            foreach (var user in users)
            {
                html.AppendLine($"<tr><td>{user.FullName}</td><td>{user.Email}</td><td>{user.Phone ?? "N/A"}</td><td>{user.UserType}</td><td>{user.CreatedAt:dd/MM/yyyy}</td></tr>");
            }

            html.AppendLine("</table></body></html>");
            return html.ToString();
        }

        private string GenerateCategoriesReportHtml(List<Category> categories)
        {
            var html = new StringBuilder();
            var totalProducts = categories.Sum(c => c.Products.Count);

            html.AppendLine("<html><body>");
            html.AppendLine($"<h1>Categories Report</h1>");
            html.AppendLine($"<p>Total Categories: {categories.Count}</p>");
            html.AppendLine($"<p>Total Products: {totalProducts}</p>");
            html.AppendLine("<table border='1'>");
            html.AppendLine("<tr><th>Category</th><th>Description</th><th>Products</th><th>Created</th></tr>");

            foreach (var category in categories)
            {
                html.AppendLine($"<tr><td>{category.Name}</td><td>{category.Description ?? "N/A"}</td><td>{category.Products.Count}</td><td>{category.CreatedAt:dd/MM/yyyy}</td></tr>");
            }

            html.AppendLine("</table></body></html>");
            return html.ToString();
        }
    }
}