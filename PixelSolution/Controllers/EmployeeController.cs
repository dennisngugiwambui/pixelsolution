using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using PixelSolution.Services.Interfaces;
using PixelSolution.ViewModels;

namespace PixelSolution.Controllers
{
    [Authorize]
    public class EmployeeController : Controller
    {
        private readonly IReportService _reportService;

        public EmployeeController(IReportService reportService)
        {
            _reportService = reportService;
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                var dashboardData = await _reportService.GetDashboardDataAsync();
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

        [HttpGet]
        public async Task<IActionResult> GetDashboardData()
        {
            try
            {
                var data = await _reportService.GetDashboardDataAsync();
                return Json(data);
            }
            catch (Exception ex)
            {
                return Json(new { error = "Failed to load dashboard data" });
            }
        }
    }
}
