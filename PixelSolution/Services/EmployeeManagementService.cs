using Microsoft.EntityFrameworkCore;
using PixelSolution.Data;
using PixelSolution.Models;

namespace PixelSolution.Services
{
    public interface IEmployeeManagementService
    {
        Task<EmployeeProfile?> CreateEmployeeProfileAsync(CreateEmployeeProfileRequest request);
        Task<EmployeeProfile?> GetEmployeeProfileAsync(int userId);
        Task<List<EmployeeProfile>> GetAllEmployeeProfilesAsync();
        Task<bool> UpdateSalaryAsync(int employeeProfileId, decimal newSalary, string salaryType = "Base");
        Task<EmployeeFine?> IssueFineAsync(IssueFineRequest request, int issuedByUserId);
        Task<bool> PayFineAsync(int fineId, string paymentMethod);
        Task<EmployeePayment?> ProcessPaymentAsync(ProcessPaymentRequest request, int processedByUserId);
        Task<List<EmployeeFine>> GetEmployeeFinesAsync(int employeeProfileId);
        Task<List<EmployeePayment>> GetEmployeePaymentsAsync(int employeeProfileId);
        Task<decimal> GetEmployeeOutstandingFinesAsync(int employeeProfileId);
    }

    public class EmployeeManagementService : IEmployeeManagementService
    {
        private readonly ApplicationDbContext _context;

