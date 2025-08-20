using Microsoft.EntityFrameworkCore;
using PixelSolution.Data;
using PixelSolution.Models;

namespace PixelSolution.Services
{
    public interface IPaymentReminderService
    {
        Task SendMonthlyPaymentRemindersAsync();
        Task SendPaymentReminderAsync(int employeeProfileId);
        Task SchedulePaymentRemindersAsync();
        Task SendPaymentReminderToAdminAsync(string employeeName, string employeeId, DateTime hireDate, decimal currentSalary);
    }

    public class PaymentReminderService : IPaymentReminderService
    {
        private readonly ApplicationDbContext _context;
        private readonly IEmailService _emailService;
        private readonly ILogger<PaymentReminderService> _logger;

        public PaymentReminderService(
            ApplicationDbContext context,
            IEmailService emailService,
            ILogger<PaymentReminderService> logger)
        {
            _context = context;
            _emailService = emailService;
            _logger = logger;
        }

        public async Task SendMonthlyPaymentRemindersAsync()
        {
            try
            {
                var today = DateTime.UtcNow.Date;
                var employeeProfiles = await _context.EmployeeProfiles
                    .Include(ep => ep.User)
                    .Where(ep => ep.EmploymentStatus == "Active")
                    .ToListAsync();

                foreach (var profile in employeeProfiles)
                {
                    var hireDate = profile.HireDate.Date;
                    var dayOfMonth = hireDate.Day;
                    
                    // Check if today is the monthly payment reminder date
                    if (today.Day == dayOfMonth || (dayOfMonth > 28 && today.Day == DateTime.DaysInMonth(today.Year, today.Month)))
                    {
                        await SendPaymentReminderAsync(profile.EmployeeProfileId);
                    }
                }

                _logger.LogInformation($"Monthly payment reminders processed for {DateTime.UtcNow:yyyy-MM-dd}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending monthly payment reminders");
            }
        }

        public async Task SendPaymentReminderAsync(int employeeProfileId)
        {
            try
            {
                var profile = await _context.EmployeeProfiles
                    .Include(ep => ep.User)
                    .Include(ep => ep.SalaryRecords.Where(sr => sr.IsActive))
                    .Include(ep => ep.Payments.OrderByDescending(p => p.PaymentDate).Take(1))
                    .FirstOrDefaultAsync(ep => ep.EmployeeProfileId == employeeProfileId);

                if (profile == null) return;

                var currentSalary = profile.SalaryRecords.FirstOrDefault(sr => sr.IsActive && sr.SalaryType == "Base");
                var lastPayment = profile.Payments.FirstOrDefault();

                // Send Email Reminder
                await SendEmailReminder(profile, currentSalary, lastPayment);

                // Send Internal Message Reminder
                await SendInternalMessageReminder(profile, currentSalary, lastPayment);

                _logger.LogInformation($"Payment reminder sent for employee {profile.User.FullName}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error sending payment reminder for employee {employeeProfileId}");
            }
        }

        private async Task SendEmailReminder(EmployeeProfile profile, EmployeeSalary? currentSalary, EmployeePayment? lastPayment)
        {
            var subject = $"Monthly Salary Payment Reminder - {profile.User.FullName}";
            var htmlBody = GeneratePaymentReminderEmailTemplate(profile, currentSalary, lastPayment);

            await _emailService.SendEmailAsync(
                "info@pixelsolution.com",
                subject,
                htmlBody,
                isHtml: true
            );
        }

        private async Task SendInternalMessageReminder(EmployeeProfile profile, EmployeeSalary? currentSalary, EmployeePayment? lastPayment)
        {
            // Internal message reminder functionality removed for now
            // Will be implemented when messaging system is ready
            _logger.LogInformation($"Internal message reminder would be sent for employee {profile.User.FullName}");
        }

