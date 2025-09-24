using Microsoft.EntityFrameworkCore;
using PixelSolution.Data;
using PixelSolution.Models;
using PixelSolution.Services.Interfaces;
using PixelSolution.ViewModels;
using iTextSharp.text;
using iTextSharp.text.pdf;
using ClosedXML.Excel;
using System.IO;
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

        public async Task<DashboardViewModel> GetDashboardDataAsync()
        {
            try
            {
                var today = DateTime.Today;
                var thisMonth = new DateTime(today.Year, today.Month, 1);
                var lastMonth = thisMonth.AddMonths(-1);

                // Today's sales - use TotalAmount for consistency
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

                // Calculate growth percentage with proper decimal handling
                var salesGrowth = lastMonthSales > 0
                    ? Math.Round(((thisMonthSales - lastMonthSales) / lastMonthSales) * 100, 1)
                    : (thisMonthSales > 0 ? 100 : 0);

                // Calculate profit (assuming 30% profit margin for demo)
                var todayProfit = todaySales * 0.3m;
                var thisMonthProfit = thisMonthSales * 0.3m;

                // Total stock value
                var totalStockValue = await _context.Products
                    .Where(p => p.IsActive)
                    .SumAsync(p => p.StockQuantity * p.SellingPrice);

                // Total orders this month
                var thisMonthOrders = await _context.Sales
                    .Where(s => s.SaleDate >= thisMonth && s.Status == "Completed")
                    .CountAsync();

                // Products sold this month
                var thisMonthProductsSold = await _context.SaleItems
                    .Where(si => si.Sale.SaleDate >= thisMonth && si.Sale.Status == "Completed")
                    .SumAsync(si => si.Quantity);

                // New customers this month
                var newCustomers = await _context.Sales
                    .Where(s => s.SaleDate >= thisMonth && !string.IsNullOrEmpty(s.CustomerEmail))
                    .Select(s => s.CustomerEmail) 
                    .Distinct()
                    .CountAsync();

                // Get sales chart data
                var salesChartData = await GetSalesChartDataAsync();

                // Get top products
                var topProducts = await GetTopProductsAsync();

                // Recent sales
                var recentSales = await _context.Sales
                    .Include(s => s.User)
                    .Include(s => s.SaleItems)
                        .ThenInclude(si => si.Product)
                    .OrderByDescending(s => s.SaleDate)
                    .Take(10)
                    .Select(s => new RecentSaleViewModel
                    {
                        SaleId = s.SaleId,
                        SaleNumber = s.SaleNumber,
                        CustomerName = string.IsNullOrEmpty(s.CustomerName) ? "Walk-in Customer" : s.CustomerName,
                        ProductNames = string.Join(", ", s.SaleItems.Select(si => si.Product.Name).Take(2)),
                        TotalAmount = s.TotalAmount,
                        Status = s.Status,
                        SaleDate = s.SaleDate
                    })
                    .ToListAsync();

                // Get sidebar counts
                var sidebarCounts = await GetSidebarCountsAsync();

                return new DashboardViewModel
                {
                    Stats = new DashboardStatsViewModel
                    {
                        TodaySales = todaySales,
                        TodayOrders = todaySalesCount,
                        ThisMonthSales = thisMonthSales,
                        LastMonthSales = lastMonthSales,
                        SalesGrowth = salesGrowth,
                        ProductsSoldToday = thisMonthProductsSold,
                        NewCustomersThisMonth = newCustomers,
                        TodayProfit = todayProfit,
                        StockValue = totalStockValue
                    },
                    Charts = new DashboardChartsViewModel
                    {
                        SalesData = salesChartData,
                        TopProducts = topProducts
                    },
                    RecentSales = recentSales,
                    SidebarCounts = sidebarCounts
                };
            }
            catch (Exception ex)
            {
                throw new Exception($"Error generating dashboard data: {ex.Message}", ex);
            }
        }

        public async Task<DashboardViewModel> GetEmployeeDashboardDataAsync(int employeeId)
        {
            try
            {
                var today = DateTime.Today;
                var thisMonth = new DateTime(today.Year, today.Month, 1);
                var lastMonth = thisMonth.AddMonths(-1);

                // Today's transaction count for this employee only (non-sensitive)
                var todayTransactions = await _context.Sales
                    .Where(s => s.SaleDate.Date == today && s.Status == "Completed" && s.UserId == employeeId)
                    .CountAsync();

                // This month's transactions for this employee
                var thisMonthTransactions = await _context.Sales
                    .Where(s => s.SaleDate >= thisMonth && s.Status == "Completed" && s.UserId == employeeId)
                    .CountAsync();

                var lastMonthTransactions = await _context.Sales
                    .Where(s => s.SaleDate >= lastMonth && s.SaleDate < thisMonth && s.Status == "Completed" && s.UserId == employeeId)
                    .CountAsync();

                // Calculate transaction growth percentage
                var transactionGrowth = lastMonthTransactions > 0
                    ? Math.Round(((thisMonthTransactions - lastMonthTransactions) / (decimal)lastMonthTransactions) * 100, 1)
                    : (thisMonthTransactions > 0 ? 100 : 0);

                // Products sold today by this employee
                var todayProductsSold = await _context.SaleItems
                    .Where(si => si.Sale.SaleDate.Date == today && si.Sale.Status == "Completed" && si.Sale.UserId == employeeId)
                    .SumAsync(si => si.Quantity);

                // This month's products sold for this employee
                var thisMonthProductsSold = await _context.SaleItems
                    .Where(si => si.Sale.SaleDate >= thisMonth && si.Sale.Status == "Completed" && si.Sale.UserId == employeeId)
                    .SumAsync(si => si.Quantity);

                // Customers served today by this employee
                var todayCustomersServed = await _context.Sales
                    .Where(s => s.SaleDate.Date == today && s.Status == "Completed" && s.UserId == employeeId)
                    .Select(s => s.CustomerName)
                    .Where(cn => !string.IsNullOrEmpty(cn))
                    .Distinct()
                    .CountAsync();

                // Total stock count (general information, not sensitive)
                var totalStockCount = await _context.Products
                    .Where(p => p.IsActive)
                    .SumAsync(p => p.StockQuantity);

                // Transaction chart data for this employee (current year by month) - non-sensitive
                var currentYear = DateTime.Now.Year;
                var salesChartData = new List<SaleChartDataViewModel>();
                for (int month = 1; month <= 12; month++)
                {
                    var monthStart = new DateTime(currentYear, month, 1);
                    var monthEnd = monthStart.AddMonths(1).AddDays(-1);
                    
                    var monthTransactions = await _context.Sales
                        .Where(s => s.SaleDate >= monthStart && s.SaleDate <= monthEnd && s.Status == "Completed" && s.UserId == employeeId)
                        .CountAsync();

                    salesChartData.Add(new SaleChartDataViewModel
                    {
                        DateLabel = monthStart.ToString("MMM"),
                        Amount = monthTransactions // Using transaction count instead of amount
                    });
                }

                // Top products sold by this employee (quantity only, no pricing)
                var topProducts = await _context.SaleItems
                    .Include(si => si.Product)
                    .Where(si => si.Sale.Status == "Completed" && si.Sale.UserId == employeeId)
                    .GroupBy(si => new { si.Product.ProductId, si.Product.Name })
                    .Select(g => new TopProductViewModel
                    {
                        Name = g.Key.Name,
                        QuantitySold = g.Sum(si => si.Quantity)
                    })
                    .OrderByDescending(tp => tp.QuantitySold)
                    .Take(5)
                    .ToListAsync();

                // Recent sales for this employee
                var recentSales = await _context.Sales
                    .Where(s => s.Status == "Completed" && s.UserId == employeeId)
                    .OrderByDescending(s => s.SaleDate)
                    .Take(10)
                    .Select(s => new RecentSaleViewModel
                    {
                        SaleNumber = s.SaleNumber,
                        CustomerName = s.CustomerName ?? "Walk-in Customer",
                        TotalAmount = s.TotalAmount,
                        SaleDate = s.SaleDate,
                        Status = s.Status
                    })
                    .ToListAsync();

                // Sidebar counts for this employee
                var sidebarCounts = await GetEmployeeSidebarCountsAsync(employeeId);

                return new DashboardViewModel
                {
                    Stats = new DashboardStatsViewModel
                    {
                        TodaySales = totalStockCount, // Show total stock instead of sales amount
                        TodayOrders = todayTransactions,
                        ThisMonthSales = thisMonthTransactions,
                        LastMonthSales = lastMonthTransactions,
                        SalesGrowth = transactionGrowth,
                        ProductsSoldToday = todayProductsSold,
                        NewCustomersThisMonth = todayCustomersServed
                    },
                    Charts = new DashboardChartsViewModel
                    {
                        SalesData = salesChartData,
                        TopProducts = topProducts
                    },
                    RecentSales = recentSales,
                    SidebarCounts = sidebarCounts
                };
            }
            catch (Exception ex)
            {
                throw new Exception($"Error generating employee dashboard data: {ex.Message}", ex);
            }
        }

        public async Task<SidebarCountsViewModel> GetEmployeeSidebarCountsAsync(int employeeId)
        {
            var today = DateTime.Today;

            // Count today's sales for this employee
            var todaySalesCount = await _context.Sales
                .Where(s => s.SaleDate.Date == today && s.Status == "Completed" && s.UserId == employeeId)
                .CountAsync();

            // Count unread messages for this employee
            var unreadMessagesCount = 0;
            try
            {
                unreadMessagesCount = await _context.Messages
                    .Where(m => !m.IsRead && m.ToUserId == employeeId)
                    .CountAsync();
            }
            catch
            {
                unreadMessagesCount = 0;
            }

            return new SidebarCountsViewModel
            {
                TodaySales = todaySalesCount,
                LowStock = 0, // Employees don't need to see inventory alerts
                PendingRequests = 0, // Employees don't handle purchase requests
                UnreadMessages = unreadMessagesCount
            };
        }

        public async Task<SidebarCountsViewModel> GetSidebarCountsAsync()
        {
            var today = DateTime.Today;
            var thisMonth = new DateTime(today.Year, today.Month, 1);

            // Count today's sales
            var todaySalesCount = await _context.Sales
                .Where(s => s.SaleDate.Date == today && s.Status == "Completed")
                .CountAsync();

            // Count low stock products (less than 10 items)
            var lowStockCount = await _context.Products
                .Where(p => p.StockQuantity < 10)
                .CountAsync();

            // Count pending purchase requests
            var pendingRequestsCount = await _context.PurchaseRequests
                .Where(pr => pr.Status == "Pending")
                .CountAsync();

            // Count unread messages (assuming there's a Messages table)
            var unreadMessagesCount = 0;
            try
            {
                unreadMessagesCount = await _context.Messages
                    .Where(m => !m.IsRead)
                    .CountAsync();
            }
            catch
            {
                // If Messages table doesn't exist, default to 0
                unreadMessagesCount = 0;
            }

            return new SidebarCountsViewModel
            {
                TodaySales = todaySalesCount,
                LowStock = lowStockCount,
                PendingRequests = pendingRequestsCount,
                UnreadMessages = unreadMessagesCount
            };
        }

        public async Task<object> GetSalesReportDataAsync(DateTime startDate, DateTime endDate)
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

                return new
                {
                    Summary = new
                    {
                        TotalSales = totalSales,
                        TotalTransactions = totalTransactions,
                        AverageTransaction = averageTransaction,
                        Period = new
                        {
                            StartDate = startDate.ToString("yyyy-MM-dd"),
                            EndDate = endDate.ToString("yyyy-MM-dd")
                        }
                    },
                    SalesByDate = salesByDate
                };
            }
            catch (Exception ex)
            {
                throw new Exception($"Error generating sales report: {ex.Message}", ex);
            }
        }

        public async Task<object> GetInventoryReportDataAsync()
        {
            try
            {
                var products = await _context.Products
                    .Include(p => p.Category)
                    .Include(p => p.Supplier)
                    .Where(p => p.Status == "Active")
                    .ToListAsync();

                var totalProducts = products.Count;
                var totalStockValue = products.Sum(p => p.StockQuantity * p.BuyingPrice);
                var lowStockProducts = products.Where(p => p.StockQuantity <= p.MinStockLevel).ToList();

                return new
                {
                    Summary = new
                    {
                        TotalProducts = totalProducts,
                        TotalStockValue = totalStockValue,
                        LowStockProducts = lowStockProducts.Count
                    },
                    Products = products.Select(p => new
                    {
                        p.ProductId,
                        p.Name,
                        p.SKU,
                        Category = p.Category?.Name ?? "No Category",
                        p.StockQuantity,
                        p.BuyingPrice,
                        StockValue = p.StockQuantity * p.BuyingPrice,
                        p.MinStockLevel,
                        IsLowStock = p.StockQuantity <= p.MinStockLevel
                    }).ToList()
                };
            }
            catch (Exception ex)
            {
                throw new Exception($"Error generating inventory report: {ex.Message}", ex);
            }
        }

        private async Task<byte[]> GenerateSalesReceiptInternalAsync(int saleId)
        {
            try
            {
                var sale = await _context.Sales
                    .Include(s => s.User)
                    .Include(s => s.SaleItems)
                        .ThenInclude(si => si.Product)
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

        public async Task<List<SaleChartDataViewModel>> GetSalesChartDataAsync()
        {
            var currentYear = DateTime.Today.Year;
            var startOfYear = new DateTime(currentYear, 1, 1);

            // First check if we have any sales data at all
            var totalSalesCount = await _context.Sales.CountAsync();
            var completedSalesCount = await _context.Sales.Where(s => s.Status == "Completed").CountAsync();
            var currentYearSalesCount = await _context.Sales.Where(s => s.SaleDate >= startOfYear).CountAsync();

            Console.WriteLine($"DEBUG: Total sales: {totalSalesCount}, Completed: {completedSalesCount}, Current year: {currentYearSalesCount}");

            var salesData = await _context.Sales
                .Where(s => s.SaleDate >= startOfYear && s.Status == "Completed")
                .GroupBy(s => new { s.SaleDate.Year, s.SaleDate.Month })
                .Select(g => new
                {
                    year = g.Key.Year,
                    month = g.Key.Month,
                    amount = g.Sum(s => s.TotalAmount)
                })
                .OrderBy(x => x.month)
                .ToListAsync();

            Console.WriteLine($"DEBUG: Sales data query result: {System.Text.Json.JsonSerializer.Serialize(salesData)}");

            // If no current year data, get data from any year for demonstration
            if (!salesData.Any())
            {
                Console.WriteLine("DEBUG: No current year data, getting all completed sales");
                salesData = await _context.Sales
                    .Where(s => s.Status == "Completed")
                    .GroupBy(s => new { s.SaleDate.Year, s.SaleDate.Month })
                    .Select(g => new
                    {
                        year = g.Key.Year,
                        month = g.Key.Month,
                        amount = g.Sum(s => s.TotalAmount)
                    })
                    .OrderByDescending(x => x.year).ThenBy(x => x.month)
                    .Take(12)
                    .ToListAsync();
            }

            // Fill missing months with zero
            var result = new List<SaleChartDataViewModel>();
            for (int month = 1; month <= 12; month++)
            {
                var monthData = salesData.FirstOrDefault(s => s.month == month);
                var monthName = new DateTime(currentYear, month, 1).ToString("MMM");
                result.Add(new SaleChartDataViewModel
                {
                    Date = new DateTime(currentYear, month, 1),
                    DateLabel = monthName,
                    Amount = monthData?.amount ?? 0,
                    OrderCount = monthData?.amount > 0 ? 1 : 0
                });
            }

            Console.WriteLine($"DEBUG: Final chart data: {System.Text.Json.JsonSerializer.Serialize(result)}");
            return result;
        }

        public async Task<List<TopProductViewModel>> GetTopProductsAsync()
        {
            var currentYear = DateTime.Today.Year;
            var startOfYear = new DateTime(currentYear, 1, 1);

            // Check if we have any sale items
            var totalSaleItems = await _context.SaleItems.CountAsync();
            Console.WriteLine($"DEBUG: Total sale items in database: {totalSaleItems}");

            var topProductsData = await _context.SaleItems
                .Include(si => si.Product)
                .Include(si => si.Sale)
                .Where(si => si.Sale.SaleDate >= startOfYear && si.Sale.Status == "Completed")
                .GroupBy(si => new { si.ProductId, si.Product.Name, si.Product.SKU })
                .Select(g => new
                {
                    productId = g.Key.ProductId,
                    productName = g.Key.Name,
                    sku = g.Key.SKU,
                    salesCount = g.Count(),
                    quantitySold = g.Sum(si => si.Quantity),
                    revenue = g.Sum(si => si.TotalPrice)
                })
                .OrderByDescending(x => x.quantitySold)
                .Take(7)
                .ToListAsync();

            Console.WriteLine($"DEBUG: Top products query result: {System.Text.Json.JsonSerializer.Serialize(topProductsData)}");

            // If no current year data, get data from any completed sales
            if (!topProductsData.Any())
            {
                Console.WriteLine("DEBUG: No current year product data, getting all completed sales");
                topProductsData = await _context.SaleItems
                    .Include(si => si.Product)
                    .Include(si => si.Sale)
                    .Where(si => si.Sale.Status == "Completed")
                    .GroupBy(si => new { si.ProductId, si.Product.Name, si.Product.SKU })
                    .Select(g => new
                    {
                        productId = g.Key.ProductId,
                        productName = g.Key.Name,
                        sku = g.Key.SKU,
                        salesCount = g.Count(),
                        quantitySold = g.Sum(si => si.Quantity),
                        revenue = g.Sum(si => si.TotalPrice)
                    })
                    .OrderByDescending(x => x.quantitySold)
                    .Take(7)
                    .ToListAsync();
            }

            // Convert to TopProductViewModel
            var topProducts = topProductsData.Select(p => new TopProductViewModel
            {
                ProductId = p.productId,
                Name = p.productName,
                SKU = p.sku,
                QuantitySold = p.quantitySold,
                Revenue = p.revenue,
                AvgPrice = p.quantitySold > 0 ? p.revenue / p.quantitySold : 0
            }).ToList();

            Console.WriteLine($"DEBUG: Final top products data: {System.Text.Json.JsonSerializer.Serialize(topProducts)}");
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
            html.AppendLine("table { width: 100%; border-collapse: collapse; margin-bottom: 20px; }");
            html.AppendLine("th, td { padding: 8px; text-align: left; border-bottom: 1px solid #ddd; }");
            html.AppendLine("th { background-color: #f5f5f5; }");
            html.AppendLine(".total-row { font-weight: bold; background-color: #f0f0f0; }");
            html.AppendLine("</style>");
            html.AppendLine("</head><body>");

            // Header
            html.AppendLine("<div class='header'>");
            html.AppendLine("<h1>PIXEL SOLUTION COMPANY LTD</h1>");
            html.AppendLine("<p>Advanced Sales Management System</p>");
            html.AppendLine("</div>");

            // Receipt Info
            html.AppendLine($"<p><strong>Receipt No:</strong> {sale.SaleNumber}</p>");
            html.AppendLine($"<p><strong>Date:</strong> {sale.SaleDate:dd/MM/yyyy HH:mm}</p>");
            html.AppendLine($"<p><strong>Sales Person:</strong> {sale.User.FirstName} {sale.User.LastName}</p>");
            if (!string.IsNullOrEmpty(sale.CustomerName))
            {
                html.AppendLine($"<p><strong>Customer:</strong> {sale.CustomerName}</p>");
            }

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
            html.AppendLine("</tbody>");
            html.AppendLine("</table>");

            html.AppendLine("<p>Thank you for your business!</p>");
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
            html.AppendLine("table { width: 100%; border-collapse: collapse; margin-bottom: 20px; }");
            html.AppendLine("th, td { padding: 8px; text-align: left; border-bottom: 1px solid #ddd; }");
            html.AppendLine("th { background-color: #f5f5f5; }");
            html.AppendLine(".total-row { font-weight: bold; background-color: #f0f0f0; }");
            html.AppendLine("</style>");
            html.AppendLine("</head><body>");

            // Header
            html.AppendLine("<div class='header'>");
            html.AppendLine("<h1>PIXEL SOLUTION COMPANY LTD</h1>");
            html.AppendLine("<h2>PURCHASE REQUEST</h2>");
            html.AppendLine("</div>");

            // Request Info
            html.AppendLine($"<p><strong>Request No:</strong> {purchaseRequest.RequestNumber}</p>");
            html.AppendLine($"<p><strong>Date:</strong> {purchaseRequest.RequestDate:dd/MM/yyyy}</p>");
            html.AppendLine($"<p><strong>Requested By:</strong> {purchaseRequest.User.FirstName} {purchaseRequest.User.LastName}</p>");
            html.AppendLine($"<p><strong>Supplier:</strong> {purchaseRequest.Supplier.CompanyName}</p>");
            html.AppendLine($"<p><strong>Status:</strong> {purchaseRequest.Status}</p>");

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

            return GenerateSalesReportPdf(sales, startDate, endDate);
        }

        public async Task<byte[]> GenerateInventoryReportAsync()
        {
            var products = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.Supplier)
                .Where(p => p.Status == "Active")
                .OrderBy(p => p.Name)
                .ToListAsync();

            return GenerateInventoryReportPdf(products);
        }

        public async Task<byte[]> GenerateUserReportAsync()
        {
            // Get the user activity report data
            var reportData = await GetUserActivityReportDataAsync();
            var data = (dynamic)reportData;
            
            return GenerateUserActivityReportPdf(data.Users, data.Summary, data.DetailedActivities);
        }

        public async Task<byte[]> GenerateCategoriesReportAsync()
        {
            var categories = await _context.Categories
                .Include(c => c.Products)
                .Where(c => c.Status == "Active")
                .OrderBy(c => c.Name)
                .ToListAsync();

            return GenerateCategoriesReportPdf(categories);
        }

        // PDF Generation Methods
        private byte[] GenerateSalesReportPdf(List<Sale> sales, DateTime startDate, DateTime endDate)
        {
            using (var stream = new MemoryStream())
            {
                var document = new iTextSharp.text.Document(PageSize.A4, 25, 25, 30, 30);
                var writer = PdfWriter.GetInstance(document, stream);
                
                document.Open();
                
                // Company Header
                var titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 18, BaseColor.DARK_GRAY);
                var companyTitle = new Paragraph("PIXEL SOLUTION COMPANY LTD", titleFont);
                companyTitle.Alignment = Element.ALIGN_CENTER;
                companyTitle.SpacingAfter = 10f;
                document.Add(companyTitle);
                
                // Report Title
                var reportTitleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 14, BaseColor.BLACK);
                var reportTitle = new Paragraph($"Sales Report ({startDate:dd/MM/yyyy} - {endDate:dd/MM/yyyy})", reportTitleFont);
                reportTitle.Alignment = Element.ALIGN_CENTER;
                reportTitle.SpacingAfter = 20f;
                document.Add(reportTitle);
                
                // Summary Statistics using TotalAmount
                var totalRevenue = sales.Sum(s => s.TotalAmount);
                var summaryFont = FontFactory.GetFont(FontFactory.HELVETICA, 10, BaseColor.BLACK);
                var summary = new Paragraph($"Total Sales: {sales.Count} | Total Revenue: KSh {totalRevenue:N2} | Generated: {DateTime.Now:dd/MM/yyyy HH:mm}", summaryFont);
                summary.Alignment = Element.ALIGN_CENTER;
                summary.SpacingAfter = 20f;
                document.Add(summary);
                
                // Create table
                var table = new PdfPTable(5);
                table.WidthPercentage = 100;
                table.SetWidths(new float[] { 15f, 15f, 25f, 20f, 25f });
                
                // Table headers
                var headerFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10, BaseColor.WHITE);
                var headers = new string[] { "Date", "Sale #", "Customer", "Amount (KSh)", "Cashier" };
                
                foreach (var header in headers)
                {
                    var cell = new PdfPCell(new Phrase(header, headerFont));
                    cell.BackgroundColor = BaseColor.DARK_GRAY;
                    cell.HorizontalAlignment = Element.ALIGN_CENTER;
                    cell.Padding = 8f;
                    table.AddCell(cell);
                }
                
                // Table data
                var dataFont = FontFactory.GetFont(FontFactory.HELVETICA, 9, BaseColor.BLACK);
                foreach (var sale in sales)
                {
                    table.AddCell(new PdfPCell(new Phrase(sale.SaleDate.ToString("dd/MM/yyyy"), dataFont)) { Padding = 5f });
                    table.AddCell(new PdfPCell(new Phrase(sale.SaleNumber ?? "N/A", dataFont)) { Padding = 5f });
                    table.AddCell(new PdfPCell(new Phrase(sale.CustomerName ?? "Walk-in", dataFont)) { Padding = 5f });
                    table.AddCell(new PdfPCell(new Phrase(sale.TotalAmount.ToString("N2"), dataFont)) { Padding = 5f, HorizontalAlignment = Element.ALIGN_RIGHT });
                    table.AddCell(new PdfPCell(new Phrase(sale.User?.FullName ?? "Unknown", dataFont)) { Padding = 5f });
                }
                
                document.Add(table);
                
                // Footer
                var footerFont = FontFactory.GetFont(FontFactory.HELVETICA_OBLIQUE, 8, BaseColor.GRAY);
                var footer = new Paragraph($"\nReport generated on {DateTime.Now:dd/MM/yyyy} at {DateTime.Now:HH:mm:ss}", footerFont);
                footer.Alignment = Element.ALIGN_CENTER;
                document.Add(footer);
                
                document.Close();
                return stream.ToArray();
            }
        }

        private byte[] GenerateInventoryReportPdf(List<Product> products)
        {
            using (var stream = new MemoryStream())
            {
                var document = new iTextSharp.text.Document(PageSize.A4, 25, 25, 30, 30);
                var writer = PdfWriter.GetInstance(document, stream);
                
                document.Open();
                
                // Company Header
                var titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 18, BaseColor.DARK_GRAY);
                var companyTitle = new Paragraph("PIXEL SOLUTION COMPANY LTD", titleFont);
                companyTitle.Alignment = Element.ALIGN_CENTER;
                companyTitle.SpacingAfter = 10f;
                document.Add(companyTitle);
                
                // Report Title
                var reportTitleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 14, BaseColor.BLACK);
                var reportTitle = new Paragraph("Inventory Report", reportTitleFont);
                reportTitle.Alignment = Element.ALIGN_CENTER;
                reportTitle.SpacingAfter = 20f;
                document.Add(reportTitle);
                
                // Summary Statistics
                var totalValue = products.Sum(p => p.StockQuantity * p.SellingPrice);
                var summaryFont = FontFactory.GetFont(FontFactory.HELVETICA, 10, BaseColor.BLACK);
                var summary = new Paragraph($"Total Products: {products.Count} | Total Stock Value: KSh {totalValue:N2} | Generated: {DateTime.Now:dd/MM/yyyy HH:mm}", summaryFont);
                summary.Alignment = Element.ALIGN_CENTER;
                summary.SpacingAfter = 20f;
                document.Add(summary);
                
                // Create table
                var table = new PdfPTable(6);
                table.WidthPercentage = 100;
                table.SetWidths(new float[] { 25f, 15f, 20f, 10f, 15f, 15f });
                
                // Table headers
                var headerFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10, BaseColor.WHITE);
                var headers = new string[] { "Product", "SKU", "Category", "Stock", "Price (KSh)", "Value (KSh)" };
                
                foreach (var header in headers)
                {
                    var cell = new PdfPCell(new Phrase(header, headerFont));
                    cell.BackgroundColor = BaseColor.DARK_GRAY;
                    cell.HorizontalAlignment = Element.ALIGN_CENTER;
                    cell.Padding = 8f;
                    table.AddCell(cell);
                }
                
                // Table data
                var dataFont = FontFactory.GetFont(FontFactory.HELVETICA, 9, BaseColor.BLACK);
                foreach (var product in products)
                {
                    var stockValue = product.StockQuantity * product.SellingPrice;
                    table.AddCell(new PdfPCell(new Phrase(product.Name ?? "N/A", dataFont)) { Padding = 5f });
                    table.AddCell(new PdfPCell(new Phrase(product.SKU ?? "N/A", dataFont)) { Padding = 5f });
                    table.AddCell(new PdfPCell(new Phrase(product.Category?.Name ?? "N/A", dataFont)) { Padding = 5f });
                    table.AddCell(new PdfPCell(new Phrase(product.StockQuantity.ToString(), dataFont)) { Padding = 5f, HorizontalAlignment = Element.ALIGN_RIGHT });
                    table.AddCell(new PdfPCell(new Phrase(product.SellingPrice.ToString("N2"), dataFont)) { Padding = 5f, HorizontalAlignment = Element.ALIGN_RIGHT });
                    table.AddCell(new PdfPCell(new Phrase(stockValue.ToString("N2"), dataFont)) { Padding = 5f, HorizontalAlignment = Element.ALIGN_RIGHT });
                }
                
                document.Add(table);
                
                // Footer
                var footerFont = FontFactory.GetFont(FontFactory.HELVETICA_OBLIQUE, 8, BaseColor.GRAY);
                var footer = new Paragraph($"\nReport generated on {DateTime.Now:dd/MM/yyyy} at {DateTime.Now:HH:mm:ss}", footerFont);
                footer.Alignment = Element.ALIGN_CENTER;
                document.Add(footer);
                
                document.Close();
                return stream.ToArray();
            }
        }

        private byte[] GenerateUserActivityReportPdf(dynamic users, dynamic summary, dynamic detailedActivities)
        {
            using (var stream = new MemoryStream())
            {
                var document = new iTextSharp.text.Document(PageSize.A4.Rotate(), 25, 25, 30, 30);
                var writer = PdfWriter.GetInstance(document, stream);
                
                document.Open();
                
                // Company Header
                var titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 18, BaseColor.DARK_GRAY);
                var companyTitle = new Paragraph("PIXEL SOLUTION COMPANY LTD", titleFont);
                companyTitle.Alignment = Element.ALIGN_CENTER;
                companyTitle.SpacingAfter = 10f;
                document.Add(companyTitle);
                
                // Report Title
                var reportTitleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 14, BaseColor.BLACK);
                var reportTitle = new Paragraph("Employee Activity Report", reportTitleFont);
                reportTitle.Alignment = Element.ALIGN_CENTER;
                reportTitle.SpacingAfter = 20f;
                document.Add(reportTitle);
                
                // Enhanced Summary Statistics focusing on employees
                var summaryFont = FontFactory.GetFont(FontFactory.HELVETICA, 10, BaseColor.BLACK);
                var summaryText = new Paragraph($"Total Employees: {summary.TotalEmployees} | Active Employees: {summary.ActiveEmployees} | Employees with Activity: {summary.EmployeesWithActivity} | Employee Activities: {summary.EmployeeActivities} | Top Performer: {summary.TopPerformingEmployee} | Generated: {DateTime.Now:dd/MM/yyyy HH:mm}", summaryFont);
                summaryText.Alignment = Element.ALIGN_CENTER;
                summaryText.SpacingAfter = 20f;
                document.Add(summaryText);
                
                // Employee Activity Summary Table
                var sectionFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 12, BaseColor.BLACK);
                var userSummaryTitle = new Paragraph("EMPLOYEE ACTIVITY SUMMARY", sectionFont);
                userSummaryTitle.SpacingAfter = 10f;
                document.Add(userSummaryTitle);
                
                var table = new PdfPTable(11);
                table.WidthPercentage = 100;
                table.SetWidths(new float[] { 12f, 15f, 8f, 8f, 8f, 8f, 8f, 8f, 8f, 8f, 12f });
                
                // Enhanced table headers for employee focus
                var headerFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 8, BaseColor.WHITE);
                var headers = new string[] { "Name", "Email", "Role", "Department", "Sales", "Total Activities", "Dashboard", "Messages", "Settings", "Employee Score", "Last Activity" };
                
                foreach (var header in headers)
                {
                    var cell = new PdfPCell(new Phrase(header, headerFont));
                    cell.BackgroundColor = BaseColor.DARK_GRAY;
                    cell.HorizontalAlignment = Element.ALIGN_CENTER;
                    cell.Padding = 5f;
                    table.AddCell(cell);
                }
                
                // Enhanced table data with employee focus
                var dataFont = FontFactory.GetFont(FontFactory.HELVETICA, 7, BaseColor.BLACK);
                var employeeFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 7, BaseColor.DARK_GRAY);
                
                foreach (var user in users)
                {
                    var isEmployee = user.UserType == "Employee";
                    var cellFont = isEmployee ? employeeFont : dataFont;
                    var bgColor = isEmployee ? new BaseColor(240, 248, 255) : BaseColor.WHITE; // Light blue for employees
                    
                    table.AddCell(new PdfPCell(new Phrase(user.FullName ?? "N/A", cellFont)) { Padding = 4f, BackgroundColor = bgColor });
                    table.AddCell(new PdfPCell(new Phrase(user.Email ?? "N/A", cellFont)) { Padding = 4f, BackgroundColor = bgColor });
                    table.AddCell(new PdfPCell(new Phrase(user.UserType ?? "N/A", cellFont)) { Padding = 4f, BackgroundColor = bgColor });
                    table.AddCell(new PdfPCell(new Phrase(user.Department ?? "N/A", cellFont)) { Padding = 4f, BackgroundColor = bgColor });
                    table.AddCell(new PdfPCell(new Phrase(user.SalesCount.ToString(), cellFont)) { Padding = 4f, HorizontalAlignment = Element.ALIGN_RIGHT, BackgroundColor = bgColor });
                    table.AddCell(new PdfPCell(new Phrase(user.TotalActivities.ToString(), cellFont)) { Padding = 4f, HorizontalAlignment = Element.ALIGN_RIGHT, BackgroundColor = bgColor });
                    table.AddCell(new PdfPCell(new Phrase(user.DashboardAccess.ToString(), cellFont)) { Padding = 4f, HorizontalAlignment = Element.ALIGN_RIGHT, BackgroundColor = bgColor });
                    table.AddCell(new PdfPCell(new Phrase(user.MessagesAccess.ToString(), cellFont)) { Padding = 4f, HorizontalAlignment = Element.ALIGN_RIGHT, BackgroundColor = bgColor });
                    table.AddCell(new PdfPCell(new Phrase(user.SettingsAccess.ToString(), cellFont)) { Padding = 4f, HorizontalAlignment = Element.ALIGN_RIGHT, BackgroundColor = bgColor });
                    table.AddCell(new PdfPCell(new Phrase(user.EmployeeEngagementScore.ToString(), cellFont)) { Padding = 4f, HorizontalAlignment = Element.ALIGN_RIGHT, BackgroundColor = bgColor });
                    table.AddCell(new PdfPCell(new Phrase(user.LastActivity?.ToString("dd/MM/yyyy HH:mm") ?? "Never", cellFont)) { Padding = 4f, BackgroundColor = bgColor });
                }
                
                document.Add(table);
                document.Add(new Paragraph(" "));
                
                // Employee Activity Log Section
                var detailTitle = new Paragraph("RECENT EMPLOYEE ACTIVITY LOG", sectionFont);
                detailTitle.SpacingAfter = 10f;
                document.Add(detailTitle);
                
                // Enhanced Detailed Activities Table
                var detailTable = new PdfPTable(6);
                detailTable.WidthPercentage = 100;
                detailTable.SetWidths(new float[] { 18f, 12f, 15f, 30f, 18f, 7f });
                
                // Enhanced detail table headers
                var detailHeaders = new string[] { "Employee", "Role", "Activity Type", "Description", "Date & Time", "Category" };
                foreach (var header in detailHeaders)
                {
                    var cell = new PdfPCell(new Phrase(header, headerFont));
                    cell.BackgroundColor = BaseColor.DARK_GRAY;
                    cell.HorizontalAlignment = Element.ALIGN_CENTER;
                    cell.Padding = 5f;
                    detailTable.AddCell(cell);
                }
                
                // Enhanced detail table data with employee focus
                foreach (var activity in detailedActivities)
                {
                    var isEmployeeActivity = activity.IsEmployeeActivity;
                    var activityFont = isEmployeeActivity ? employeeFont : dataFont;
                    var activityBgColor = isEmployeeActivity ? new BaseColor(240, 248, 255) : BaseColor.WHITE;
                    
                    detailTable.AddCell(new PdfPCell(new Phrase(activity.UserName ?? "Unknown", activityFont)) { Padding = 4f, BackgroundColor = activityBgColor });
                    detailTable.AddCell(new PdfPCell(new Phrase(activity.UserType ?? "N/A", activityFont)) { Padding = 4f, BackgroundColor = activityBgColor });
                    detailTable.AddCell(new PdfPCell(new Phrase(activity.ActivityType ?? "N/A", activityFont)) { Padding = 4f, BackgroundColor = activityBgColor });
                    detailTable.AddCell(new PdfPCell(new Phrase(activity.Description ?? "N/A", activityFont)) { Padding = 4f, BackgroundColor = activityBgColor });
                    detailTable.AddCell(new PdfPCell(new Phrase(activity.CreatedAt.ToString("dd/MM/yyyy HH:mm:ss"), activityFont)) { Padding = 4f, BackgroundColor = activityBgColor });
                    detailTable.AddCell(new PdfPCell(new Phrase(activity.ActivityCategory ?? "Other", activityFont)) { Padding = 4f, BackgroundColor = activityBgColor });
                }
                
                document.Add(detailTable);
                
                // Footer
                var footerFont = FontFactory.GetFont(FontFactory.HELVETICA_OBLIQUE, 8, BaseColor.GRAY);
                var footer = new Paragraph($"\nUser Activity Report generated on {DateTime.Now:dd/MM/yyyy} at {DateTime.Now:HH:mm:ss}\nPixel Solution Company Ltd", footerFont);
                footer.Alignment = Element.ALIGN_CENTER;
                document.Add(footer);
                
                document.Close();
                return stream.ToArray();
            }
        }

        private byte[] GenerateCategoriesReportPdf(List<Category> categories)
        {
            using (var stream = new MemoryStream())
            {
                var document = new iTextSharp.text.Document(PageSize.A4, 25, 25, 30, 30);
                var writer = PdfWriter.GetInstance(document, stream);
                
                document.Open();
                
                // Company Header
                var titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 18, BaseColor.DARK_GRAY);
                var companyTitle = new Paragraph("PIXEL SOLUTION COMPANY LTD", titleFont);
                companyTitle.Alignment = Element.ALIGN_CENTER;
                companyTitle.SpacingAfter = 10f;
                document.Add(companyTitle);
                
                // Report Title
                var reportTitleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 14, BaseColor.BLACK);
                var reportTitle = new Paragraph("Categories Report", reportTitleFont);
                reportTitle.Alignment = Element.ALIGN_CENTER;
                reportTitle.SpacingAfter = 20f;
                document.Add(reportTitle);
                
                // Summary Statistics
                var totalProducts = categories.Sum(c => c.Products.Count);
                var summaryFont = FontFactory.GetFont(FontFactory.HELVETICA, 10, BaseColor.BLACK);
                var summary = new Paragraph($"Total Categories: {categories.Count} | Total Products: {totalProducts} | Generated: {DateTime.Now:dd/MM/yyyy HH:mm}", summaryFont);
                summary.Alignment = Element.ALIGN_CENTER;
                summary.SpacingAfter = 20f;
                document.Add(summary);
                
                // Create table
                var table = new PdfPTable(4);
                table.WidthPercentage = 100;
                table.SetWidths(new float[] { 30f, 40f, 15f, 15f });
                
                // Table headers
                var headerFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10, BaseColor.WHITE);
                var headers = new string[] { "Category", "Description", "Products", "Status" };
                
                foreach (var header in headers)
                {
                    var cell = new PdfPCell(new Phrase(header, headerFont));
                    cell.BackgroundColor = BaseColor.DARK_GRAY;
                    cell.HorizontalAlignment = Element.ALIGN_CENTER;
                    cell.Padding = 8f;
                    table.AddCell(cell);
                }
                
                // Table data
                var dataFont = FontFactory.GetFont(FontFactory.HELVETICA, 9, BaseColor.BLACK);
                foreach (var category in categories)
                {
                    table.AddCell(new PdfPCell(new Phrase(category.Name ?? "N/A", dataFont)) { Padding = 5f });
                    table.AddCell(new PdfPCell(new Phrase(category.Description ?? "N/A", dataFont)) { Padding = 5f });
                    table.AddCell(new PdfPCell(new Phrase(category.Products.Count.ToString(), dataFont)) { Padding = 5f, HorizontalAlignment = Element.ALIGN_RIGHT });
                    table.AddCell(new PdfPCell(new Phrase(category.Status ?? "N/A", dataFont)) { Padding = 5f });
                }
                
                document.Add(table);
                
                // Footer
                var footerFont = FontFactory.GetFont(FontFactory.HELVETICA_OBLIQUE, 8, BaseColor.GRAY);
                var footer = new Paragraph($"\nReport generated on {DateTime.Now:dd/MM/yyyy} at {DateTime.Now:HH:mm:ss}", footerFont);
                footer.Alignment = Element.ALIGN_CENTER;
                document.Add(footer);
                
                document.Close();
                return stream.ToArray();
            }
        }

        // Excel Generation Methods
        public async Task<byte[]> GenerateSalesReportExcelAsync(DateTime startDate, DateTime endDate)
        {
            var sales = await _context.Sales
                .Include(s => s.User)
                .Include(s => s.SaleItems)
                    .ThenInclude(si => si.Product)
                .Where(s => s.SaleDate >= startDate && s.SaleDate <= endDate && s.Status == "Completed")
                .OrderByDescending(s => s.SaleDate)
                .ToListAsync();

            return GenerateSalesReportExcel(sales, startDate, endDate);
        }

        public async Task<byte[]> GenerateInventoryReportExcelAsync()
        {
            var products = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.Supplier)
                .Where(p => p.Status == "Active")
                .OrderBy(p => p.Name)
                .ToListAsync();

            return GenerateInventoryReportExcel(products);
        }

        public async Task<byte[]> GenerateUserReportExcelAsync()
        {
            var users = await _context.Users
                .Include(u => u.Department)
                .Where(u => u.Status == "Active")
                .OrderBy(u => u.FirstName)
                .ToListAsync();

            return GenerateUserReportExcel(users);
        }

        public async Task<byte[]> GenerateCategoriesReportExcelAsync()
        {
            var categories = await _context.Categories
                .Include(c => c.Products)
                .Where(c => c.Status == "Active")
                .OrderBy(c => c.Name)
                .ToListAsync();

            return GenerateCategoriesReportExcel(categories);
        }


        private byte[] GenerateSalesReportExcel(List<Sale> sales, DateTime startDate, DateTime endDate)
        {
            // Generate CSV format since Excel libraries are not available
            var content = new StringBuilder();
            content.AppendLine("PIXEL SOLUTION COMPANY LTD");
            content.AppendLine($"Sales Report ({startDate:dd/MM/yyyy} - {endDate:dd/MM/yyyy})");
            content.AppendLine($"Total Sales: {sales.Count} | Total Revenue: KSh {sales.Sum(s => s.TotalAmount):N2}");
            content.AppendLine();
            content.AppendLine("Date,Sale #,Customer,Amount (KSh),Cashier");
            
            foreach (var sale in sales)
            {
                content.AppendLine($"{sale.SaleDate:dd/MM/yyyy},{sale.SaleNumber ?? "N/A"},{sale.CustomerName ?? "Walk-in"},{sale.TotalAmount:N2},{sale.CashierName ?? "Unknown"}");
            }
            
            return Encoding.UTF8.GetBytes(content.ToString());
        }

        private byte[] GenerateInventoryReportExcel(List<Product> products)
        {
            // Generate CSV format since Excel libraries are not available
            var content = new StringBuilder();
            content.AppendLine("PIXEL SOLUTION COMPANY LTD");
            content.AppendLine("Inventory Report");
            var totalValue = products.Sum(p => p.StockQuantity * p.SellingPrice);
            content.AppendLine($"Total Products: {products.Count} | Total Stock Value: KSh {totalValue:N2}");
            content.AppendLine();
            content.AppendLine("Product,SKU,Category,Stock,Price (KSh),Value (KSh)");
            
            foreach (var product in products)
            {
                var value = product.StockQuantity * product.SellingPrice;
                content.AppendLine($"{product.Name},{product.SKU},{product.Category?.Name ?? "Uncategorized"},{product.StockQuantity},{product.SellingPrice:N2},{value:N2}");
            }
            
            return Encoding.UTF8.GetBytes(content.ToString());
        }

        private byte[] GenerateUserReportExcel(List<User> users)
        {
            // Generate CSV format since Excel libraries are not available
            var content = new StringBuilder();
            content.AppendLine("PIXEL SOLUTION COMPANY LTD");
            content.AppendLine("Users Report");
            content.AppendLine($"Total Active Users: {users.Count} | Generated: {DateTime.Now:dd/MM/yyyy HH:mm}");
            content.AppendLine();
            content.AppendLine("Name,Email,Type,Department,Status");
            
            foreach (var user in users)
            {
                content.AppendLine($"{user.FirstName} {user.LastName},{user.Email ?? "N/A"},{user.UserType ?? "N/A"},{user.Department ?? "N/A"},{user.Status ?? "N/A"}");
            }
            
            return Encoding.UTF8.GetBytes(content.ToString());
        }

        private byte[] GenerateCategoriesReportExcel(List<Category> categories)
        {
            // Generate CSV format since Excel libraries are not available
            var content = new StringBuilder();
            content.AppendLine("PIXEL SOLUTION COMPANY LTD");
            content.AppendLine("Categories Report");
            var totalProducts = categories.Sum(c => c.Products.Count);
            content.AppendLine($"Total Categories: {categories.Count} | Total Products: {totalProducts} | Generated: {DateTime.Now:dd/MM/yyyy HH:mm}");
            content.AppendLine();
            content.AppendLine("Category,Description,Products,Status");
            
            foreach (var category in categories)
            {
                content.AppendLine($"{category.Name ?? "N/A"},{category.Description ?? "N/A"},{category.Products.Count},{category.Status ?? "N/A"}");
            }
            
            return Encoding.UTF8.GetBytes(content.ToString());
        }

        public async Task<object> GetSupplierReportDataAsync()
        {
            try
            {
                var suppliers = await _context.Suppliers
                    .Include(s => s.Products)
                    .Include(s => s.PurchaseRequests)
                    .Select(s => new
                    {
                        s.SupplierId,
                        s.CompanyName,
                        s.ContactPerson,
                        s.Email,
                        s.Phone,
                        s.Status,
                        ProductCount = s.Products.Count,
                        ActiveProductCount = s.Products.Count(p => p.Status == "Active"),
                        PurchaseRequestCount = s.PurchaseRequests.Count,
                        TotalPurchaseValue = s.PurchaseRequests
                            .Where(pr => pr.Status == "Approved")
                            .Sum(pr => pr.TotalAmount),
                        s.CreatedAt
                    })
                    .OrderBy(s => s.CompanyName)
                    .ToListAsync();

                var summary = new
                {
                    TotalSuppliers = suppliers.Count,
                    ActiveSuppliers = suppliers.Count(s => s.Status == "Active"),
                    TotalProducts = suppliers.Sum(s => s.ProductCount),
                    TotalPurchaseValue = suppliers.Sum(s => s.TotalPurchaseValue)
                };

                return new
                {
                    Suppliers = suppliers,
                    Summary = summary
                };
            }
            catch (Exception ex)
            {
                throw new Exception($"Error getting supplier report: {ex.Message}", ex);
            }
        }

        public async Task<object> GetUserActivityReportDataAsync()
        {
            try
            {
                // Get users with activity logs - prioritize employees
                var users = await _context.Users
                    .Include(u => u.UserDepartments)
                        .ThenInclude(ud => ud.Department)
                    .Include(u => u.Sales)
                    .Include(u => u.PurchaseRequests)
                    .OrderBy(u => u.UserType == "Employee" ? 0 : 1) // Employees first
                    .ThenBy(u => u.FirstName)
                    .ToListAsync();

                // Get user activity data from UserActivityLogs table - focus on employee activities
                var userActivities = await _context.UserActivityLogs
                    .Include(ual => ual.User)
                    .ToListAsync();

                // Get detailed activity logs for the report - prioritize employee activities
                var detailedActivities = await _context.UserActivityLogs
                    .Include(ual => ual.User)
                    .Where(ual => ual.User != null)
                    .OrderBy(ual => ual.User.UserType == "Employee" ? 0 : 1) // Employee activities first
                    .ThenByDescending(ual => ual.CreatedAt)
                    .Take(100) // Increased to 100 activities to capture more employee data
                    .ToListAsync();

                // Group activities by user in memory to avoid LINQ translation issues
                var activityGroups = userActivities
                    .GroupBy(ual => ual.UserId)
                    .ToDictionary(g => g.Key, g => new
                    {
                        TotalActivities = g.Count(),
                        LastActivity = g.Max(ual => ual.CreatedAt),
                        LoginCount = g.Count(ual => ual.ActivityType == "Login"),
                        SaleActivities = g.Count(ual => ual.ActivityType.Contains("Sale")),
                        DashboardAccess = g.Count(ual => ual.ActivityType == "Dashboard Access"),
                        MessagesAccess = g.Count(ual => ual.ActivityType == "Messages Access"),
                        SettingsAccess = g.Count(ual => ual.ActivityType == "Settings Access"),
                        ReportExports = g.Count(ual => ual.ActivityType == "ReportExport"),
                        EmployeeActivities = g.Count(ual => ual.ActivityType.Contains("Sale") || 
                                                           ual.ActivityType.Contains("Dashboard") || 
                                                           ual.ActivityType.Contains("Messages") ||
                                                           ual.ActivityType.Contains("Settings"))
                    });

                var userReport = users.Select(u => new
                {
                    u.UserId,
                    u.FirstName,
                    u.LastName,
                    FullName = u.FirstName + " " + u.LastName,
                    u.Email,
                    u.UserType,
                    u.Status,
                    Department = u.UserDepartments.FirstOrDefault()?.Department?.Name ?? "No Department",
                    SalesCount = u.Sales.Count(s => s.Status == "Completed"),
                    TotalSalesAmount = u.Sales
                        .Where(s => s.Status == "Completed")
                        .Sum(s => s.TotalAmount),
                    PurchaseRequestCount = u.PurchaseRequests.Count,
                    LastSaleDate = u.Sales
                        .Where(s => s.Status == "Completed")
                        .OrderByDescending(s => s.SaleDate)
                        .Select(s => s.SaleDate)
                        .FirstOrDefault(),
                    // Enhanced activity log data - using dictionary lookup
                    TotalActivities = activityGroups.ContainsKey(u.UserId) ? activityGroups[u.UserId].TotalActivities : 0,
                    LastActivity = activityGroups.ContainsKey(u.UserId) ? activityGroups[u.UserId].LastActivity : (DateTime?)null,
                    LoginCount = activityGroups.ContainsKey(u.UserId) ? activityGroups[u.UserId].LoginCount : 0,
                    SaleActivities = activityGroups.ContainsKey(u.UserId) ? activityGroups[u.UserId].SaleActivities : 0,
                    DashboardAccess = activityGroups.ContainsKey(u.UserId) ? activityGroups[u.UserId].DashboardAccess : 0,
                    MessagesAccess = activityGroups.ContainsKey(u.UserId) ? activityGroups[u.UserId].MessagesAccess : 0,
                    SettingsAccess = activityGroups.ContainsKey(u.UserId) ? activityGroups[u.UserId].SettingsAccess : 0,
                    EmployeeActivities = activityGroups.ContainsKey(u.UserId) ? activityGroups[u.UserId].EmployeeActivities : 0,
                    ReportExports = activityGroups.ContainsKey(u.UserId) ? activityGroups[u.UserId].ReportExports : 0,
                    u.CreatedAt,
                    // Employee performance metrics
                    IsActiveEmployee = u.UserType == "Employee" && u.Status == "Active",
                    EmployeeEngagementScore = u.UserType == "Employee" ? 
                        (activityGroups.ContainsKey(u.UserId) ? activityGroups[u.UserId].EmployeeActivities : 0) : 0
                })
                .OrderBy(u => u.UserType == "Employee" ? 0 : 1) // Employees first
                .ThenByDescending(u => u.EmployeeActivities) // Most active employees first
                .ThenBy(u => u.FirstName)
                .ToList();

                var summary = new
                {
                    TotalUsers = userReport.Count,
                    ActiveUsers = userReport.Count(u => u.Status == "Active"),
                    TotalEmployees = userReport.Count(u => u.UserType == "Employee"),
                    ActiveEmployees = userReport.Count(u => u.UserType == "Employee" && u.Status == "Active"),
                    TotalSales = userReport.Sum(u => u.TotalSalesAmount),
                    TotalTransactions = userReport.Sum(u => u.SalesCount),
                    TotalActivities = userReport.Sum(u => u.TotalActivities),
                    EmployeeActivities = userReport.Sum(u => u.EmployeeActivities),
                    UsersWithActivity = userReport.Count(u => u.TotalActivities > 0),
                    EmployeesWithActivity = userReport.Count(u => u.UserType == "Employee" && u.TotalActivities > 0),
                    AverageEmployeeEngagement = userReport.Where(u => u.UserType == "Employee").Any() ? 
                        userReport.Where(u => u.UserType == "Employee").Average(u => u.EmployeeEngagementScore) : 0,
                    TopPerformingEmployee = userReport.Where(u => u.UserType == "Employee").OrderByDescending(u => u.EmployeeActivities).FirstOrDefault()?.FullName ?? "None"
                };

                return new
                {
                    Users = userReport,
                    Summary = summary,
                    DetailedActivities = detailedActivities.Select(da => new
                    {
                        da.ActivityId,
                        da.UserId,
                        UserName = da.User != null ? $"{da.User.FirstName} {da.User.LastName}" : "Unknown User",
                        UserType = da.User?.UserType ?? "Unknown",
                        da.ActivityType,
                        da.Description,
                        da.CreatedAt,
                        da.IpAddress,
                        IsEmployeeActivity = da.User?.UserType == "Employee",
                        ActivityCategory = da.ActivityType.Contains("Sale") ? "Sales" :
                                         da.ActivityType.Contains("Dashboard") ? "Navigation" :
                                         da.ActivityType.Contains("Messages") ? "Communication" :
                                         da.ActivityType.Contains("Settings") ? "Profile Management" :
                                         da.ActivityType.Contains("Login") ? "Authentication" : "Other"
                    }).ToList()
                };
            }
            catch (Exception ex)
            {
                throw new Exception($"Error getting user activity report: {ex.Message}", ex);
            }
        }

        public async Task<byte[]> GenerateSuppliersReportAsync()
        {
            try
            {
                var reportData = await GetSupplierReportDataAsync();
                var data = (dynamic)reportData;

                using (var stream = new MemoryStream())
                {
                    var document = new Document(PageSize.A4.Rotate());
                    var writer = PdfWriter.GetInstance(document, stream);
                    document.Open();

                    // Company header
                    var titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 18, BaseColor.DARK_GRAY);
                    var companyTitle = new Paragraph("PIXEL SOLUTION COMPANY LTD", titleFont);
                    companyTitle.Alignment = Element.ALIGN_CENTER;
                    companyTitle.SpacingAfter = 10f;
                    document.Add(companyTitle);

                    // Report Title
                    var reportTitleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 14, BaseColor.BLACK);
                    var reportTitle = new Paragraph("Suppliers Report", reportTitleFont);
                    reportTitle.Alignment = Element.ALIGN_CENTER;
                    reportTitle.SpacingAfter = 20f;
                    document.Add(reportTitle);

                    // Summary Statistics
                    var summaryFont = FontFactory.GetFont(FontFactory.HELVETICA, 10, BaseColor.BLACK);
                    var summary = new Paragraph($"Total Suppliers: {data.Summary.TotalSuppliers} | Active Suppliers: {data.Summary.ActiveSuppliers} | Total Purchase Value: KSh {data.Summary.TotalPurchaseValue:N2} | Generated: {DateTime.Now:dd/MM/yyyy HH:mm}", summaryFont);
                    summary.Alignment = Element.ALIGN_CENTER;
                    summary.SpacingAfter = 20f;
                    document.Add(summary);

                    // Suppliers table
                    var table = new PdfPTable(7);
                    table.WidthPercentage = 100;
                    table.SetWidths(new float[] { 2f, 2f, 2f, 1.5f, 1f, 1f, 1.5f });

                    // Table headers
                    var headerFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10, BaseColor.WHITE);
                    string[] headers = { "Company", "Contact Person", "Email", "Phone", "Products", "Status", "Purchase Value" };
                    
                    foreach (string header in headers)
                    {
                        var cell = new PdfPCell(new Phrase(header, headerFont));
                        cell.BackgroundColor = BaseColor.DARK_GRAY;
                        cell.Padding = 5f;
                        table.AddCell(cell);
                    }

                    // Table data
                    var dataFont = FontFactory.GetFont(FontFactory.HELVETICA, 9, BaseColor.BLACK);
                    foreach (var supplier in data.Suppliers)
                    {
                        table.AddCell(new PdfPCell(new Phrase(supplier.CompanyName.ToString(), dataFont)) { Padding = 5f });
                        table.AddCell(new PdfPCell(new Phrase(supplier.ContactPerson.ToString(), dataFont)) { Padding = 5f });
                        table.AddCell(new PdfPCell(new Phrase(supplier.Email.ToString(), dataFont)) { Padding = 5f });
                        table.AddCell(new PdfPCell(new Phrase(supplier.Phone.ToString(), dataFont)) { Padding = 5f });
                        table.AddCell(new PdfPCell(new Phrase(supplier.ProductCount.ToString(), dataFont)) { Padding = 5f });
                        table.AddCell(new PdfPCell(new Phrase(supplier.Status.ToString(), dataFont)) { Padding = 5f });
                        table.AddCell(new PdfPCell(new Phrase($"KSh {supplier.TotalPurchaseValue:N2}", dataFont)) { Padding = 5f });
                    }

                    document.Add(table);

                    // Footer
                    var footerFont = FontFactory.GetFont(FontFactory.HELVETICA_OBLIQUE, 8, BaseColor.GRAY);
                    var footer = new Paragraph($"\nReport generated on {DateTime.Now:dd/MM/yyyy} at {DateTime.Now:HH:mm:ss}", footerFont);
                    footer.Alignment = Element.ALIGN_CENTER;
                    document.Add(footer);

                    document.Close();
                    return stream.ToArray();
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Error generating suppliers report PDF: {ex.Message}", ex);
            }
        }

        public async Task<byte[]> GenerateSuppliersReportExcelAsync()
        {
            try
            {
                var reportData = await GetSupplierReportDataAsync();
                var data = (dynamic)reportData;

                // Generate CSV format since Excel libraries are not available
                var content = new StringBuilder();
                content.AppendLine("PIXEL SOLUTION COMPANY LTD");
                content.AppendLine("Suppliers Report");
                content.AppendLine($"Total Suppliers: {data.Summary.TotalSuppliers} | Active Suppliers: {data.Summary.ActiveSuppliers} | Total Purchase Value: KSh {data.Summary.TotalPurchaseValue:N2}");
                content.AppendLine();
                content.AppendLine("Company,Contact Person,Email,Phone,Products,Status,Purchase Value");
                
                foreach (var supplier in data.Suppliers)
                {
                    content.AppendLine($"{supplier.CompanyName ?? "N/A"},{supplier.ContactPerson ?? "N/A"},{supplier.Email ?? "N/A"},{supplier.Phone ?? "N/A"},{supplier.ProductCount},{supplier.Status ?? "N/A"},{supplier.TotalPurchaseValue:N2}");
                }
                
                return Encoding.UTF8.GetBytes(content.ToString());
            }
            catch (Exception ex)
            {
                throw new Exception($"Error generating suppliers report Excel: {ex.Message}", ex);
            }
        }

        private async Task<byte[]> GenerateComprehensiveReportAsync(DateTime startDate, DateTime endDate)
        {
            try
            {
                // Get all report data directly without try-catch to see actual errors
                var salesData = await GetSalesReportDataAsync(startDate, endDate);
                var inventoryData = await GetInventoryReportDataAsync();
                var usersData = await GetUserActivityReportDataAsync();
                var suppliersData = await GetSupplierReportDataAsync();

                // Cast to dynamic for easier property access
                dynamic sales = salesData;
                dynamic inventory = inventoryData;
                dynamic users = usersData;
                dynamic suppliers = suppliersData;

                using (var stream = new MemoryStream())
                {
                    var document = new Document(PageSize.A4);
                    var writer = PdfWriter.GetInstance(document, stream);
                    document.Open();

                    // Company header
                    var titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 20, BaseColor.DARK_GRAY);
                    var companyTitle = new Paragraph("PIXEL SOLUTION COMPANY LTD", titleFont);
                    companyTitle.Alignment = Element.ALIGN_CENTER;
                    companyTitle.SpacingAfter = 15f;
                    document.Add(companyTitle);

                    // Report Title
                    var reportTitleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 16, BaseColor.BLACK);
                    var reportTitle = new Paragraph("COMPREHENSIVE BUSINESS REPORT", reportTitleFont);
                    reportTitle.Alignment = Element.ALIGN_CENTER;
                    reportTitle.SpacingAfter = 10f;
                    document.Add(reportTitle);

                    // Period
                    var periodFont = FontFactory.GetFont(FontFactory.HELVETICA, 12, BaseColor.BLACK);
                    var period = new Paragraph($"Report Period: {startDate:dd/MM/yyyy} - {endDate:dd/MM/yyyy}", periodFont);
                    period.Alignment = Element.ALIGN_CENTER;
                    period.SpacingAfter = 20f;
                    document.Add(period);

                    // Executive Summary
                    var sectionFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 14, BaseColor.BLACK);
                    var summaryTitle = new Paragraph("EXECUTIVE SUMMARY", sectionFont);
                    summaryTitle.SpacingAfter = 10f;
                    document.Add(summaryTitle);

                    var summaryFont = FontFactory.GetFont(FontFactory.HELVETICA, 10, BaseColor.BLACK);
                    var summaryText = new Paragraph($"• Total Sales Revenue: KSh {(sales.Summary?.TotalSales ?? 0):N2}\n" +
                                                   $"• Total Transactions: {sales.Summary?.TotalTransactions ?? 0}\n" +
                                                   $"• Total Products in Inventory: {inventory.Summary?.TotalProducts ?? 0}\n" +
                                                   $"• Total Stock Value: KSh {(inventory.Summary?.TotalStockValue ?? 0):N2}\n" +
                                                   $"• Active Users: {users.Summary?.ActiveUsers ?? 0}\n" +
                                                   $"• Active Suppliers: {suppliers.Summary?.ActiveSuppliers ?? 0}\n" +
                                                   $"• Low Stock Products: {inventory.Summary?.LowStockProducts ?? 0}", summaryFont);
                    summaryText.SpacingAfter = 20f;
                    document.Add(summaryText);

                    // Sales Performance
                    document.Add(new Paragraph("SALES PERFORMANCE", sectionFont) { SpacingAfter = 10f });
                    
                    var salesTable = new PdfPTable(2);
                    salesTable.WidthPercentage = 100;
                    salesTable.SetWidths(new float[] { 1f, 1f });

                    var headerFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10, BaseColor.WHITE);
                    var dataFont = FontFactory.GetFont(FontFactory.HELVETICA, 10, BaseColor.BLACK);

                    salesTable.AddCell(new PdfPCell(new Phrase("Metric", headerFont)) { BackgroundColor = BaseColor.DARK_GRAY, Padding = 5f });
                    salesTable.AddCell(new PdfPCell(new Phrase("Value", headerFont)) { BackgroundColor = BaseColor.DARK_GRAY, Padding = 5f });
                    
                    salesTable.AddCell(new PdfPCell(new Phrase("Total Sales", dataFont)) { Padding = 5f });
                    salesTable.AddCell(new PdfPCell(new Phrase($"KSh {(sales.Summary?.TotalSales ?? 0):N2}", dataFont)) { Padding = 5f });
                    
                    salesTable.AddCell(new PdfPCell(new Phrase("Total Transactions", dataFont)) { Padding = 5f });
                    salesTable.AddCell(new PdfPCell(new Phrase((sales.Summary?.TotalTransactions ?? 0).ToString(), dataFont)) { Padding = 5f });
                    
                    salesTable.AddCell(new PdfPCell(new Phrase("Average Transaction", dataFont)) { Padding = 5f });
                    salesTable.AddCell(new PdfPCell(new Phrase($"KSh {(sales.Summary?.AverageTransaction ?? 0):N2}", dataFont)) { Padding = 5f });

                    document.Add(salesTable);
                    document.Add(new Paragraph(" "));

                    // Inventory Status
                    document.Add(new Paragraph("INVENTORY STATUS", sectionFont) { SpacingAfter = 10f });
                    
                    var inventoryTable = new PdfPTable(2);
                    inventoryTable.WidthPercentage = 100;
                    inventoryTable.SetWidths(new float[] { 1f, 1f });

                    inventoryTable.AddCell(new PdfPCell(new Phrase("Metric", headerFont)) { BackgroundColor = BaseColor.DARK_GRAY, Padding = 5f });
                    inventoryTable.AddCell(new PdfPCell(new Phrase("Value", headerFont)) { BackgroundColor = BaseColor.DARK_GRAY, Padding = 5f });
                    
                    inventoryTable.AddCell(new PdfPCell(new Phrase("Total Products", dataFont)) { Padding = 5f });
                    inventoryTable.AddCell(new PdfPCell(new Phrase((inventory.Summary?.TotalProducts ?? 0).ToString(), dataFont)) { Padding = 5f });
                    
                    inventoryTable.AddCell(new PdfPCell(new Phrase("Low Stock Products", dataFont)) { Padding = 5f });
                    inventoryTable.AddCell(new PdfPCell(new Phrase((inventory.Summary?.LowStockProducts ?? 0).ToString(), dataFont)) { Padding = 5f });
                    
                    inventoryTable.AddCell(new PdfPCell(new Phrase("Total Stock Value", dataFont)) { Padding = 5f });
                    inventoryTable.AddCell(new PdfPCell(new Phrase($"KSh {(inventory.Summary?.TotalStockValue ?? 0):N2}", dataFont)) { Padding = 5f });

                    document.Add(inventoryTable);
                    document.Add(new Paragraph(" "));

                    // User Activity
                    document.Add(new Paragraph("USER ACTIVITY", sectionFont) { SpacingAfter = 10f });
                    
                    var userTable = new PdfPTable(2);
                    userTable.WidthPercentage = 100;
                    userTable.SetWidths(new float[] { 1f, 1f });

                    userTable.AddCell(new PdfPCell(new Phrase("Metric", headerFont)) { BackgroundColor = BaseColor.DARK_GRAY, Padding = 5f });
                    userTable.AddCell(new PdfPCell(new Phrase("Value", headerFont)) { BackgroundColor = BaseColor.DARK_GRAY, Padding = 5f });
                    
                    userTable.AddCell(new PdfPCell(new Phrase("Total Users", dataFont)) { Padding = 5f });
                    userTable.AddCell(new PdfPCell(new Phrase((users.Summary?.TotalUsers ?? 0).ToString(), dataFont)) { Padding = 5f });
                    
                    userTable.AddCell(new PdfPCell(new Phrase("Active Users", dataFont)) { Padding = 5f });
                    userTable.AddCell(new PdfPCell(new Phrase((users.Summary?.ActiveUsers ?? 0).ToString(), dataFont)) { Padding = 5f });
                    
                    userTable.AddCell(new PdfPCell(new Phrase("Total User Sales", dataFont)) { Padding = 5f });
                    userTable.AddCell(new PdfPCell(new Phrase($"KSh {(users.Summary?.TotalSales ?? 0):N2}", dataFont)) { Padding = 5f });

                    document.Add(userTable);

                    // Footer
                    var footerFont = FontFactory.GetFont(FontFactory.HELVETICA_OBLIQUE, 8, BaseColor.GRAY);
                    var footer = new Paragraph($"\n\nComprehensive report generated on {DateTime.Now:dd/MM/yyyy} at {DateTime.Now:HH:mm:ss}\nPixel Solution Company Ltd - Business Intelligence Report", footerFont);
                    footer.Alignment = Element.ALIGN_CENTER;
                    document.Add(footer);

                    document.Close();
                    return stream.ToArray();
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Error generating comprehensive report: {ex.Message}", ex);
            }
        }

        public async Task<SalesPageViewModel> GetSalesPageDataAsync()
        {
            try
            {
                var today = DateTime.Today;
                
                // Get today's sales statistics
                var todaysSales = await _context.Sales
                    .Where(s => s.SaleDate.Date == today && s.Status == "Completed")
                    .SumAsync(s => s.TotalAmount);

                var todaysTransactions = await _context.Sales
                    .Where(s => s.SaleDate.Date == today && s.Status == "Completed")
                    .CountAsync();

                var averageTransaction = todaysTransactions > 0 ? todaysSales / todaysTransactions : 0;

                // Get recent sales
                var recentSales = await _context.Sales
                    .Include(s => s.User)
                    .Include(s => s.SaleItems)
                        .ThenInclude(si => si.Product)
                    .Where(s => s.Status == "Completed")
                    .OrderByDescending(s => s.SaleDate)
                    .Take(10)
                    .Select(s => new SaleListViewModel
                    {
                        SaleId = s.SaleId,
                        SaleNumber = s.SaleNumber,
                        SaleDate = s.SaleDate,
                        TotalAmount = s.TotalAmount,
                        PaymentMethod = s.PaymentMethod,
                        CustomerName = s.CustomerName ?? "Walk-in Customer",
                        CustomerPhone = s.CustomerPhone,
                        ItemCount = s.SaleItems.Count,
                        CashierName = s.User.FirstName + " " + s.User.LastName
                    })
                    .ToListAsync();

                // Get products for sale
                var products = await _context.Products
                    .Include(p => p.Category)
                    .Where(p => p.IsActive && p.Status == "Active")
                    .Select(p => new ProductSearchViewModel
                    {
                        ProductId = p.ProductId,
                        Name = p.Name,
                        SKU = p.SKU,
                        CategoryName = p.Category.Name,
                        SellingPrice = p.SellingPrice,
                        StockQuantity = p.StockQuantity,
                        ImageUrl = p.ImageUrl
                    })
                    .ToListAsync();

                return new SalesPageViewModel
                {
                    Products = products,
                    RecentSales = recentSales,
                    TodaysSales = todaysSales,
                    TodaysTransactions = todaysTransactions,
                    AverageTransaction = averageTransaction
                };
            }
            catch (Exception ex)
            {
                throw new Exception($"Error getting sales page data: {ex.Message}", ex);
            }
        }

        public async Task<List<Category>> GetCategoriesAsync()
        {
            return await _context.Categories
                .Where(c => c.IsActive && c.Status == "Active")
                .OrderBy(c => c.Name)
                .ToListAsync();
        }

        public async Task<byte[]> GeneratePdfReportAsync(string reportType, DateTime? startDate = null, DateTime? endDate = null)
        {
            try
            {
                using var memoryStream = new MemoryStream();
                var document = new Document(PageSize.A4, 25, 25, 30, 30);
                var writer = PdfWriter.GetInstance(document, memoryStream);
                
                document.Open();
                
                // Add company header
                var titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 18);
                var headerFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 12);
                var normalFont = FontFactory.GetFont(FontFactory.HELVETICA, 10);
                
                var title = new Paragraph("PIXEL SOLUTION COMPANY LTD", titleFont);
                title.Alignment = Element.ALIGN_CENTER;
                document.Add(title);
                
                var reportTitle = new Paragraph($"{reportType.ToUpper()} REPORT", headerFont);
                reportTitle.Alignment = Element.ALIGN_CENTER;
                reportTitle.SpacingAfter = 20;
                document.Add(reportTitle);
                
                // Add date range if provided
                if (startDate.HasValue && endDate.HasValue)
                {
                    var dateRange = new Paragraph($"Period: {startDate.Value:yyyy-MM-dd} to {endDate.Value:yyyy-MM-dd}", normalFont);
                    dateRange.Alignment = Element.ALIGN_CENTER;
                    dateRange.SpacingAfter = 20;
                    document.Add(dateRange);
                }
                
                // Generate report content based on type
                switch (reportType.ToLower())
                {
                    case "sales":
                        await AddSalesReportContent(document, startDate, endDate);
                        break;
                    case "inventory":
                        await AddInventoryReportContent(document);
                        break;
                    case "users":
                        await AddUsersReportContent(document);
                        break;
                    case "categories":
                        await AddCategoriesReportContent(document);
                        break;
                    default:
                        var errorMsg = new Paragraph("Invalid report type specified.", normalFont);
                        document.Add(errorMsg);
                        break;
                }
                
                // Add footer
                var footer = new Paragraph($"Generated on: {DateTime.Now:yyyy-MM-dd HH:mm:ss}", normalFont);
                footer.Alignment = Element.ALIGN_RIGHT;
                footer.SpacingBefore = 20;
                document.Add(footer);
                
                document.Close();
                return memoryStream.ToArray();
            }
            catch (Exception ex)
            {
                throw new Exception($"Error generating PDF report: {ex.Message}", ex);
            }
        }

        public async Task<byte[]> GenerateExcelReportAsync(string reportType, DateTime? startDate = null, DateTime? endDate = null)
        {
            try
            {
                using var workbook = new XLWorkbook();
                var worksheet = workbook.Worksheets.Add($"{reportType} Report");
                
                // Add company header
                worksheet.Cell(1, 1).Value = "PIXEL SOLUTION COMPANY LTD";
                worksheet.Cell(1, 1).Style.Font.Bold = true;
                worksheet.Cell(1, 1).Style.Font.FontSize = 16;
                worksheet.Range("A1:E1").Merge();
                worksheet.Cell(1, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                
                worksheet.Cell(2, 1).Value = $"{reportType.ToUpper()} REPORT";
                worksheet.Cell(2, 1).Style.Font.Bold = true;
                worksheet.Cell(2, 1).Style.Font.FontSize = 14;
                worksheet.Range("A2:E2").Merge();
                worksheet.Cell(2, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                
                int currentRow = 4;
                
                // Add date range if provided
                if (startDate.HasValue && endDate.HasValue)
                {
                    worksheet.Cell(3, 1).Value = $"Period: {startDate.Value:yyyy-MM-dd} to {endDate.Value:yyyy-MM-dd}";
                    worksheet.Range("A3:E3").Merge();
                    worksheet.Cell(3, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    currentRow = 5;
                }
                
                // Generate report content based on type
                switch (reportType.ToLower())
                {
                    case "sales":
                        var sales = await _context.Sales.Include(s => s.User).ToListAsync();
                        worksheet.Cell(currentRow, 1).Value = "Sale Number";
                        worksheet.Cell(currentRow, 2).Value = "Date";
                        worksheet.Cell(currentRow, 3).Value = "Customer";
                        worksheet.Cell(currentRow, 4).Value = "Amount";
                        worksheet.Cell(currentRow, 5).Value = "Cashier";
                        
                        currentRow++;
                        foreach (var sale in sales)
                        {
                            worksheet.Cell(currentRow, 1).Value = sale.SaleNumber;
                            worksheet.Cell(currentRow, 2).Value = sale.SaleDate.ToString("yyyy-MM-dd");
                            worksheet.Cell(currentRow, 3).Value = sale.CustomerName ?? "Walk-in";
                            worksheet.Cell(currentRow, 4).Value = sale.TotalAmount;
                            worksheet.Cell(currentRow, 5).Value = sale.CashierName;
                            currentRow++;
                        }
                        break;
                    case "inventory":
                        var products = await _context.Products.Include(p => p.Category).ToListAsync();
                        worksheet.Cell(currentRow, 1).Value = "Product";
                        worksheet.Cell(currentRow, 2).Value = "SKU";
                        worksheet.Cell(currentRow, 3).Value = "Category";
                        worksheet.Cell(currentRow, 4).Value = "Stock";
                        worksheet.Cell(currentRow, 5).Value = "Price";
                        
                        currentRow++;
                        foreach (var product in products)
                        {
                            worksheet.Cell(currentRow, 1).Value = product.Name;
                            worksheet.Cell(currentRow, 2).Value = product.SKU;
                            worksheet.Cell(currentRow, 3).Value = product.Category?.Name ?? "N/A";
                            worksheet.Cell(currentRow, 4).Value = product.StockQuantity;
                            worksheet.Cell(currentRow, 5).Value = product.SellingPrice;
                            currentRow++;
                        }
                        break;
                }
                
                // Auto-fit columns
                worksheet.Columns().AdjustToContents();
                
                using var stream = new MemoryStream();
                workbook.SaveAs(stream);
                return stream.ToArray();
            }
            catch (Exception ex)
            {
                throw new Exception($"Error generating Excel report: {ex.Message}", ex);
            }
        }

        private async Task AddSalesReportContent(Document document, DateTime? startDate, DateTime? endDate)
        {
            var query = _context.Sales.Include(s => s.User).Include(s => s.SaleItems).ThenInclude(si => si.Product).AsQueryable();
            
            if (startDate.HasValue && endDate.HasValue)
            {
                query = query.Where(s => s.SaleDate >= startDate.Value && s.SaleDate <= endDate.Value);
            }
            
            var sales = await query.OrderByDescending(s => s.SaleDate).ToListAsync();
            
            var table = new PdfPTable(6);
            table.WidthPercentage = 100;
            table.SetWidths(new float[] { 15, 20, 15, 15, 20, 15 });
            
            // Headers
            var headerFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10);
            table.AddCell(new PdfPCell(new Phrase("Sale #", headerFont)));
            table.AddCell(new PdfPCell(new Phrase("Date", headerFont)));
            table.AddCell(new PdfPCell(new Phrase("Customer", headerFont)));
            table.AddCell(new PdfPCell(new Phrase("Amount", headerFont)));
            table.AddCell(new PdfPCell(new Phrase("Payment", headerFont)));
            table.AddCell(new PdfPCell(new Phrase("Cashier", headerFont)));
            
            var normalFont = FontFactory.GetFont(FontFactory.HELVETICA, 9);
            foreach (var sale in sales)
            {
                table.AddCell(new PdfPCell(new Phrase(sale.SaleNumber, normalFont)));
                table.AddCell(new PdfPCell(new Phrase(sale.SaleDate.ToString("yyyy-MM-dd"), normalFont)));
                table.AddCell(new PdfPCell(new Phrase(sale.CustomerName ?? "Walk-in", normalFont)));
                table.AddCell(new PdfPCell(new Phrase($"KSh {sale.TotalAmount:N2}", normalFont)));
                table.AddCell(new PdfPCell(new Phrase(sale.PaymentMethod, normalFont)));
                table.AddCell(new PdfPCell(new Phrase(sale.CashierName, normalFont)));
            }
            
            document.Add(table);
            
            // Summary
            var totalAmount = sales.Sum(s => s.TotalAmount);
            var summary = new Paragraph($"\nTotal Sales: {sales.Count}\nTotal Amount: KSh {totalAmount:N2}", 
                FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 12));
            document.Add(summary);
        }

        private async Task AddInventoryReportContent(Document document)
        {
            var products = await _context.Products.Include(p => p.Category).Where(p => p.IsActive).ToListAsync();
            
            var table = new PdfPTable(6);
            table.WidthPercentage = 100;
            table.SetWidths(new float[] { 20, 15, 20, 15, 15, 15 });
            
            // Headers
            var headerFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10);
            table.AddCell(new PdfPCell(new Phrase("Product Name", headerFont)));
            table.AddCell(new PdfPCell(new Phrase("SKU", headerFont)));
            table.AddCell(new PdfPCell(new Phrase("Category", headerFont)));
            table.AddCell(new PdfPCell(new Phrase("Stock", headerFont)));
            table.AddCell(new PdfPCell(new Phrase("Buy Price", headerFont)));
            table.AddCell(new PdfPCell(new Phrase("Sell Price", headerFont)));
            
            var normalFont = FontFactory.GetFont(FontFactory.HELVETICA, 9);
            foreach (var product in products)
            {
                table.AddCell(new PdfPCell(new Phrase(product.Name, normalFont)));
                table.AddCell(new PdfPCell(new Phrase(product.SKU, normalFont)));
                table.AddCell(new PdfPCell(new Phrase(product.Category?.Name ?? "N/A", normalFont)));
                table.AddCell(new PdfPCell(new Phrase(product.StockQuantity.ToString(), normalFont)));
                table.AddCell(new PdfPCell(new Phrase($"KSh {product.BuyingPrice:N2}", normalFont)));
                table.AddCell(new PdfPCell(new Phrase($"KSh {product.SellingPrice:N2}", normalFont)));
            }
            
            document.Add(table);
        }

        private async Task AddUsersReportContent(Document document)
        {
            var users = await _context.Users.Where(u => u.Status == "Active").ToListAsync();
            
            var table = new PdfPTable(5);
            table.WidthPercentage = 100;
            table.SetWidths(new float[] { 25, 25, 20, 15, 15 });
            
            // Headers
            var headerFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10);
            table.AddCell(new PdfPCell(new Phrase("Name", headerFont)));
            table.AddCell(new PdfPCell(new Phrase("Email", headerFont)));
            table.AddCell(new PdfPCell(new Phrase("Role", headerFont)));
            table.AddCell(new PdfPCell(new Phrase("Status", headerFont)));
            table.AddCell(new PdfPCell(new Phrase("Created", headerFont)));
            
            var normalFont = FontFactory.GetFont(FontFactory.HELVETICA, 9);
            foreach (var user in users)
            {
                table.AddCell(new PdfPCell(new Phrase($"{user.FirstName} {user.LastName}", normalFont)));
                table.AddCell(new PdfPCell(new Phrase(user.Email, normalFont)));
                table.AddCell(new PdfPCell(new Phrase(user.UserType, normalFont)));
                table.AddCell(new PdfPCell(new Phrase(user.Status, normalFont)));
                table.AddCell(new PdfPCell(new Phrase(user.CreatedAt.ToString("yyyy-MM-dd"), normalFont)));
            }
            
            document.Add(table);
        }

        private async Task AddCategoriesReportContent(Document document)
        {
            var categories = await _context.Categories.Include(c => c.Products).Where(c => c.IsActive).ToListAsync();
            
            var table = new PdfPTable(4);
            table.WidthPercentage = 100;
            table.SetWidths(new float[] { 30, 40, 15, 15 });
            
            // Headers
            var headerFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10);
            table.AddCell(new PdfPCell(new Phrase("Category Name", headerFont)));
            table.AddCell(new PdfPCell(new Phrase("Description", headerFont)));
            table.AddCell(new PdfPCell(new Phrase("Products", headerFont)));
            table.AddCell(new PdfPCell(new Phrase("Status", headerFont)));
            
            var normalFont = FontFactory.GetFont(FontFactory.HELVETICA, 9);
            foreach (var category in categories)
            {
                table.AddCell(new PdfPCell(new Phrase(category.Name, normalFont)));
                table.AddCell(new PdfPCell(new Phrase(category.Description, normalFont)));
                table.AddCell(new PdfPCell(new Phrase(category.Products.Count.ToString(), normalFont)));
                table.AddCell(new PdfPCell(new Phrase(category.Status, normalFont)));
            }
            
            document.Add(table);
        }

        // Additional methods required by IReportService interface
        public async Task<byte[]> GetSalesReportAsync(DateTime startDate, DateTime endDate)
        {
            return await GeneratePdfReportAsync("sales", startDate, endDate);
        }

        public async Task<byte[]> GetInventoryReportAsync()
        {
            return await GeneratePdfReportAsync("inventory");
        }

        public async Task<byte[]> GetSupplierReportAsync()
        {
            return await GeneratePdfReportAsync("suppliers");
        }


        public async Task<byte[]> GenerateSalesReceiptAsync(int saleId)
        {
            return await GenerateSalesReceiptInternalAsync(saleId);
        }

        public async Task<byte[]> GetUserActivityReportAsync()
        {
            return await GeneratePdfReportAsync("users");
        }


        public async Task<byte[]> GenerateComprehensiveReportAsync(DateTime? startDate = null, DateTime? endDate = null)
        {
            var start = startDate ?? DateTime.Now.AddMonths(-1);
            var end = endDate ?? DateTime.Now;
            return await GenerateComprehensiveReportAsync(start, end);
        }

        public async Task<byte[]> GenerateReceiptPdfAsync(int saleId)
        {
            return await GenerateSalesReceiptInternalAsync(saleId);
        }

        public async Task<byte[]> GenerateReceiptPdfAsync(ReceiptPdfRequest request)
        {
            try
            {
                using var memoryStream = new MemoryStream();
                var document = new Document(PageSize.A4, 25, 25, 30, 30);
                var writer = PdfWriter.GetInstance(document, memoryStream);
                
                document.Open();

                // Company Header
                var titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 18, BaseColor.DARK_GRAY);
                var companyTitle = new Paragraph("PIXEL SOLUTION COMPANY LTD", titleFont);
                companyTitle.Alignment = Element.ALIGN_CENTER;
                companyTitle.SpacingAfter = 10f;
                document.Add(companyTitle);

                // Receipt Title
                var receiptTitleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 14, BaseColor.BLACK);
                var receiptTitle = new Paragraph($"SALES RECEIPT #{request.SaleNumber}", receiptTitleFont);
                receiptTitle.Alignment = Element.ALIGN_CENTER;
                receiptTitle.SpacingAfter = 20f;
                document.Add(receiptTitle);

                // Sale Details
                var detailsFont = FontFactory.GetFont(FontFactory.HELVETICA, 10, BaseColor.BLACK);
                var saleDetails = new Paragraph($"Date: {request.SaleDate:dd/MM/yyyy HH:mm}\nCashier: {request.CashierName}\nCustomer: {request.CustomerName ?? "Walk-in Customer"}\nPhone: {request.CustomerPhone ?? "N/A"}", detailsFont);
                saleDetails.SpacingAfter = 15f;
                document.Add(saleDetails);

                // Items Table
                var table = new PdfPTable(4);
                table.WidthPercentage = 100;
                table.SetWidths(new float[] { 40, 15, 20, 25 });

                // Headers
                var headerFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10);
                table.AddCell(new PdfPCell(new Phrase("Product", headerFont)));
                table.AddCell(new PdfPCell(new Phrase("Qty", headerFont)));
                table.AddCell(new PdfPCell(new Phrase("Price", headerFont)));
                table.AddCell(new PdfPCell(new Phrase("Total", headerFont)));

                var normalFont = FontFactory.GetFont(FontFactory.HELVETICA, 9);
                foreach (var item in request.Items)
                {
                    table.AddCell(new PdfPCell(new Phrase(item.Name, normalFont)));
                    table.AddCell(new PdfPCell(new Phrase(item.Quantity.ToString(), normalFont)));
                    table.AddCell(new PdfPCell(new Phrase($"KSh {item.UnitPrice:N2}", normalFont)));
                    table.AddCell(new PdfPCell(new Phrase($"KSh {item.Total:N2}", normalFont)));
                }

                document.Add(table);

                // Payment Summary
                var summaryFont = FontFactory.GetFont(FontFactory.HELVETICA, 10, BaseColor.BLACK);
                var paymentSummary = new Paragraph($"\nSubtotal: KSh {request.Subtotal:N2}\nTax: KSh {request.Tax:N2}\nTOTAL: KSh {request.TotalAmount:N2}\nAmount Paid: KSh {request.AmountPaid:N2}\nChange: KSh {request.ChangeGiven:N2}\nPayment Method: {request.PaymentMethod}", summaryFont);
                paymentSummary.Alignment = Element.ALIGN_RIGHT;
                paymentSummary.SpacingAfter = 10f;
                document.Add(paymentSummary);

                // Footer
                var footerFont = FontFactory.GetFont(FontFactory.HELVETICA_OBLIQUE, 8, BaseColor.GRAY);
                var footer = new Paragraph($"\nThank you for your business!\nGenerated: {DateTime.Now:dd/MM/yyyy HH:mm:ss}", footerFont);
                footer.Alignment = Element.ALIGN_CENTER;
                document.Add(footer);

                document.Close();
                return memoryStream.ToArray();
            }
            catch (Exception ex)
            {
                throw new Exception($"Error generating receipt PDF from request: {ex.Message}", ex);
            }
        }

        public async Task<byte[]> GenerateReceiptPDFAsync(string receiptHtml)
        {
            try
            {
                using var memoryStream = new MemoryStream();
                var document = new Document(PageSize.A4, 25, 25, 30, 30);
                var writer = PdfWriter.GetInstance(document, memoryStream);
                document.Open();

                // Company Header
                var titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 16, BaseColor.BLACK);
                var title = new Paragraph("PIXEL SOLUTION COMPANY LTD", titleFont);
                title.Alignment = Element.ALIGN_CENTER;
                title.SpacingAfter = 10f;
                document.Add(title);

                var subtitleFont = FontFactory.GetFont(FontFactory.HELVETICA, 12, BaseColor.DARK_GRAY);
                var subtitle = new Paragraph("Sales Receipt", subtitleFont);
                subtitle.Alignment = Element.ALIGN_CENTER;
                subtitle.SpacingAfter = 20f;
                document.Add(subtitle);

                // Parse HTML content and add to PDF
                var contentFont = FontFactory.GetFont(FontFactory.HELVETICA, 10, BaseColor.BLACK);
                
                // Simple HTML parsing - extract text content
                var cleanText = System.Text.RegularExpressions.Regex.Replace(receiptHtml, "<.*?>", "");
                cleanText = System.Net.WebUtility.HtmlDecode(cleanText);
                
                var content = new Paragraph(cleanText, contentFont);
                content.SpacingAfter = 20f;
                document.Add(content);

                // Footer
                var footerFont = FontFactory.GetFont(FontFactory.HELVETICA_OBLIQUE, 8, BaseColor.GRAY);
                var footer = new Paragraph($"\nGenerated: {DateTime.Now:dd/MM/yyyy HH:mm:ss}", footerFont);
                footer.Alignment = Element.ALIGN_CENTER;
                document.Add(footer);

                document.Close();
                return memoryStream.ToArray();
            }
            catch (Exception ex)
            {
                throw new Exception($"Error generating receipt PDF from HTML: {ex.Message}", ex);
            }
        }

        public async Task<byte[]> GenerateSupplierInvoicePDFAsync(SupplierInvoice invoice)
        {
            try
            {
                using var memoryStream = new MemoryStream();
                var document = new Document(PageSize.A4, 40, 40, 40, 40);
                var writer = PdfWriter.GetInstance(document, memoryStream);
                document.Open();

                // Professional Colors
                var primaryBlue = new BaseColor(52, 73, 94);  // Professional dark blue
                var accentBlue = new BaseColor(41, 128, 185); // Bright blue for highlights
                var darkText = new BaseColor(44, 62, 80);     // Professional dark text
                var lightGray = new BaseColor(236, 240, 241);  // Light background
                var borderGray = new BaseColor(189, 195, 199); // Table borders
                var greenAmount = new BaseColor(39, 174, 96);  // Positive amounts
                var redAmount = new BaseColor(231, 76, 60);    // Outstanding amounts

                // Professional Typography
                var companyFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 20, primaryBlue);
                var invoiceTitleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 16, darkText);
                var sectionHeaderFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 12, primaryBlue);
                var tableHeaderFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 9, BaseColor.WHITE);
                var normalFont = FontFactory.GetFont(FontFactory.HELVETICA, 9, darkText);
                var boldFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 9, darkText);
                var smallFont = FontFactory.GetFont(FontFactory.HELVETICA, 8, new BaseColor(127, 140, 141));
                var headerFont = sectionHeaderFont; // Alias for sectionHeaderFont
                var defaultAmountFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10, darkText);

                // Professional Header Layout
                var headerTable = new PdfPTable(2);
                headerTable.WidthPercentage = 100;
                headerTable.SetWidths(new float[] { 2, 1 });
                headerTable.SpacingAfter = 30;

                // Company Information (Left)
                var companyCell = new PdfPCell();
                companyCell.Border = Rectangle.NO_BORDER;
                companyCell.PaddingRight = 20;

                var companyName = new Paragraph("PIXELSOLUTION", companyFont);
                companyCell.AddElement(companyName);

                var companyAddress = new Paragraph("Building Name\n123 Business Street\nNairobi, Kenya\nZip Code", normalFont);
                companyAddress.SpacingBefore = 5;
                companyCell.AddElement(companyAddress);

                var companyContact = new Paragraph("+254-XXX-XXXX\nsupport@pixelsolution.com\nwww.pixelsolution.com", normalFont);
                companyContact.SpacingBefore = 8;
                companyCell.AddElement(companyContact);

                headerTable.AddCell(companyCell);

                // Logo/Invoice Title (Right)
                var logoCell = new PdfPCell();
                logoCell.Border = Rectangle.NO_BORDER;
                logoCell.HorizontalAlignment = Element.ALIGN_RIGHT;

                var invoiceTitle = new Paragraph("INVOICE", invoiceTitleFont);
                invoiceTitle.Alignment = Element.ALIGN_RIGHT;
                logoCell.AddElement(invoiceTitle);

                var invoiceNumber = new Paragraph($"#{invoice.InvoiceNumber}", boldFont);
                invoiceNumber.Alignment = Element.ALIGN_RIGHT;
                invoiceNumber.SpacingBefore = 5;
                logoCell.AddElement(invoiceNumber);

                var invoiceDate = new Paragraph($"Date: {invoice.InvoiceDate:MM/dd/yyyy}\nDue: {invoice.DueDate:MM/dd/yyyy}", normalFont);
                invoiceDate.Alignment = Element.ALIGN_RIGHT;
                invoiceDate.SpacingBefore = 8;
                logoCell.AddElement(invoiceDate);

                headerTable.AddCell(logoCell);
                document.Add(headerTable);

                // Professional Billing Information Layout
                var billingTable = new PdfPTable(2);
                billingTable.WidthPercentage = 100;
                billingTable.SetWidths(new float[] { 1, 1 });
                billingTable.SpacingAfter = 25;

                // Bill To Section (Left)
                var billToCell = new PdfPCell();
                billToCell.Border = Rectangle.NO_BORDER;
                billToCell.PaddingRight = 20;

                var billToHeader = new Paragraph("BILL TO", sectionHeaderFont);
                billToCell.AddElement(billToHeader);

                var supplierInfo = new Paragraph($"{invoice.Supplier.CompanyName}\n{invoice.Supplier.ContactPerson}\n{invoice.Supplier.Address}\n{invoice.Supplier.Phone}\n{invoice.Supplier.Email}", normalFont);
                supplierInfo.SpacingBefore = 8;
                billToCell.AddElement(supplierInfo);

                billingTable.AddCell(billToCell);

                // Invoice Details (Right)
                var invoiceDetailsCell = new PdfPCell();
                invoiceDetailsCell.Border = Rectangle.NO_BORDER;
                var detailsHeader = new Paragraph("INVOICE DETAILS", sectionHeaderFont);
                invoiceDetailsCell.AddElement(detailsHeader);

                // Create a clean details table
                var innerDetailsTable = new PdfPTable(2);
                innerDetailsTable.WidthPercentage = 100;
                innerDetailsTable.SetWidths(new float[] { 1, 1 });
                innerDetailsTable.SpacingBefore = 8;

                // Add detail rows
                var detailItems = new (string label, string value)[] {
                    ("Invoice Number:", invoice.InvoiceNumber),
                    ("Issue Date:", invoice.InvoiceDate.ToString("MM/dd/yyyy")),
                    ("Due Date:", invoice.DueDate.ToString("MM/dd/yyyy")),
                    ("Status:", invoice.Status.ToUpper())
                };

                foreach (var (label, value) in detailItems)
                {
                    var labelCell = new PdfPCell(new Phrase(label, normalFont));
                    labelCell.Border = Rectangle.NO_BORDER;
                    labelCell.PaddingBottom = 4;
                    innerDetailsTable.AddCell(labelCell);

                    var valueCell = new PdfPCell(new Phrase(value, boldFont));
                    valueCell.Border = Rectangle.NO_BORDER;
                    valueCell.PaddingBottom = 4;
                    valueCell.HorizontalAlignment = Element.ALIGN_RIGHT;
                    innerDetailsTable.AddCell(valueCell);
                }

                invoiceDetailsCell.AddElement(innerDetailsTable);
                billingTable.AddCell(invoiceDetailsCell);
                document.Add(billingTable);

                // Define colors
                var primaryColor = primaryBlue; // Use the same as primaryBlue for consistency
                var redColor = redAmount;       // Use the same as redAmount for consistency

                // Professional Items Table
                var itemsHeader = new Paragraph("DESCRIPTION", sectionHeaderFont);
                itemsHeader.SpacingBefore = 20;
                itemsHeader.SpacingAfter = 15;
                document.Add(itemsHeader);

                // Create supplier details section
                var supplierDetailsTitle = new Paragraph("SUPPLIER DETAILS", sectionHeaderFont);
                var supplierDetailsCell = new PdfPCell();
                supplierDetailsCell.Border = Rectangle.NO_BORDER;
                supplierDetailsCell.Padding = 10;
                supplierDetailsCell.BackgroundColor = new BaseColor(248, 249, 250);
                
                supplierDetailsCell.AddElement(supplierDetailsTitle);
                supplierDetailsCell.AddElement(new Paragraph(" ", normalFont) { SpacingAfter = 10 });
                supplierDetailsCell.AddElement(new Paragraph($"Company: {invoice.Supplier.CompanyName}", boldFont));
                supplierDetailsCell.AddElement(new Paragraph($"Contact Person: {invoice.Supplier.ContactPerson}", normalFont));
                supplierDetailsCell.AddElement(new Paragraph($"Email: {invoice.Supplier.Email}", normalFont));
                supplierDetailsCell.AddElement(new Paragraph($"Phone: {invoice.Supplier.Phone}", normalFont));
                supplierDetailsCell.AddElement(new Paragraph($"Address: {invoice.Supplier.Address}", normalFont));

                // Add supplier details to a table for better layout
                var detailsTable = new PdfPTable(1);
                detailsTable.WidthPercentage = 100;
                detailsTable.AddCell(supplierDetailsCell);
                document.Add(detailsTable);
                document.Add(new Paragraph(" ", normalFont) { SpacingAfter = 20 });

                // Invoice Items Section
                var itemsTitle = new Paragraph("📦 Invoice Items", sectionHeaderFont);
                itemsTitle.SpacingBefore = 10;
                itemsTitle.SpacingAfter = 15;
                document.Add(itemsTitle);

                // Items Table
                var itemsTable = new PdfPTable(6);
                itemsTable.WidthPercentage = 100;
                itemsTable.SetWidths(new float[] { 3, 2, 2, 1, 1.5f, 1.5f });

                // Table Headers
                var headerCells = new string[] { "Product Name", "Batch Number", "Supply Date", "Qty", "Unit Cost", "Total Cost" };
                foreach (var header in headerCells)
                {
                    var cell = new PdfPCell(new Phrase(header, FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 9, BaseColor.WHITE)));
                    cell.BackgroundColor = primaryColor;
                    cell.Padding = 8;
                    cell.HorizontalAlignment = Element.ALIGN_CENTER;
                    cell.Border = Rectangle.BOX;
                    cell.BorderColor = borderGray;
                    itemsTable.AddCell(cell);
                }

                // Table Rows
                bool alternateRow = false;
                foreach (var item in invoice.SupplierInvoiceItems)
                {
                    var rowColor = alternateRow ? new BaseColor(248, 249, 250) : BaseColor.WHITE;
                    
                    var cells = new string[]
                    {
                        item.SupplierProductSupply.Product.Name,
                        item.SupplierProductSupply.BatchNumber ?? "N/A",
                        item.SupplierProductSupply.SupplyDate.ToString("MMM dd, yyyy"),
                        item.Quantity.ToString("N0"),
                        $"KSh {item.UnitCost:N2}",
                        $"KSh {item.TotalCost:N2}"
                    };

                    foreach (var cellText in cells)
                    {
                        var cell = new PdfPCell(new Phrase(cellText, normalFont));
                        cell.BackgroundColor = rowColor;
                        cell.Padding = 8;
                        cell.HorizontalAlignment = Element.ALIGN_LEFT;
                        itemsTable.AddCell(cell);
                    }
                    alternateRow = !alternateRow;
                }

                document.Add(itemsTable);
                document.Add(new Paragraph(" ", normalFont) { SpacingAfter = 20 });

                // Totals Section
                var totalsTable = new PdfPTable(2);
                totalsTable.WidthPercentage = 50;
                totalsTable.HorizontalAlignment = Element.ALIGN_RIGHT;
                totalsTable.SetWidths(new float[] { 1, 1 });

                // Totals data
                var totalsData = new (string label, decimal amount, bool isGrandTotal, bool isAmountDue)[]
                {
                    ("Subtotal:", invoice.Subtotal, false, false),
                    ("Tax (16%):", invoice.TaxAmount, false, false),
                    ("Total Amount:", invoice.TotalAmount, true, false),
                    ("Amount Paid:", invoice.AmountPaid, false, false),
                    ("Amount Due:", invoice.AmountDue, false, true)
                };

                foreach (var (label, amount, isGrandTotal, isAmountDue) in totalsData)
                {
                    var labelCell = new PdfPCell(new Phrase(label, isGrandTotal ? boldFont : normalFont));
                    labelCell.Border = Rectangle.NO_BORDER;
                    labelCell.HorizontalAlignment = Element.ALIGN_RIGHT;
                    labelCell.Padding = 5;
                    if (isGrandTotal) labelCell.BackgroundColor = new BaseColor(248, 249, 250);
                    totalsTable.AddCell(labelCell);

                    var currentAmountFont = isAmountDue ? FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10, redColor) :
                                   isGrandTotal ? boldFont : normalFont;
                    var amountCell = new PdfPCell(new Phrase($"KSh {amount:N2}", currentAmountFont));
                    amountCell.Border = Rectangle.NO_BORDER;
                    amountCell.HorizontalAlignment = Element.ALIGN_RIGHT;
                    amountCell.Padding = 5;
                    if (isGrandTotal) amountCell.BackgroundColor = new BaseColor(248, 249, 250);
                    totalsTable.AddCell(amountCell);
                }

                document.Add(totalsTable);
                document.Add(new Paragraph(" ", normalFont) { SpacingAfter = 30 });

                // Payment History (if any)
                if (invoice.SupplierPayments.Any())
                {
                    var paymentTitle = new Paragraph("💳 Payment History", sectionHeaderFont);
                    paymentTitle.SpacingAfter = 15;
                    document.Add(paymentTitle);

                    var paymentTable = new PdfPTable(5);
                    paymentTable.WidthPercentage = 100;
                    paymentTable.SetWidths(new float[] { 2, 2, 1.5f, 2, 2 });

                    // Payment Headers
                    var paymentHeaders = new string[] { "Payment Date", "Amount", "Method", "Reference", "Processed By" };
                    foreach (var header in paymentHeaders)
                    {
                        var cell = new PdfPCell(new Phrase(header, FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10, BaseColor.WHITE)));
                        cell.BackgroundColor = new BaseColor(76, 175, 80); // Green
                        cell.Padding = 8;
                        cell.HorizontalAlignment = Element.ALIGN_CENTER;
                        paymentTable.AddCell(cell);
                    }

                    // Payment Rows
                    alternateRow = false;
                    foreach (var payment in invoice.SupplierPayments)
                    {
                        var rowColor = alternateRow ? new BaseColor(248, 249, 250) : BaseColor.WHITE;
                        
                        var paymentCells = new string[]
                        {
                            payment.PaymentDate.ToString("MMM dd, yyyy"),
                            $"KSh {payment.Amount:N2}",
                            payment.PaymentMethod,
                            payment.PaymentReference,
                            payment.ProcessedBy
                        };

                        foreach (var cellText in paymentCells)
                        {
                            var cell = new PdfPCell(new Phrase(cellText, normalFont));
                            cell.BackgroundColor = rowColor;
                            cell.Padding = 6;
                            cell.HorizontalAlignment = Element.ALIGN_LEFT;
                            paymentTable.AddCell(cell);
                        }
                        alternateRow = !alternateRow;
                    }

                    document.Add(paymentTable);
                    document.Add(new Paragraph(" ", normalFont) { SpacingAfter = 30 });
                }

                // Notes Section (if any)
                if (!string.IsNullOrEmpty(invoice.Notes))
                {
                    var notesTitle = new Paragraph("📝 Additional Notes", headerFont);
                    notesTitle.SpacingAfter = 10;
                    document.Add(notesTitle);

                    var notesCell = new PdfPCell(new Phrase(invoice.Notes, normalFont));
                    notesCell.BackgroundColor = new BaseColor(255, 243, 205);
                    notesCell.Border = Rectangle.LEFT_BORDER;
                    notesCell.BorderColor = new BaseColor(255, 193, 7);
                    notesCell.BorderWidth = 3;
                    notesCell.Padding = 15;

                    var notesTable = new PdfPTable(1);
                    notesTable.WidthPercentage = 100;
                    notesTable.AddCell(notesCell);
                    document.Add(notesTable);
                    document.Add(new Paragraph(" ", normalFont) { SpacingAfter = 30 });
                }

                // Signatures Section
                var signaturesTitle = new Paragraph("✍️ Signatures", headerFont);
                signaturesTitle.SpacingAfter = 20;
                document.Add(signaturesTitle);

                var signaturesTable = new PdfPTable(2);
                signaturesTable.WidthPercentage = 100;
                signaturesTable.SetWidths(new float[] { 1, 1 });

                // Admin Signature
                var adminSigCell = new PdfPCell();
                adminSigCell.Border = Rectangle.BOX;
                adminSigCell.BorderColor = lightGray;
                adminSigCell.Padding = 20;
                adminSigCell.BackgroundColor = new BaseColor(250, 250, 250);

                adminSigCell.AddElement(new Paragraph("Admin Signature", boldFont) { Alignment = Element.ALIGN_CENTER });
                adminSigCell.AddElement(new Paragraph(" ", normalFont) { SpacingAfter = 40 }); // Space for signature
                adminSigCell.AddElement(new Paragraph("_________________________", normalFont) { Alignment = Element.ALIGN_CENTER });
                adminSigCell.AddElement(new Paragraph("Authorized Administrator", smallFont) { Alignment = Element.ALIGN_CENTER });
                adminSigCell.AddElement(new Paragraph("PixelSolution", smallFont) { Alignment = Element.ALIGN_CENTER });

                signaturesTable.AddCell(adminSigCell);

                // Supplier Signature
                var supplierSigCell = new PdfPCell();
                supplierSigCell.Border = Rectangle.BOX;
                supplierSigCell.BorderColor = lightGray;
                supplierSigCell.Padding = 20;
                supplierSigCell.BackgroundColor = new BaseColor(250, 250, 250);

                supplierSigCell.AddElement(new Paragraph("Supplier Signature", boldFont) { Alignment = Element.ALIGN_CENTER });
                supplierSigCell.AddElement(new Paragraph(" ", normalFont) { SpacingAfter = 40 }); // Space for signature
                supplierSigCell.AddElement(new Paragraph("_________________________", normalFont) { Alignment = Element.ALIGN_CENTER });
                supplierSigCell.AddElement(new Paragraph(invoice.Supplier.ContactPerson, smallFont) { Alignment = Element.ALIGN_CENTER });
                supplierSigCell.AddElement(new Paragraph(invoice.Supplier.CompanyName, smallFont) { Alignment = Element.ALIGN_CENTER });

                signaturesTable.AddCell(supplierSigCell);
                document.Add(signaturesTable);
                document.Add(new Paragraph(" ", normalFont) { SpacingAfter = 30 });

                // Footer
                var footerTable = new PdfPTable(1);
                footerTable.WidthPercentage = 100;
                var footerCell = new PdfPCell();
                footerCell.Border = Rectangle.TOP_BORDER;
                footerCell.BorderColor = primaryColor;
                footerCell.BorderWidth = 2;
                footerCell.Padding = 15;
                footerCell.BackgroundColor = new BaseColor(248, 249, 250);

                var footerInfo = new Paragraph("PixelSolution Business Management System", boldFont);
                footerInfo.Alignment = Element.ALIGN_CENTER;
                footerCell.AddElement(footerInfo);

                var contactInfo = new Paragraph("Professional Supplier Invoice Management", normalFont);
                contactInfo.Alignment = Element.ALIGN_CENTER;
                footerCell.AddElement(contactInfo);

                var contactDetails = new Paragraph("📧 support@pixelsolution.com | 📞 +254-XXX-XXXX", smallFont);
                contactDetails.Alignment = Element.ALIGN_CENTER;
                contactDetails.SpacingBefore = 5;
                footerCell.AddElement(contactDetails);

                var generatedBy = new Paragraph($"Generated on {DateTime.Now:dddd, MMMM dd, yyyy 'at' HH:mm:ss} by PixelSolution System", smallFont);
                generatedBy.Alignment = Element.ALIGN_CENTER;
                generatedBy.SpacingBefore = 10;
                footerCell.AddElement(generatedBy);

                var officialDoc = new Paragraph("This is an official computer-generated invoice document.", smallFont);
                officialDoc.Alignment = Element.ALIGN_CENTER;
                officialDoc.SpacingBefore = 5;
                footerCell.AddElement(officialDoc);

                footerTable.AddCell(footerCell);
                document.Add(footerTable);

                document.Close();
                return memoryStream.ToArray();
            }
            catch (Exception ex)
            {
                throw new Exception($"Error generating supplier invoice PDF: {ex.Message}", ex);
            }
        }
    }
}