using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PixelSolution.Data;
using PixelSolution.Models;
using PixelSolution.Services;
using System.Security.Claims;

namespace PixelSolution.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin,Manager")]
    public class EmployeeManagementApiController : ControllerBase
    {
        private readonly IEmployeeManagementService _employeeService;
        private readonly ApplicationDbContext _context;

        public EmployeeManagementApiController(
            IEmployeeManagementService employeeService,
            ApplicationDbContext context)
        {
            _employeeService = employeeService;
            _context = context;
        }

        // Employee Profile Management
        [HttpPost("profile")]
        public async Task<IActionResult> CreateEmployeeProfile([FromBody] CreateEmployeeProfileRequest request)
        {
            var profile = await _employeeService.CreateEmployeeProfileAsync(request);
            if (profile == null)
                return BadRequest(new { message = "Unable to create employee profile" });

            return Ok(new { message = "Employee profile created successfully", profile });
        }

        [HttpGet("profiles")]
        public async Task<IActionResult> GetAllEmployeeProfiles()
        {
            var profiles = await _employeeService.GetAllEmployeeProfilesAsync();
            return Ok(profiles);
        }

        [HttpGet("profile/{userId}")]
        public async Task<IActionResult> GetEmployeeProfile(int userId)
        {
            var profile = await _employeeService.GetEmployeeProfileAsync(userId);
            if (profile == null)
                return NotFound(new { message = "Employee profile not found" });

            return Ok(profile);
        }

        // Salary Management
        [HttpPut("profile/{employeeProfileId}/salary")]
        public async Task<IActionResult> UpdateSalary(int employeeProfileId, [FromBody] UpdateSalaryRequest request)
        {
            var success = await _employeeService.UpdateSalaryAsync(employeeProfileId, request.Amount, request.SalaryType);
            if (!success)
                return BadRequest(new { message = "Unable to update salary" });

            return Ok(new { message = "Salary updated successfully" });
        }

        // Fine Management
        [HttpPost("fine")]
        public async Task<IActionResult> IssueFine([FromBody] IssueFineRequest request)
        {
            var currentUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            var fine = await _employeeService.IssueFineAsync(request, currentUserId);
            
            if (fine == null)
                return BadRequest(new { message = "Unable to issue fine" });

            return Ok(new { message = "Fine issued successfully", fine });
        }

        [HttpPut("fine/{fineId}/pay")]
        public async Task<IActionResult> PayFine(int fineId, [FromBody] PayFineRequest request)
        {
            var success = await _employeeService.PayFineAsync(fineId, request.PaymentMethod);
            if (!success)
                return BadRequest(new { message = "Unable to process fine payment" });

            return Ok(new { message = "Fine payment processed successfully" });
        }

        [HttpGet("profile/{employeeProfileId}/fines")]
        public async Task<IActionResult> GetEmployeeFines(int employeeProfileId)
        {
            var fines = await _employeeService.GetEmployeeFinesAsync(employeeProfileId);
            return Ok(fines);
        }

        [HttpGet("profile/{employeeProfileId}/outstanding-fines")]
        public async Task<IActionResult> GetOutstandingFines(int employeeProfileId)
        {
            var amount = await _employeeService.GetEmployeeOutstandingFinesAsync(employeeProfileId);
            return Ok(new { outstandingAmount = amount });
        }

        // Payment Management
        [HttpPost("payment")]
        public async Task<IActionResult> ProcessPayment([FromBody] ProcessPaymentRequest request)
        {
            var currentUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            var payment = await _employeeService.ProcessPaymentAsync(request, currentUserId);
            
            if (payment == null)
                return BadRequest(new { message = "Unable to process payment" });

            return Ok(new { message = "Payment processed successfully", payment });
        }

        [HttpGet("profile/{employeeProfileId}/payments")]
        public async Task<IActionResult> GetEmployeePayments(int employeeProfileId)
        {
            var payments = await _employeeService.GetEmployeePaymentsAsync(employeeProfileId);
            return Ok(payments);
        }

        // Reports and Analytics
        [HttpGet("payroll-summary")]
        public async Task<IActionResult> GetPayrollSummary([FromQuery] string? period = null)
        {
            try
            {
                var profiles = await _employeeService.GetAllEmployeeProfilesAsync();
                
                var summary = profiles.Select(p => new
                {
                    EmployeeId = p.EmployeeProfileId,
                    EmployeeNumber = p.EmployeeNumber,
                    EmployeeName = p.User.FullName,
                    Position = p.Position,
                    BaseSalary = p.BaseSalary,
                    OutstandingFines = p.Fines.Where(f => f.Status != "Paid").Sum(f => f.Amount),
                    LastPayment = p.Payments.OrderByDescending(pay => pay.PaymentDate).FirstOrDefault()
                }).ToList();

                return Ok(summary);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = "Unable to generate payroll summary", error = ex.Message });
            }
        }

        [HttpGet("salary-history/{employeeProfileId}")]
        public async Task<IActionResult> GetSalaryHistory(int employeeProfileId)
        {
            var profile = await _context.EmployeeProfiles
                .Include(ep => ep.SalaryRecords)
                .FirstOrDefaultAsync(ep => ep.EmployeeProfileId == employeeProfileId);

            if (profile == null)
                return NotFound(new { message = "Employee profile not found" });

            var salaryHistory = profile.SalaryRecords
                .OrderByDescending(sr => sr.EffectiveDate)
                .ToList();

            return Ok(salaryHistory);
        }
    }

    // Request Models for Employee Management
    public class UpdateSalaryRequest
    {
        public decimal Amount { get; set; }
        public string SalaryType { get; set; } = "Base";
    }

    public class PayFineRequest
    {
        public string PaymentMethod { get; set; } = string.Empty;
    }
}