        private string GeneratePaymentReminderEmailTemplate(EmployeeProfile profile, EmployeeSalary? currentSalary, EmployeePayment? lastPayment)
        {
            var salaryAmount = currentSalary?.Amount ?? 0;
            var lastPaymentDate = lastPayment?.PaymentDate.ToString("MMMM dd, yyyy") ?? "No previous payments";
            var currentMonth = DateTime.UtcNow.ToString("MMMM yyyy");

            return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Payment Reminder - PixelSolution</title>
    <style>
        body {{ font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; margin: 0; padding: 0; background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); }}
        .container {{ max-width: 600px; margin: 40px auto; background: white; border-radius: 16px; overflow: hidden; box-shadow: 0 20px 60px rgba(0,0,0,0.3); }}
        .header {{ background: linear-gradient(135deg, #667eea, #764ba2); padding: 40px 30px; text-align: center; }}
        .logo {{ color: white; font-size: 28px; font-weight: 700; margin-bottom: 10px; }}
        .header-subtitle {{ color: rgba(255,255,255,0.9); font-size: 16px; }}
        .content {{ padding: 40px 30px; }}
        .greeting {{ font-size: 24px; font-weight: 600; color: #1e293b; margin-bottom: 20px; }}
        .message {{ font-size: 16px; line-height: 1.6; color: #475569; margin-bottom: 30px; }}
        .payment-card {{ background: linear-gradient(135deg, #f0f9ff, #e0f2fe); border-radius: 12px; padding: 25px; margin: 25px 0; border-left: 4px solid #0ea5e9; }}
        .payment-title {{ font-size: 18px; font-weight: 600; color: #0c4a6e; margin-bottom: 15px; }}
        .payment-details {{ display: grid; grid-template-columns: 1fr 1fr; gap: 15px; }}
        .detail-item {{ }}
        .detail-label {{ font-size: 12px; color: #64748b; text-transform: uppercase; letter-spacing: 0.5px; margin-bottom: 5px; }}
        .detail-value {{ font-size: 16px; font-weight: 600; color: #1e293b; }}
        .salary-amount {{ font-size: 24px; font-weight: 700; color: #0c4a6e; }}
        .cta-section {{ text-align: center; margin: 30px 0; }}
        .cta-button {{ display: inline-block; background: linear-gradient(135deg, #667eea, #764ba2); color: white; padding: 15px 30px; text-decoration: none; border-radius: 8px; font-weight: 600; transition: transform 0.3s ease; }}
        .cta-button:hover {{ transform: translateY(-2px); }}
        .footer {{ background: #f8fafc; padding: 30px; text-align: center; border-top: 1px solid #e2e8f0; }}
        .footer-text {{ color: #64748b; font-size: 14px; line-height: 1.5; }}
        .company-info {{ margin-top: 20px; padding-top: 20px; border-top: 1px solid #e2e8f0; }}
        .social-links {{ margin: 20px 0; }}
        .social-links a {{ display: inline-block; margin: 0 10px; color: #667eea; text-decoration: none; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <div class='logo'>⚡ PixelSolution</div>
            <div class='header-subtitle'>Employee Payment Management System</div>
        </div>
        
        <div class='content'>
            <div class='greeting'>Monthly Payment Reminder</div>
            
            <div class='message'>
                This is a friendly reminder that it's time to process the monthly salary payment for <strong>{profile.User.FullName}</strong>.
            </div>
            
            <div class='payment-card'>
                <div class='payment-title'>💰 Payment Details</div>
                <div class='payment-details'>
                    <div class='detail-item'>
                        <div class='detail-label'>Employee Name</div>
                        <div class='detail-value'>{profile.User.FullName}</div>
                    </div>
                    <div class='detail-item'>
                        <div class='detail-label'>Employee ID</div>
                        <div class='detail-value'>{profile.EmployeeNumber}</div>
                    </div>
                    <div class='detail-item'>
                        <div class='detail-label'>Position</div>
                        <div class='detail-value'>{profile.Position}</div>
                    </div>
                    <div class='detail-item'>
                        <div class='detail-label'>Payment Period</div>
                        <div class='detail-value'>{currentMonth}</div>
                    </div>
                    <div class='detail-item'>
                        <div class='detail-label'>Monthly Salary</div>
                        <div class='detail-value salary-amount'>KSh {salaryAmount:N2}</div>
                    </div>
                    <div class='detail-item'>
                        <div class='detail-label'>Last Payment</div>
                        <div class='detail-value'>{lastPaymentDate}</div>
                    </div>
                </div>
            </div>
            
            <div class='cta-section'>
                <a href='https://localhost:5001/Admin/Users' class='cta-button'>
                    🚀 Process Payment Now
                </a>
            </div>
            
            <div class='message'>
                <strong>Important:</strong> Please ensure all salary payments are processed on time to maintain employee satisfaction and comply with employment regulations.
            </div>
        </div>
        
        <div class='footer'>
            <div class='footer-text'>
                <strong>PixelSolution Ltd</strong><br>
                Employee Management System<br>
                Nairobi, Kenya
            </div>
            
            <div class='social-links'>
                <a href='#'>📧 info@pixelsolution.com</a>
                <a href='#'>📞 +254742282250</a>
            </div>
            
            <div class='company-info'>
                <div style='color: #9ca3af; font-size: 12px;'>
                    This is an automated reminder from PixelSolution Employee Management System.<br>
                    Generated on {DateTime.UtcNow:MMMM dd, yyyy} at {DateTime.UtcNow:HH:mm} UTC
                </div>
            </div>
        </div>
    </div>
</body>
</html>";
        }

        public async Task SchedulePaymentRemindersAsync()
        {
            // This would typically be called by a background service or scheduled job
            // For now, we'll implement a simple check that can be called periodically
            await SendMonthlyPaymentRemindersAsync();
        }

        public async Task SendPaymentReminderToAdminAsync(string employeeName, string employeeId, DateTime hireDate, decimal currentSalary)
        {
            try
            {
                var subject = $"Monthly Salary Payment Reminder - {employeeName}";
                var htmlBody = GenerateSimplePaymentReminderTemplate(employeeName, employeeId, hireDate, currentSalary);

                await _emailService.SendEmailAsync(
                    "info@pixelsolution.com",
                    subject,
                    htmlBody,
                    isHtml: true
                );

                _logger.LogInformation($"Payment reminder sent for employee {employeeName}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error sending payment reminder for employee {employeeName}");
            }
        }

        private string GenerateSimplePaymentReminderTemplate(string employeeName, string employeeId, DateTime hireDate, decimal currentSalary)
        {
            var currentMonth = DateTime.UtcNow.ToString("MMMM yyyy");

            return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Payment Reminder - PixelSolution</title>
    <style>
        body {{ font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; margin: 0; padding: 0; background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); }}
        .container {{ max-width: 600px; margin: 40px auto; background: white; border-radius: 16px; overflow: hidden; box-shadow: 0 20px 60px rgba(0,0,0,0.3); }}
        .header {{ background: linear-gradient(135deg, #667eea, #764ba2); padding: 40px 30px; text-align: center; }}
        .logo {{ color: white; font-size: 28px; font-weight: 700; margin-bottom: 10px; }}
        .header-subtitle {{ color: rgba(255,255,255,0.9); font-size: 16px; }}
        .content {{ padding: 40px 30px; }}
        .greeting {{ font-size: 24px; font-weight: 600; color: #1e293b; margin-bottom: 20px; }}
        .message {{ font-size: 16px; line-height: 1.6; color: #475569; margin-bottom: 30px; }}
        .payment-card {{ background: linear-gradient(135deg, #f0f9ff, #e0f2fe); border-radius: 12px; padding: 25px; margin: 25px 0; border-left: 4px solid #0ea5e9; }}
        .payment-title {{ font-size: 18px; font-weight: 600; color: #0c4a6e; margin-bottom: 15px; }}
        .payment-details {{ display: grid; grid-template-columns: 1fr 1fr; gap: 15px; }}
        .detail-item {{ }}
        .detail-label {{ font-size: 12px; color: #64748b; text-transform: uppercase; letter-spacing: 0.5px; margin-bottom: 5px; }}
        .detail-value {{ font-size: 16px; font-weight: 600; color: #1e293b; }}
        .salary-amount {{ font-size: 24px; font-weight: 700; color: #0c4a6e; }}
        .cta-section {{ text-align: center; margin: 30px 0; }}
        .cta-button {{ display: inline-block; background: linear-gradient(135deg, #667eea, #764ba2); color: white; padding: 15px 30px; text-decoration: none; border-radius: 8px; font-weight: 600; transition: transform 0.3s ease; }}
        .cta-button:hover {{ transform: translateY(-2px); }}
        .footer {{ background: #f8fafc; padding: 30px; text-align: center; border-top: 1px solid #e2e8f0; }}
        .footer-text {{ color: #64748b; font-size: 14px; line-height: 1.5; }}
        .company-info {{ margin-top: 20px; padding-top: 20px; border-top: 1px solid #e2e8f0; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <div class='logo'>⚡ PixelSolution</div>
            <div class='header-subtitle'>Employee Payment Management System</div>
        </div>
        
        <div class='content'>
            <div class='greeting'>Monthly Payment Reminder</div>
            
            <div class='message'>
                This is a friendly reminder that it's time to process the monthly salary payment for <strong>{employeeName}</strong>.
            </div>
            
            <div class='payment-card'>
                <div class='payment-title'>💰 Payment Details</div>
                <div class='payment-details'>
                    <div class='detail-item'>
                        <div class='detail-label'>Employee Name</div>
                        <div class='detail-value'>{employeeName}</div>
                    </div>
                    <div class='detail-item'>
                        <div class='detail-label'>Employee ID</div>
                        <div class='detail-value'>{employeeId}</div>
                    </div>
                    <div class='detail-item'>
                        <div class='detail-label'>Hire Date</div>
                        <div class='detail-value'>{hireDate:MMM dd, yyyy}</div>
                    </div>
                    <div class='detail-item'>
                        <div class='detail-label'>Payment Period</div>
                        <div class='detail-value'>{currentMonth}</div>
                    </div>
                    <div class='detail-item' style='grid-column: span 2;'>
                        <div class='detail-label'>Monthly Salary</div>
                        <div class='detail-value salary-amount'>KSh {currentSalary:N2}</div>
                    </div>
                </div>
            </div>
            
            <div class='cta-section'>
                <a href='https://localhost:5001/Admin/Users' class='cta-button'>
                    🚀 Process Payment Now
                </a>
            </div>
            
            <div class='message'>
                <strong>Important:</strong> Please ensure all salary payments are processed on time to maintain employee satisfaction and comply with employment regulations.
            </div>
        </div>
        
        <div class='footer'>
            <div class='footer-text'>
                <strong>PixelSolution Ltd</strong><br>
                Employee Management System<br>
                Nairobi, Kenya
            </div>
            
            <div class='company-info'>
                <div style='color: #9ca3af; font-size: 12px;'>
                    This is an automated reminder from PixelSolution Employee Management System.<br>
                    Generated on {DateTime.UtcNow:MMMM dd, yyyy} at {DateTime.UtcNow:HH:mm} UTC
                </div>
            </div>
        </div>
    </div>
</body>
</html>";
        }
    }
}
