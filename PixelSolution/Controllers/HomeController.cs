using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using PixelSolution.Models;

namespace PixelSolution.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        public IActionResult Index()
        {
            try
            {
                // If user is authenticated, redirect to dashboard
                if (User.Identity?.IsAuthenticated == true)
                {
                    return RedirectToAction("Dashboard", "Admin");
                }

                // If not authenticated, redirect to login
                return RedirectToAction("Login", "Auth");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Home Index");
                return RedirectToAction("Login", "Auth");
            }
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}