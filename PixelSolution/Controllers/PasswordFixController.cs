using Microsoft.AspNetCore.Mvc;
using PixelSolution.Data;
using PixelSolution.Utilities;

namespace PixelSolution.Controllers
{
    public class PasswordFixController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly PasswordUtility _passwordUtility;

        public PasswordFixController(ApplicationDbContext context)
        {
            _context = context;
            _passwordUtility = new PasswordUtility(_context);
        }

        // GET: /PasswordFix/FixAdminPassword
        public async Task<IActionResult> FixAdminPassword()
        {
            try
            {
                string email = "dennisngugi219@gmail.com";
                string password = "Admin1234";

                // Test current password first
                bool currentTest = await _passwordUtility.TestPasswordAsync(email, password);
                ViewBag.CurrentTest = currentTest;

                if (!currentTest)
                {
                    // Update the password with correct hash using raw SQL (bypasses trigger issues)
                    var updateResult = await _passwordUtility.UpdateUserPasswordWithRawSqlAsync(email, password);
                    ViewBag.UpdateResult = updateResult.Success;
                    ViewBag.UpdateError = updateResult.ErrorMessage;

                    if (updateResult.Success)
                    {
                        // Test again after update
                        bool newTest = await _passwordUtility.TestPasswordAsync(email, password);
                        ViewBag.NewTest = newTest;
                        ViewBag.Message = "Password hash updated successfully!";
                    }
                    else
                    {
                        ViewBag.Message = $"Failed to update password hash: {updateResult.ErrorMessage}";
                    }
                }
                else
                {
                    ViewBag.Message = "Password hash is already correct!";
                }

                return View();
            }
            catch (Exception ex)
            {
                ViewBag.Error = ex.Message;
                return View();
            }
        }
    }
}
