using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using PixelSolution.Services.Interfaces;
using PixelSolution.ViewModels;
using PixelSolution.Models;

namespace PixelSolution.Controllers
{
    [Authorize(Roles = "Employee,Admin,Manager")]
    public class EmployeeController : Controller
    {
        private readonly IReportService _reportService;
        private readonly ISalesService _salesService;

        public EmployeeController(IReportService reportService, ISalesService salesService)
        {
            _reportService = reportService;
            _salesService = salesService;
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "0");
                var dashboardData = await _reportService.GetEmployeeDashboardDataAsync(userId);
                return View(dashboardData);
            }
            catch (Exception ex)
            {
                // Log error and return view with empty data
                var emptyData = new DashboardViewModel
                {
                    Stats = new DashboardStatsViewModel(),
                    Charts = new DashboardChartsViewModel(),
                    RecentSales = new List<RecentSaleViewModel>(),
                    SidebarCounts = new SidebarCountsViewModel()
                };
                return View(emptyData);
            }
        }

        public async Task<IActionResult> Sales()
        {
            try
            {
                var salesData = await _reportService.GetSalesPageDataAsync();
                return View(salesData);
            }
            catch (Exception ex)
            {
                var emptySalesData = new SalesPageViewModel
                {
                    TodaysSales = 0,
                    TodaysTransactions = 0,
                    AverageTransaction = 0
                };
                return View(emptySalesData);
            }
        }

        public IActionResult Messages()
        {
            return View();
        }

        public IActionResult Settings()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetDashboardData()
        {
            try
            {
                var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "0");
                var data = await _reportService.GetEmployeeDashboardDataAsync(userId);
                return Json(data);
            }
            catch (Exception ex)
            {
                return Json(new { error = "Failed to load dashboard data" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetProductsForSale()
        {
            try
            {
                var salesData = await _reportService.GetSalesPageDataAsync();
                return Json(new { 
                    success = true, 
                    products = salesData.Products,
                    message = "Products loaded successfully"
                });
            }
            catch (Exception ex)
            {
                return Json(new { 
                    success = false, 
                    products = new List<object>(),
                    message = "Failed to load products: " + ex.Message
                });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessSale([FromBody] ProcessSaleRequest request)
        {
            try
            {
                var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "0");
                var result = await _salesService.ProcessSaleAsync(request, userId);
                
                if (result.Success)
                {
                    return Json(new { 
                        success = true, 
                        saleId = result.SaleId,
                        receiptData = result.ReceiptData,
                        message = "Sale processed successfully" 
                    });
                }
                else
                {
                    return Json(new { 
                        success = false, 
                        message = result.ErrorMessage 
                    });
                }
            }
            catch (Exception ex)
            {
                return Json(new { 
                    success = false, 
                    message = "Failed to process sale: " + ex.Message 
                });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetCategories()
        {
            try
            {
                var categories = await _reportService.GetCategoriesAsync();
                return Json(new { 
                    success = true, 
                    categories = categories 
                });
            }
            catch (Exception ex)
            {
                return Json(new { 
                    success = false, 
                    categories = new List<object>(),
                    message = "Failed to load categories: " + ex.Message
                });
            }
        }
    }
}