        public EmployeeManagementService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<EmployeeProfile?> CreateEmployeeProfileAsync(CreateEmployeeProfileRequest request)
        {
            try
            {
                // Check if user exists and doesn't already have a profile
                var user = await _context.Users.FindAsync(request.UserId);
                if (user == null)
                    return null;

                var existingProfile = await _context.EmployeeProfiles
                    .FirstOrDefaultAsync(ep => ep.UserId == request.UserId);
                if (existingProfile != null)
                    return existingProfile;

                // Generate employee number if not provided
                if (string.IsNullOrEmpty(request.EmployeeNumber))
                {
                    var lastEmployee = await _context.EmployeeProfiles
                        .OrderByDescending(ep => ep.EmployeeProfileId)
                        .FirstOrDefaultAsync();
                    
                    var nextNumber = (lastEmployee?.EmployeeProfileId ?? 0) + 1;
                    request.EmployeeNumber = $"EMP{nextNumber:D4}";
                }

                var profile = new EmployeeProfile
                {
                    UserId = request.UserId,
                    EmployeeNumber = request.EmployeeNumber,
                    Position = request.Position,
                    HireDate = request.HireDate,
                    BaseSalary = request.BaseSalary,
                    PaymentFrequency = request.PaymentFrequency,
                    BankAccount = request.BankAccount,
                    BankName = request.BankName,
                    EmergencyContact = request.EmergencyContact
                };

                _context.EmployeeProfiles.Add(profile);
                await _context.SaveChangesAsync();

                // Create initial salary record
                var salaryRecord = new EmployeeSalary
                {
                    EmployeeProfileId = profile.EmployeeProfileId,
                    Amount = request.BaseSalary,
                    SalaryType = "Base",
                    EffectiveDate = request.HireDate,
                    Notes = "Initial salary setup"
                };

                _context.EmployeeSalaries.Add(salaryRecord);
                await _context.SaveChangesAsync();

                return profile;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public async Task<EmployeeProfile?> GetEmployeeProfileAsync(int userId)
        {
            return await _context.EmployeeProfiles
                .Include(ep => ep.User)
                .Include(ep => ep.EmployeeSalaries.Where(sr => sr.IsActive))
                .Include(ep => ep.EmployeeFines.Where(f => f.Status != "Paid"))
                .Include(ep => ep.EmployeePayments)
                .FirstOrDefaultAsync(ep => ep.UserId == userId);
        }

        public async Task<List<EmployeeProfile>> GetAllEmployeeProfilesAsync()
        {
            return await _context.EmployeeProfiles
                .Include(ep => ep.User)
                .Include(ep => ep.EmployeeSalaries.Where(sr => sr.IsActive))
                .Include(ep => ep.EmployeeFines.Where(f => f.Status != "Paid"))
                .Where(ep => ep.EmploymentStatus == "Active")
                .OrderBy(ep => ep.EmployeeNumber)
                .ToListAsync();
        }

        public async Task<bool> UpdateSalaryAsync(int employeeProfileId, decimal newSalary, string salaryType = "Base")
        {
            try
            {
                var profile = await _context.EmployeeProfiles.FindAsync(employeeProfileId);
                if (profile == null)
                    return false;

                // Deactivate current salary records of the same type
                var currentSalaries = await _context.EmployeeSalaries
                    .Where(es => es.EmployeeProfileId == employeeProfileId && 
                                es.SalaryType == salaryType && 
                                es.IsActive)
                    .ToListAsync();

                foreach (var salary in currentSalaries)
                {
                    salary.IsActive = false;
                    salary.EndDate = DateTime.UtcNow;
                }

                // Create new salary record
                var newSalaryRecord = new EmployeeSalary
                {
                    EmployeeProfileId = employeeProfileId,
                    Amount = newSalary,
                    SalaryType = salaryType,
                    EffectiveDate = DateTime.UtcNow,
                    Notes = $"Salary updated from {profile.BaseSalary} to {newSalary}"
                };

                _context.EmployeeSalaries.Add(newSalaryRecord);

                // Update base salary in profile if it's a base salary change
                if (salaryType == "Base")
                {
                    profile.BaseSalary = newSalary;
                    profile.UpdatedAt = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<EmployeeFine?> IssueFineAsync(IssueFineRequest request, int issuedByUserId)
        {
            try
            {
                var profile = await _context.EmployeeProfiles.FindAsync(request.EmployeeProfileId);
                if (profile == null)
                    return null;

                var fine = new EmployeeFine
                {
                    EmployeeProfileId = request.EmployeeProfileId,
                    Reason = request.Reason,
                    Amount = request.Amount,
                    Description = request.Description,
                    IssuedByUserId = issuedByUserId,
                    Status = "Pending"
                };

                _context.EmployeeFines.Add(fine);
                await _context.SaveChangesAsync();

                return fine;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public async Task<bool> PayFineAsync(int fineId, string paymentMethod)
        {
            try
            {
                var fine = await _context.EmployeeFines.FindAsync(fineId);
                if (fine == null || fine.Status == "Paid")
                    return false;

                fine.Status = "Paid";
                fine.PaidDate = DateTime.UtcNow;
                fine.PaymentMethod = paymentMethod;

                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<EmployeePayment?> ProcessPaymentAsync(ProcessPaymentRequest request, int processedByUserId)
        {
            try
            {
                var profile = await _context.EmployeeProfiles.FindAsync(request.EmployeeProfileId);
                if (profile == null)
                    return null;

                // Generate payment number
                var paymentNumber = $"PAY-{DateTime.Now:yyyyMMdd}-{new Random().Next(1000, 9999)}";

                var payment = new EmployeePayment
                {
                    EmployeeProfileId = request.EmployeeProfileId,
                    PaymentNumber = paymentNumber,
                    GrossPay = request.GrossPay,
                    Deductions = request.Deductions,
                    NetPay = request.GrossPay - request.Deductions,
                    PaymentPeriod = request.PaymentPeriod,
                    PaymentMethod = request.PaymentMethod,
                    Notes = request.Notes,
                    ProcessedByUserId = processedByUserId,
                    Status = "Paid"
                };

                _context.EmployeePayments.Add(payment);
                await _context.SaveChangesAsync();

                return payment;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public async Task<List<EmployeeFine>> GetEmployeeFinesAsync(int employeeProfileId)
        {
            return await _context.EmployeeFines
                .Include(ef => ef.IssuedByUser)
                .Where(ef => ef.EmployeeProfileId == employeeProfileId)
                .OrderByDescending(ef => ef.IssuedDate)
                .ToListAsync();
        }

        public async Task<List<EmployeePayment>> GetEmployeePaymentsAsync(int employeeProfileId)
        {
            return await _context.EmployeePayments
                .Include(ep => ep.ProcessedByUser)
                .Where(ep => ep.EmployeeProfileId == employeeProfileId)
                .OrderByDescending(ep => ep.PaymentDate)
                .ToListAsync();
        }

        public async Task<decimal> GetEmployeeOutstandingFinesAsync(int employeeProfileId)
        {
            return await _context.EmployeeFines
                .Where(ef => ef.EmployeeProfileId == employeeProfileId && ef.Status != "Paid")
                .SumAsync(ef => ef.Amount);
        }
    }
}
