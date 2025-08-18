using Microsoft.EntityFrameworkCore;
using PixelSolution.Data;
using PixelSolution.Models;
using PixelSolution.Services.Interfaces;
using System.Text;
using iTextSharp.text;
using iTextSharp.text.pdf;
using ClosedXML.Excel;
using System.Drawing;

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

                // Get sidebar counts
                var sidebarCounts = await GetSidebarCountsAsync();

                // Today's sales
                var todaySales = await _context.Sales
                    .Where(s => s.SaleDate.Date == today && s.Status == "Completed")
                    .SumAsync(s => s.AmountPaid);

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
                        SalesPerson = s.User.FirstName + " " + s.User.LastName
                    })
                    .ToListAsync();

                return new
                {
                    stats = new
                    {
                        todaySales = new { value = todaySales, count = todaySalesCount },
                        thisMonthSales = new { value = thisMonthSales, growth = salesGrowth },
                        thisMonthOrders = thisMonthOrders,
                        productsSold = thisMonthProductsSold,
                        newCustomers = newCustomers
                    },
                    charts = new
                    {
                        salesData = await GetSalesChartDataAsync(),
                        topProducts = await GetTopProductsAsync()
                    },
                    recentSales = recentSales,
                    sidebarCounts = sidebarCounts
                };
            }
            catch (Exception ex)
            {
                throw new Exception($"Error generating dashboard data: {ex.Message}", ex);
            }
        }

        private async Task<object> GetSidebarCountsAsync()
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

            return new
            {
                todaySales = todaySalesCount,
                lowStock = lowStockCount,
                pendingRequests = pendingRequestsCount,
                unreadMessages = unreadMessagesCount
            };
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

                var totalSales = sales.Sum(s => s.AmountPaid);
                var totalTransactions = sales.Count;
                var averageTransaction = totalTransactions > 0 ? totalSales / totalTransactions : 0;

                var salesByDate = sales
                    .GroupBy(s => s.SaleDate.Date)
                    .Select(g => new
                    {
                        date = g.Key.ToString("yyyy-MM-dd"),
                        amount = g.Sum(s => s.AmountPaid),
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

        public async Task<object> GetInventoryReportAsync()
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

        public async Task<byte[]> GenerateSalesReceiptAsync(int saleId)
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

        private async Task<object> GetSalesChartDataAsync()
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
            var result = new List<object>();
            for (int month = 1; month <= 12; month++)
            {
                var monthData = salesData.FirstOrDefault(s => s.month == month);
                var monthName = new DateTime(currentYear, month, 1).ToString("MMM");
                result.Add(new
                {
                    date = monthName,
                    amount = monthData?.amount ?? 0
                });
            }

            Console.WriteLine($"DEBUG: Final chart data: {System.Text.Json.JsonSerializer.Serialize(result)}");
            return result;
        }

        private async Task<object> GetTopProductsAsync()
        {
            var currentYear = DateTime.Today.Year;
            var startOfYear = new DateTime(currentYear, 1, 1);

            // Check if we have any sale items
            var totalSaleItems = await _context.SaleItems.CountAsync();
            Console.WriteLine($"DEBUG: Total sale items in database: {totalSaleItems}");

            var topProducts = await _context.SaleItems
                .Include(si => si.Product)
                .Include(si => si.Sale)
                .Where(si => si.Sale.SaleDate >= startOfYear && si.Sale.Status == "Completed")
                .GroupBy(si => new { si.ProductId, si.Product.Name })
                .Select(g => new
                {
                    productId = g.Key.ProductId,
                    productName = g.Key.Name,
                    salesCount = g.Count(),
                    quantitySold = g.Sum(si => si.Quantity),
                    revenue = g.Sum(si => si.TotalPrice)
                })
                .OrderByDescending(x => x.salesCount)
                .Take(5)
                .ToListAsync();

            Console.WriteLine($"DEBUG: Top products query result: {System.Text.Json.JsonSerializer.Serialize(topProducts)}");

            // If no current year data, get data from any completed sales
            if (!topProducts.Any())
            {
                Console.WriteLine("DEBUG: No current year product data, getting all completed sales");
                topProducts = await _context.SaleItems
                    .Include(si => si.Product)
                    .Include(si => si.Sale)
                    .Where(si => si.Sale.Status == "Completed")
                    .GroupBy(si => new { si.ProductId, si.Product.Name })
                    .Select(g => new
                    {
                        productId = g.Key.ProductId,
                        productName = g.Key.Name,
                        salesCount = g.Count(),
                        quantitySold = g.Sum(si => si.Quantity),
                        revenue = g.Sum(si => si.TotalPrice)
                    })
                    .OrderByDescending(x => x.salesCount)
                    .Take(5)
                    .ToListAsync();
            }

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
            var reportData = await GetUserActivityReportAsync();
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
                
                // Summary Statistics using AmountPaid
                var totalRevenue = sales.Sum(s => s.AmountPaid);
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
                    table.AddCell(new PdfPCell(new Phrase(sale.AmountPaid.ToString("N2"), dataFont)) { Padding = 5f, HorizontalAlignment = Element.ALIGN_RIGHT });
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
                var reportTitle = new Paragraph("User Activity Report", reportTitleFont);
                reportTitle.Alignment = Element.ALIGN_CENTER;
                reportTitle.SpacingAfter = 20f;
                document.Add(reportTitle);
                
                // Summary Statistics
                var summaryFont = FontFactory.GetFont(FontFactory.HELVETICA, 10, BaseColor.BLACK);
                var summaryText = new Paragraph($"Total Users: {summary.TotalUsers} | Active Users: {summary.ActiveUsers} | Users with Activity: {summary.UsersWithActivity} | Total Activities: {summary.TotalActivities} | Generated: {DateTime.Now:dd/MM/yyyy HH:mm}", summaryFont);
                summaryText.Alignment = Element.ALIGN_CENTER;
                summaryText.SpacingAfter = 20f;
                document.Add(summaryText);
                
                // User Summary Table
                var sectionFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 12, BaseColor.BLACK);
                var userSummaryTitle = new Paragraph("USER ACTIVITY SUMMARY", sectionFont);
                userSummaryTitle.SpacingAfter = 10f;
                document.Add(userSummaryTitle);
                
                var table = new PdfPTable(9);
                table.WidthPercentage = 100;
                table.SetWidths(new float[] { 15f, 20f, 10f, 10f, 10f, 10f, 10f, 10f, 15f });
                
                // Table headers
                var headerFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 9, BaseColor.WHITE);
                var headers = new string[] { "Name", "Email", "Department", "Sales", "Total Activities", "Logins", "Sales Activities", "Report Exports", "Last Activity" };
                
                foreach (var header in headers)
                {
                    var cell = new PdfPCell(new Phrase(header, headerFont));
                    cell.BackgroundColor = BaseColor.DARK_GRAY;
                    cell.HorizontalAlignment = Element.ALIGN_CENTER;
                    cell.Padding = 5f;
                    table.AddCell(cell);
                }
                
                // Table data
                var dataFont = FontFactory.GetFont(FontFactory.HELVETICA, 8, BaseColor.BLACK);
                foreach (var user in users)
                {
                    table.AddCell(new PdfPCell(new Phrase(user.FullName ?? "N/A", dataFont)) { Padding = 4f });
                    table.AddCell(new PdfPCell(new Phrase(user.Email ?? "N/A", dataFont)) { Padding = 4f });
                    table.AddCell(new PdfPCell(new Phrase(user.Department ?? "N/A", dataFont)) { Padding = 4f });
                    table.AddCell(new PdfPCell(new Phrase(user.SalesCount.ToString(), dataFont)) { Padding = 4f, HorizontalAlignment = Element.ALIGN_RIGHT });
                    table.AddCell(new PdfPCell(new Phrase(user.TotalActivities.ToString(), dataFont)) { Padding = 4f, HorizontalAlignment = Element.ALIGN_RIGHT });
                    table.AddCell(new PdfPCell(new Phrase(user.LoginCount.ToString(), dataFont)) { Padding = 4f, HorizontalAlignment = Element.ALIGN_RIGHT });
                    table.AddCell(new PdfPCell(new Phrase(user.SaleActivities.ToString(), dataFont)) { Padding = 4f, HorizontalAlignment = Element.ALIGN_RIGHT });
                    table.AddCell(new PdfPCell(new Phrase(user.ReportExports.ToString(), dataFont)) { Padding = 4f, HorizontalAlignment = Element.ALIGN_RIGHT });
                    table.AddCell(new PdfPCell(new Phrase(user.LastActivity?.ToString("dd/MM/yyyy HH:mm") ?? "Never", dataFont)) { Padding = 4f });
                }
                
                document.Add(table);
                document.Add(new Paragraph(" "));
                
                // Detailed Activity Log Section
                var detailTitle = new Paragraph("RECENT ACTIVITY LOG", sectionFont);
                detailTitle.SpacingAfter = 10f;
                document.Add(detailTitle);
                
                // Detailed Activities Table
                var detailTable = new PdfPTable(5);
                detailTable.WidthPercentage = 100;
                detailTable.SetWidths(new float[] { 20f, 15f, 35f, 20f, 10f });
                
                // Detail table headers
                var detailHeaders = new string[] { "User", "Activity Type", "Description", "Date & Time", "IP Address" };
                foreach (var header in detailHeaders)
                {
                    var cell = new PdfPCell(new Phrase(header, headerFont));
                    cell.BackgroundColor = BaseColor.DARK_GRAY;
                    cell.HorizontalAlignment = Element.ALIGN_CENTER;
                    cell.Padding = 5f;
                    detailTable.AddCell(cell);
                }
                
                // Detail table data
                foreach (var activity in detailedActivities)
                {
                    detailTable.AddCell(new PdfPCell(new Phrase(activity.UserName ?? "Unknown", dataFont)) { Padding = 4f });
                    detailTable.AddCell(new PdfPCell(new Phrase(activity.ActivityType ?? "N/A", dataFont)) { Padding = 4f });
                    detailTable.AddCell(new PdfPCell(new Phrase(activity.Description ?? "N/A", dataFont)) { Padding = 4f });
                    detailTable.AddCell(new PdfPCell(new Phrase(activity.CreatedAt.ToString("dd/MM/yyyy HH:mm:ss"), dataFont)) { Padding = 4f });
                    detailTable.AddCell(new PdfPCell(new Phrase(activity.IpAddress ?? "N/A", dataFont)) { Padding = 4f });
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

        public async Task<byte[]> GenerateReceiptPdfAsync(PixelSolution.ViewModels.ReceiptPdfRequest request)
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
                
                // Receipt Title
                var receiptTitleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 14, BaseColor.BLACK);
                var receiptTitle = new Paragraph("SALES RECEIPT", receiptTitleFont);
                receiptTitle.Alignment = Element.ALIGN_CENTER;
                receiptTitle.SpacingAfter = 20f;
                document.Add(receiptTitle);
                
                // Receipt Details
                var normalFont = FontFactory.GetFont(FontFactory.HELVETICA, 10, BaseColor.BLACK);
                document.Add(new Paragraph($"Receipt #: {request.SaleNumber}", normalFont));
                document.Add(new Paragraph($"Date: {request.SaleDate:dd/MM/yyyy HH:mm}", normalFont));
                document.Add(new Paragraph($"Cashier: {request.CashierName}", normalFont));
                if (!string.IsNullOrEmpty(request.CustomerName))
                {
                    document.Add(new Paragraph($"Customer: {request.CustomerName}", normalFont));
                }
                document.Add(new Paragraph(" ", normalFont)); // Spacing
                
                // Items Table
                var table = new PdfPTable(4);
                table.WidthPercentage = 100;
                table.SetWidths(new float[] { 40f, 15f, 20f, 25f });
                
                // Table headers
                var headerFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10, BaseColor.WHITE);
                var headers = new string[] { "Item", "Qty", "Unit Price", "Total" };
                
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
                foreach (var item in request.Items)
                {
                    table.AddCell(new PdfPCell(new Phrase(item.Name, dataFont)) { Padding = 5f });
                    table.AddCell(new PdfPCell(new Phrase(item.Quantity.ToString(), dataFont)) { Padding = 5f, HorizontalAlignment = Element.ALIGN_CENTER });
                    table.AddCell(new PdfPCell(new Phrase($"KSh {item.UnitPrice:N2}", dataFont)) { Padding = 5f, HorizontalAlignment = Element.ALIGN_RIGHT });
                    table.AddCell(new PdfPCell(new Phrase($"KSh {item.Total:N2}", dataFont)) { Padding = 5f, HorizontalAlignment = Element.ALIGN_RIGHT });
                }
                
                document.Add(table);
                
                // Totals
                document.Add(new Paragraph(" ", normalFont)); // Spacing
                var boldFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10, BaseColor.BLACK);
                document.Add(new Paragraph($"Subtotal: KSh {request.Subtotal:N2}", normalFont) { Alignment = Element.ALIGN_RIGHT });
                document.Add(new Paragraph($"Tax (16%): KSh {request.Tax:N2}", normalFont) { Alignment = Element.ALIGN_RIGHT });
                document.Add(new Paragraph($"Total: KSh {request.TotalAmount:N2}", boldFont) { Alignment = Element.ALIGN_RIGHT });
                document.Add(new Paragraph($"Amount Paid: KSh {request.AmountPaid:N2}", normalFont) { Alignment = Element.ALIGN_RIGHT });
                if (request.ChangeGiven > 0)
                {
                    document.Add(new Paragraph($"Change: KSh {request.ChangeGiven:N2}", normalFont) { Alignment = Element.ALIGN_RIGHT });
                }
                
                // Footer
                document.Add(new Paragraph(" ", normalFont)); // Spacing
                var footerFont = FontFactory.GetFont(FontFactory.HELVETICA_OBLIQUE, 8, BaseColor.GRAY);
                var footer = new Paragraph("Thank you for your business!", footerFont);
                footer.Alignment = Element.ALIGN_CENTER;
                document.Add(footer);
                
                document.Close();
                return stream.ToArray();
            }
        }

        private byte[] GenerateSalesReportExcel(List<Sale> sales, DateTime startDate, DateTime endDate)
        {
            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Sales Report");
                
                // Company Header
                worksheet.Range("A1:E1").Merge();
                worksheet.Cell("A1").Value = "PIXEL SOLUTION COMPANY LTD";
                worksheet.Cell("A1").Style.Font.FontSize = 18;
                worksheet.Cell("A1").Style.Font.Bold = true;
                worksheet.Cell("A1").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                
                // Report Title
                worksheet.Range("A2:E2").Merge();
                worksheet.Cell("A2").Value = $"Sales Report ({startDate:dd/MM/yyyy} - {endDate:dd/MM/yyyy})";
                worksheet.Cell("A2").Style.Font.FontSize = 14;
                worksheet.Cell("A2").Style.Font.Bold = true;
                worksheet.Cell("A2").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                
                // Summary using AmountPaid
                var totalRevenue = sales.Sum(s => s.AmountPaid);
                worksheet.Range("A3:E3").Merge();
                worksheet.Cell("A3").Value = $"Total Sales: {sales.Count} | Total Revenue: KSh {totalRevenue:N2} | Generated: {DateTime.Now:dd/MM/yyyy HH:mm}";
                worksheet.Cell("A3").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                
                // Headers
                var headers = new string[] { "Date", "Sale #", "Customer", "Amount (KSh)", "Cashier" };
                for (int i = 0; i < headers.Length; i++)
                {
                    worksheet.Cell(5, i + 1).Value = headers[i];
                    worksheet.Cell(5, i + 1).Style.Font.Bold = true;
                    worksheet.Cell(5, i + 1).Style.Fill.BackgroundColor = XLColor.LightGray;
                }
                
                // Data
                int row = 6;
                foreach (var sale in sales)
                {
                    worksheet.Cell(row, 1).Value = sale.SaleDate.ToString("dd/MM/yyyy");
                    worksheet.Cell(row, 2).Value = sale.SaleNumber ?? "N/A";
                    worksheet.Cell(row, 3).Value = sale.CustomerName ?? "Walk-in";
                    worksheet.Cell(row, 4).Value = sale.AmountPaid;
                    worksheet.Cell(row, 4).Style.NumberFormat.Format = "#,##0.00";
                    worksheet.Cell(row, 5).Value = sale.User?.FirstName + " " + sale.User?.LastName ?? "Unknown";
                    row++;
                }
                
                // Auto-fit columns
                worksheet.ColumnsUsed().AdjustToContents();
                
                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    return stream.ToArray();
                }
            }
        }

        private byte[] GenerateInventoryReportExcel(List<Product> products)
        {
            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Inventory Report");
                
                // Company Header
                worksheet.Range("A1:F1").Merge();
                worksheet.Cell("A1").Value = "PIXEL SOLUTION COMPANY LTD";
                worksheet.Cell("A1").Style.Font.FontSize = 18;
                worksheet.Cell("A1").Style.Font.Bold = true;
                worksheet.Cell("A1").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                
                // Report Title
                worksheet.Range("A2:F2").Merge();
                worksheet.Cell("A2").Value = "Inventory Report";
                worksheet.Cell("A2").Style.Font.FontSize = 14;
                worksheet.Cell("A2").Style.Font.Bold = true;
                worksheet.Cell("A2").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                
                // Summary
                var totalValue = products.Sum(p => p.StockQuantity * p.SellingPrice);
                worksheet.Range("A3:F3").Merge();
                worksheet.Cell("A3").Value = $"Total Products: {products.Count} | Total Stock Value: KSh {totalValue:N2} | Generated: {DateTime.Now:dd/MM/yyyy HH:mm}";
                worksheet.Cell("A3").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                
                // Headers
                var headers = new string[] { "Product", "SKU", "Category", "Stock", "Price (KSh)", "Value (KSh)" };
                for (int i = 0; i < headers.Length; i++)
                {
                    worksheet.Cell(5, i + 1).Value = headers[i];
                    worksheet.Cell(5, i + 1).Style.Font.Bold = true;
                    worksheet.Cell(5, i + 1).Style.Fill.BackgroundColor = XLColor.LightGray;
                }
                
                // Data
                int row = 6;
                foreach (var product in products)
                {
                    var stockValue = product.StockQuantity * product.SellingPrice;
                    worksheet.Cell(row, 1).Value = product.Name ?? "N/A";
                    worksheet.Cell(row, 2).Value = product.SKU ?? "N/A";
                    worksheet.Cell(row, 3).Value = product.Category?.Name ?? "N/A";
                    worksheet.Cell(row, 4).Value = product.StockQuantity;
                    worksheet.Cell(row, 5).Value = product.SellingPrice;
                    worksheet.Cell(row, 5).Style.NumberFormat.Format = "#,##0.00";
                    worksheet.Cell(row, 6).Value = stockValue;
                    worksheet.Cell(row, 6).Style.NumberFormat.Format = "#,##0.00";
                    row++;
                }
                
                // Auto-fit columns
                worksheet.ColumnsUsed().AdjustToContents();
                
                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    return stream.ToArray();
                }
            }
        }

        private byte[] GenerateUserReportExcel(List<User> users)
        {
            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Users Report");
                
                // Company Header
                worksheet.Range("A1:E1").Merge();
                worksheet.Cell("A1").Value = "PIXEL SOLUTION COMPANY LTD";
                worksheet.Cell("A1").Style.Font.FontSize = 18;
                worksheet.Cell("A1").Style.Font.Bold = true;
                worksheet.Cell("A1").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                
                // Report Title
                worksheet.Range("A2:E2").Merge();
                worksheet.Cell("A2").Value = "Users Report";
                worksheet.Cell("A2").Style.Font.FontSize = 14;
                worksheet.Cell("A2").Style.Font.Bold = true;
                worksheet.Cell("A2").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                
                // Summary
                worksheet.Range("A3:E3").Merge();
                worksheet.Cell("A3").Value = $"Total Active Users: {users.Count} | Generated: {DateTime.Now:dd/MM/yyyy HH:mm}";
                worksheet.Cell("A3").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                
                // Headers
                var headers = new string[] { "Name", "Email", "Type", "Department", "Status" };
                for (int i = 0; i < headers.Length; i++)
                {
                    worksheet.Cell(5, i + 1).Value = headers[i];
                    worksheet.Cell(5, i + 1).Style.Font.Bold = true;
                    worksheet.Cell(5, i + 1).Style.Fill.BackgroundColor = XLColor.LightGray;
                }
                
                // Data
                int row = 6;
                foreach (var user in users)
                {
                    worksheet.Cell(row, 1).Value = $"{user.FirstName} {user.LastName}";
                    worksheet.Cell(row, 2).Value = user.Email ?? "N/A";
                    worksheet.Cell(row, 3).Value = user.UserType ?? "N/A";
                    worksheet.Cell(row, 4).Value = user.Department ?? "N/A";
                    worksheet.Cell(row, 5).Value = user.Status ?? "N/A";
                    row++;
                }
                
                // Auto-fit columns
                worksheet.ColumnsUsed().AdjustToContents();
                
                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    return stream.ToArray();
                }
            }
        }

        private byte[] GenerateCategoriesReportExcel(List<Category> categories)
        {
            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Categories Report");
                
                // Company Header
                worksheet.Range("A1:D1").Merge();
                worksheet.Cell("A1").Value = "PIXEL SOLUTION COMPANY LTD";
                worksheet.Cell("A1").Style.Font.FontSize = 18;
                worksheet.Cell("A1").Style.Font.Bold = true;
                worksheet.Cell("A1").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                
                // Report Title
                worksheet.Range("A2:D2").Merge();
                worksheet.Cell("A2").Value = "Categories Report";
                worksheet.Cell("A2").Style.Font.FontSize = 14;
                worksheet.Cell("A2").Style.Font.Bold = true;
                worksheet.Cell("A2").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                
                // Summary
                var totalProducts = categories.Sum(c => c.Products.Count);
                worksheet.Range("A3:D3").Merge();
                worksheet.Cell("A3").Value = $"Total Categories: {categories.Count} | Total Products: {totalProducts} | Generated: {DateTime.Now:dd/MM/yyyy HH:mm}";
                worksheet.Cell("A3").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                
                // Headers
                var headers = new string[] { "Category", "Description", "Products", "Status" };
                for (int i = 0; i < headers.Length; i++)
                {
                    worksheet.Cell(5, i + 1).Value = headers[i];
                    worksheet.Cell(5, i + 1).Style.Font.Bold = true;
                    worksheet.Cell(5, i + 1).Style.Fill.BackgroundColor = XLColor.LightGray;
                }
                
                // Data
                int row = 6;
                foreach (var category in categories)
                {
                    worksheet.Cell(row, 1).Value = category.Name ?? "N/A";
                    worksheet.Cell(row, 2).Value = category.Description ?? "N/A";
                    worksheet.Cell(row, 3).Value = category.Products.Count;
                    worksheet.Cell(row, 4).Value = category.Status ?? "N/A";
                    row++;
                }
                
                // Auto-fit columns
                worksheet.ColumnsUsed().AdjustToContents();
                
                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    return stream.ToArray();
                }
            }
        }

        public async Task<object> GetSupplierReportAsync()
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

        public async Task<object> GetUserActivityReportAsync()
        {
            try
            {
                // Get users with activity logs
                var users = await _context.Users
                    .Include(u => u.UserDepartments)
                        .ThenInclude(ud => ud.Department)
                    .Include(u => u.Sales)
                    .Include(u => u.PurchaseRequests)
                    .ToListAsync();

                // Get user activity data from UserActivityLogs table - simplified query
                var userActivities = await _context.UserActivityLogs
                    .ToListAsync();

                // Get detailed activity logs for the report
                var detailedActivities = await _context.UserActivityLogs
                    .Include(ual => ual.User)
                    .OrderByDescending(ual => ual.CreatedAt)
                    .Take(50) // Latest 50 activities
                    .ToListAsync();

                // Group activities by user in memory to avoid LINQ translation issues
                var activityGroups = userActivities
                    .GroupBy(ual => ual.UserId)
                    .ToDictionary(g => g.Key, g => new
                    {
                        TotalActivities = g.Count(),
                        LastActivity = g.Max(ual => ual.CreatedAt),
                        LoginCount = g.Count(ual => ual.ActivityType == "Login"),
                        SaleActivities = g.Count(ual => ual.ActivityType == "Sale"),
                        ReportExports = g.Count(ual => ual.ActivityType == "ReportExport")
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
                        .Sum(s => s.AmountPaid),
                    PurchaseRequestCount = u.PurchaseRequests.Count,
                    LastSaleDate = u.Sales
                        .Where(s => s.Status == "Completed")
                        .OrderByDescending(s => s.SaleDate)
                        .Select(s => s.SaleDate)
                        .FirstOrDefault(),
                    // Activity log data - using dictionary lookup
                    TotalActivities = activityGroups.ContainsKey(u.UserId) ? activityGroups[u.UserId].TotalActivities : 0,
                    LastActivity = activityGroups.ContainsKey(u.UserId) ? activityGroups[u.UserId].LastActivity : (DateTime?)null,
                    LoginCount = activityGroups.ContainsKey(u.UserId) ? activityGroups[u.UserId].LoginCount : 0,
                    SaleActivities = activityGroups.ContainsKey(u.UserId) ? activityGroups[u.UserId].SaleActivities : 0,
                    ReportExports = activityGroups.ContainsKey(u.UserId) ? activityGroups[u.UserId].ReportExports : 0,
                    u.CreatedAt
                })
                .OrderBy(u => u.FirstName)
                .ThenBy(u => u.LastName)
                .ToList();

                var summary = new
                {
                    TotalUsers = userReport.Count,
                    ActiveUsers = userReport.Count(u => u.Status == "Active"),
                    TotalSales = userReport.Sum(u => u.TotalSalesAmount),
                    TotalTransactions = userReport.Sum(u => u.SalesCount),
                    TotalActivities = userReport.Sum(u => u.TotalActivities),
                    UsersWithActivity = userReport.Count(u => u.TotalActivities > 0)
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
                        da.ActivityType,
                        da.Description,
                        da.CreatedAt,
                        da.IpAddress
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
                var reportData = await GetSupplierReportAsync();
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
                var reportData = await GetSupplierReportAsync();
                var data = (dynamic)reportData;

                using (var workbook = new XLWorkbook())
                {
                    var worksheet = workbook.Worksheets.Add("Suppliers Report");

                    // Company Header
                    worksheet.Range("A1:G1").Merge();
                    worksheet.Cell("A1").Value = "PIXEL SOLUTION COMPANY LTD";
                    worksheet.Cell("A1").Style.Font.FontSize = 18;
                    worksheet.Cell("A1").Style.Font.Bold = true;
                    worksheet.Cell("A1").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                    // Report Title
                    worksheet.Range("A2:G2").Merge();
                    worksheet.Cell("A2").Value = "Suppliers Report";
                    worksheet.Cell("A2").Style.Font.FontSize = 14;
                    worksheet.Cell("A2").Style.Font.Bold = true;
                    worksheet.Cell("A2").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                    // Summary
                    worksheet.Range("A3:G3").Merge();
                    worksheet.Cell("A3").Value = $"Total Suppliers: {data.Summary.TotalSuppliers} | Active Suppliers: {data.Summary.ActiveSuppliers} | Total Purchase Value: KSh {data.Summary.TotalPurchaseValue:N2} | Generated: {DateTime.Now:dd/MM/yyyy HH:mm}";
                    worksheet.Cell("A3").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                    // Headers
                    var headers = new string[] { "Company", "Contact Person", "Email", "Phone", "Products", "Status", "Purchase Value" };
                    for (int i = 0; i < headers.Length; i++)
                    {
                        worksheet.Cell(5, i + 1).Value = headers[i];
                        worksheet.Cell(5, i + 1).Style.Font.Bold = true;
                        worksheet.Cell(5, i + 1).Style.Fill.BackgroundColor = XLColor.LightGray;
                    }

                    // Data
                    int row = 6;
                    foreach (var supplier in data.Suppliers)
                    {
                        worksheet.Cell(row, 1).Value = supplier.CompanyName.ToString();
                        worksheet.Cell(row, 2).Value = supplier.ContactPerson.ToString();
                        worksheet.Cell(row, 3).Value = supplier.Email.ToString();
                        worksheet.Cell(row, 4).Value = supplier.Phone.ToString();
                        worksheet.Cell(row, 5).Value = supplier.ProductCount;
                        worksheet.Cell(row, 6).Value = supplier.Status.ToString();
                        worksheet.Cell(row, 7).Value = supplier.TotalPurchaseValue;
                        worksheet.Cell(row, 7).Style.NumberFormat.Format = "#,##0.00";
                        row++;
                    }

                    // Auto-fit columns
                    worksheet.ColumnsUsed().AdjustToContents();

                    using (var stream = new MemoryStream())
                    {
                        workbook.SaveAs(stream);
                        return stream.ToArray();
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Error generating suppliers report Excel: {ex.Message}", ex);
            }
        }

        public async Task<byte[]> GenerateComprehensiveReportAsync(DateTime startDate, DateTime endDate)
        {
            try
            {
                // Get all report data directly without try-catch to see actual errors
                var salesData = await GetSalesReportAsync(startDate, endDate);
                var inventoryData = await GetInventoryReportAsync();
                var usersData = await GetUserActivityReportAsync();
                var suppliersData = await GetSupplierReportAsync();

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

    }
}