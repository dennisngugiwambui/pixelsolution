using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PixelSolution.Data;
using PixelSolution.Services.Interfaces;
using PixelSolution.Utilities;

namespace PixelSolution.Controllers
{
    public class DebugAuthController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuthService _authService;
        private readonly PasswordUtility _passwordUtility;

        public DebugAuthController(ApplicationDbContext context, IAuthService authService)
        {
            _context = context;
            _authService = authService;
            _passwordUtility = new PasswordUtility(_context);
        }

        // GET: /DebugAuth/CompareAuthentication
        public async Task<IActionResult> CompareAuthentication()
        {
            try
            {
                string email = "dennisngugi219@gmail.com";
                string password = "Admin1234";

                ViewBag.Email = email;
                ViewBag.Password = password;

                // Test 1: PasswordUtility test
                bool utilityTest = await _passwordUtility.TestPasswordAsync(email, password);
                ViewBag.UtilityTest = utilityTest;

                // Test 2: AuthService authentication
                var authResult = await _authService.AuthenticateAsync(email, password);
                ViewBag.AuthServiceTest = authResult != null;
                ViewBag.AuthServiceUser = authResult?.Email ?? "NULL";

                // Test 3: Direct database query to see current hash
                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower());
                
                ViewBag.UserFound = user != null;
                ViewBag.CurrentHash = user?.PasswordHash ?? "NULL";
                ViewBag.UserStatus = user?.Status ?? "NULL";
                ViewBag.UserType = user?.UserType ?? "NULL";

                // Test 4: Direct BCrypt verification
                if (user != null && !string.IsNullOrEmpty(user.PasswordHash))
                {
                    bool directBcryptTest = BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);
                    ViewBag.DirectBcryptTest = directBcryptTest;
                }
                else
                {
                    ViewBag.DirectBcryptTest = false;
                }

                return View();
            }
            catch (Exception ex)
            {
                ViewBag.Error = ex.Message;
                ViewBag.StackTrace = ex.StackTrace;
                return View();
            }
        }
    }
}
