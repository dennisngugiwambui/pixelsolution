using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PixelSolution.Services;

namespace PixelSolution.Services
{
    public interface IEnhancedEmailService
    {
        Task<bool> SendEmailAsync(string to, string subject, string body, bool isHtml = true);
        Task<bool> SendEmailWithAttachmentAsync(string to, string subject, string body, byte[] attachmentData, string attachmentName);
        Task<bool> SendEmployeeEmailAsync(string to, string subject, string templateType, Dictionary<string, string> templateData);
        Task<bool> SendFancyEmployeeEmailAsync(string to, string employeeName, string templateType, Dictionary<string, string> data);
    }

    public class EnhancedEmailService : IEnhancedEmailService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<EnhancedEmailService> _logger;
        private readonly string _smtpHost;
        private readonly int _smtpPort;
        private readonly string _smtpUsername;
        private readonly string _smtpPassword;
        private readonly string _fromEmail;
        private readonly string _fromName;
        private readonly bool _enableSsl;

        public EnhancedEmailService(IConfiguration configuration, ILogger<EnhancedEmailService> logger)
        {
            _configuration = configuration;
            _logger = logger;

            // Load email settings from configuration
            _smtpHost = _configuration["EmailSettings:SmtpServer"] ?? "smtp.gmail.com";
            _smtpPort = int.Parse(_configuration["EmailSettings:SmtpPort"] ?? "587");
            _smtpUsername = _configuration["EmailSettings:SmtpUsername"] ?? "";
            _smtpPassword = _configuration["EmailSettings:SmtpPassword"] ?? "";
            _fromEmail = _configuration["EmailSettings:FromEmail"] ?? "";
            _fromName = _configuration["EmailSettings:FromName"] ?? "PixelSolution";
            _enableSsl = bool.Parse(_configuration["EmailSettings:EnableSsl"] ?? "true");

            _logger.LogInformation($"EnhancedEmailService initialized with SMTP: {_smtpHost}:{_smtpPort}, From: {_fromEmail}, SSL: {_enableSsl}");
        }

        public async Task<bool> SendEmailAsync(string to, string subject, string body, bool isHtml = true)
        {
            try
            {
                _logger.LogInformation($"Attempting to send email to: {to}, Subject: {subject}");
                _logger.LogInformation($"SMTP Config - Host: {_smtpHost}, Port: {_smtpPort}, Username: {_smtpUsername}, FromEmail: {_fromEmail}, SSL: {_enableSsl}");
                
                // Validate email configuration
                if (string.IsNullOrEmpty(_smtpUsername) || string.IsNullOrEmpty(_smtpPassword))
                {
                    _logger.LogError("SMTP credentials are missing. Username or password is empty.");
                    return false;
                }

                if (string.IsNullOrEmpty(_fromEmail))
                {
                    _logger.LogError("FromEmail is not configured.");
                    return false;
                }

                using var client = new SmtpClient(_smtpHost, _smtpPort);
                client.Credentials = new NetworkCredential(_smtpUsername, _smtpPassword);
                client.EnableSsl = _enableSsl;
                client.Timeout = 30000; // 30 seconds timeout
                
                // Add event handlers for better debugging
                client.SendCompleted += (sender, e) =>
                {
                    if (e.Error != null)
                    {
                        _logger.LogError(e.Error, "SMTP SendCompleted event reported error");
                    }
                    else if (e.Cancelled)
                    {
                        _logger.LogWarning("Email sending was cancelled");
                    }
                    else
                    {
                        _logger.LogInformation("SMTP SendCompleted event: Email sent successfully");
                    }
                };

                using var message = new MailMessage();
                message.From = new MailAddress(_fromEmail, _fromName);
                message.To.Add(to);
                message.Subject = subject;
                message.Body = body;
                message.IsBodyHtml = isHtml;

                _logger.LogInformation($"Sending email via SMTP...");
                await client.SendMailAsync(message);
                _logger.LogInformation($"✅ Email sent successfully to: {to}");
                return true;
            }
            catch (SmtpException smtpEx)
            {
                _logger.LogError(smtpEx, $"SMTP Error sending email to: {to}. StatusCode: {smtpEx.StatusCode}, Message: {smtpEx.Message}");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"General error sending email to: {to}. Error: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> SendEmailWithAttachmentAsync(string to, string subject, string body, byte[] attachmentData, string attachmentName)
        {
            try
            {
                _logger.LogInformation($"Attempting to send email with attachment to: {to}");
                
                using var client = new SmtpClient(_smtpHost, _smtpPort);
                client.Credentials = new NetworkCredential(_smtpUsername, _smtpPassword);
                client.EnableSsl = _enableSsl;
                client.Timeout = 30000;

                using var message = new MailMessage();
                message.From = new MailAddress(_fromEmail, _fromName);
                message.To.Add(to);
                message.Subject = subject;
                message.Body = body;
                message.IsBodyHtml = true;

                // Add attachment
                using var stream = new MemoryStream(attachmentData);
                var attachment = new Attachment(stream, attachmentName);
                message.Attachments.Add(attachment);

                await client.SendMailAsync(message);
                _logger.LogInformation($"Email with attachment sent successfully to: {to}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to send email with attachment to: {to}. Error: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> SendEmployeeEmailAsync(string to, string subject, string templateType, Dictionary<string, string> templateData)
        {
            var htmlBody = GenerateEmailTemplate(templateType, templateData);
            return await SendEmailAsync(to, subject, htmlBody, true);
        }

        public async Task<bool> SendFancyEmployeeEmailAsync(string to, string employeeName, string templateType, Dictionary<string, string> data)
        {
            var subject = GetEmailSubject(templateType, employeeName);
            var htmlBody = GenerateFancyEmailTemplate(templateType, employeeName, data);
            
            _logger.LogInformation($"Sending fancy email to {to} with template {templateType}");
            return await SendEmailAsync(to, subject, htmlBody, true);
        }

        private string GetEmailSubject(string templateType, string employeeName)
        {
            return templateType switch
            {
                "welcome" => $"Welcome to PixelSolution, {employeeName}!",
                "payment_delay" => "Important: Payment Delay Notification",
                "termination" => "Employment Termination Notice",
                "salary_update" => "Salary Update Notification",
                "fine_issued" => "Fine Issued - Action Required",
                _ => "PixelSolution Notification"
            };
        }

        private string GenerateEmailTemplate(string templateType, Dictionary<string, string> data)
        {
            return templateType switch
            {
                "welcome" => GenerateWelcomeTemplate(data),
                "payment_delay" => GeneratePaymentDelayTemplate(data),
                "termination" => GenerateTerminationTemplate(data),
                "salary_update" => GenerateSalaryUpdateTemplate(data),
                "fine_issued" => GenerateFineIssuedTemplate(data),
                _ => GenerateDefaultTemplate(data)
            };
        }

        private string GenerateFancyEmailTemplate(string templateType, string employeeName, Dictionary<string, string> data)
        {
            var baseTemplate = GetFancyEmailBase();
            var content = templateType switch
            {
                "welcome" => GenerateFancyWelcomeContent(employeeName, data),
                "payment_delay" => GenerateFancyPaymentDelayContent(employeeName, data),
                "termination" => GenerateFancyTerminationContent(employeeName, data),
                "salary_update" => GenerateFancySalaryUpdateContent(employeeName, data),
                "fine_issued" => GenerateFancyFineIssuedContent(employeeName, data),
                _ => GenerateFancyDefaultContent(employeeName, data)
            };

            return baseTemplate.Replace("{{CONTENT}}", content);
        }

        private string GetFancyEmailBase()
        {
            return @"
<!DOCTYPE html>
<html lang='en'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>PixelSolution Notification</title>
    <style>
        * { margin: 0; padding: 0; box-sizing: border-box; }
        body { font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); margin: 0; padding: 20px; }
        .email-container { max-width: 600px; margin: 0 auto; background: white; border-radius: 16px; overflow: hidden; box-shadow: 0 20px 40px rgba(0,0,0,0.1); }
        .header { background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); padding: 30px; text-align: center; color: white; }
        .logo { font-size: 28px; font-weight: bold; margin-bottom: 10px; }
        .company-name { font-size: 16px; opacity: 0.9; }
        .content { padding: 40px 30px; }
        .card { background: #f8fafc; border-radius: 12px; padding: 25px; margin: 20px 0; border-left: 4px solid #667eea; }
        .card-title { font-size: 18px; font-weight: 600; color: #2d3748; margin-bottom: 15px; }
        .card-content { color: #4a5568; line-height: 1.6; }
        .highlight { background: linear-gradient(135deg, #667eea, #764ba2); color: white; padding: 15px; border-radius: 8px; margin: 15px 0; text-align: center; }
        .button { display: inline-block; background: linear-gradient(135deg, #667eea, #764ba2); color: white; padding: 12px 24px; border-radius: 8px; text-decoration: none; font-weight: 600; margin: 10px 0; }
        .footer { background: #2d3748; color: white; padding: 25px; text-align: center; }
        .footer-links { margin-top: 15px; }
        .footer-link { color: #a0aec0; text-decoration: none; margin: 0 10px; }
        .social-icons { margin-top: 15px; }
        .social-icon { display: inline-block; width: 40px; height: 40px; background: #4a5568; border-radius: 50%; margin: 0 5px; line-height: 40px; text-align: center; color: white; text-decoration: none; }
        @media (max-width: 600px) {
            .email-container { margin: 10px; }
            .content { padding: 20px 15px; }
            .header { padding: 20px 15px; }
        }
    </style>
</head>
<body>
    <div class='email-container'>
        <div class='header'>
            <div class='logo'>🎯 PixelSolution</div>
            <div class='company-name'>Sales Management System</div>
        </div>
        <div class='content'>
            {{CONTENT}}
        </div>
        <div class='footer'>
            <p><strong>PixelSolution Ltd</strong></p>
            <p>Nairobi, Kenya | +254742282250</p>
            <div class='footer-links'>
                <a href='#' class='footer-link'>Privacy Policy</a>
                <a href='#' class='footer-link'>Terms of Service</a>
                <a href='#' class='footer-link'>Contact Support</a>
            </div>
            <div class='social-icons'>
                <a href='#' class='social-icon'>📧</a>
                <a href='#' class='social-icon'>📱</a>
                <a href='#' class='social-icon'>🌐</a>
            </div>
            <p style='margin-top: 15px; font-size: 12px; opacity: 0.7;'>
                This email was sent automatically by PixelSolution. Please do not reply to this email.
            </p>
        </div>
    </div>
</body>
</html>";
        }

        private string GenerateFancyWelcomeContent(string employeeName, Dictionary<string, string> data)
        {
            return $@"
            <h1 style='color: #2d3748; margin-bottom: 20px;'>Welcome to the Team, {employeeName}! 🎉</h1>
            <p style='color: #4a5568; font-size: 16px; line-height: 1.6; margin-bottom: 20px;'>
                We are thrilled to have you join our PixelSolution family! Your journey with us starts today, and we're excited to see the amazing contributions you'll make.
            </p>
            
            <div class='card'>
                <div class='card-title'>📋 Your Employee Information</div>
                <div class='card-content'>
                    <p><strong>Position:</strong> {data.GetValueOrDefault("Position", "Not specified")}</p>
                    <p><strong>Start Date:</strong> {data.GetValueOrDefault("HireDate", DateTime.Now.ToString("MMMM dd, yyyy"))}</p>
                    <p><strong>Department:</strong> {data.GetValueOrDefault("Department", "General")}</p>
                    <p><strong>Employee ID:</strong> {data.GetValueOrDefault("EmployeeNumber", "TBD")}</p>
                </div>
            </div>

            <div class='highlight'>
                <h3>🚀 Ready to Get Started?</h3>
                <p>Your manager will contact you soon with your first assignments and orientation schedule.</p>
            </div>

            <div class='card'>
                <div class='card-title'>📞 Need Help?</div>
                <div class='card-content'>
                    <p>If you have any questions, don't hesitate to reach out to HR or your direct supervisor. We're here to help you succeed!</p>
                </div>
            </div>";
        }

        private string GenerateFancySalaryUpdateContent(string employeeName, Dictionary<string, string> data)
        {
            return $@"
            <h1 style='color: #2d3748; margin-bottom: 20px;'>Salary Update Notification 💰</h1>
            <p style='color: #4a5568; font-size: 16px; line-height: 1.6; margin-bottom: 20px;'>
                Dear {employeeName}, we're pleased to inform you about an update to your salary structure.
            </p>
            
            <div class='highlight'>
                <h3>💵 New Salary Details</h3>
                <p style='font-size: 18px; margin: 10px 0;'><strong>Amount:</strong> KSh {data.GetValueOrDefault("Amount", "0"):N2}</p>
                <p><strong>Type:</strong> {data.GetValueOrDefault("SalaryType", "Base Salary")}</p>
                <p><strong>Effective Date:</strong> {data.GetValueOrDefault("EffectiveDate", DateTime.Now.ToString("MMMM dd, yyyy"))}</p>
            </div>

            <div class='card'>
                <div class='card-title'>📝 Additional Notes</div>
                <div class='card-content'>
                    <p>{data.GetValueOrDefault("Notes", "No additional notes provided.")}</p>
                </div>
            </div>

            <div class='card'>
                <div class='card-title'>📋 What's Next?</div>
                <div class='card-content'>
                    <p>This change will be reflected in your next payroll cycle. If you have any questions about this update, please contact HR or your supervisor.</p>
                </div>
            </div>";
        }

        private string GenerateFancyFineIssuedContent(string employeeName, Dictionary<string, string> data)
        {
            return $@"
            <h1 style='color: #dc2626; margin-bottom: 20px;'>Fine Issued - Action Required ⚠️</h1>
            <p style='color: #4a5568; font-size: 16px; line-height: 1.6; margin-bottom: 20px;'>
                Dear {employeeName}, this is to inform you that a fine has been issued to your account.
            </p>
            
            <div class='card' style='border-left-color: #dc2626;'>
                <div class='card-title' style='color: #dc2626;'>💳 Fine Details</div>
                <div class='card-content'>
                    <p><strong>Amount:</strong> KSh {data.GetValueOrDefault("Amount", "0"):N2}</p>
                    <p><strong>Reason:</strong> {data.GetValueOrDefault("Reason", "Not specified")}</p>
                    <p><strong>Due Date:</strong> {data.GetValueOrDefault("DueDate", DateTime.Now.AddDays(30).ToString("MMMM dd, yyyy"))}</p>
                    <p><strong>Description:</strong> {data.GetValueOrDefault("Description", "No additional details provided.")}</p>
                </div>
            </div>

            <div class='highlight' style='background: linear-gradient(135deg, #dc2626, #b91c1c);'>
                <h3>📅 Payment Due</h3>
                <p>Please ensure this fine is paid by the due date to avoid additional penalties.</p>
            </div>

            <div class='card'>
                <div class='card-title'>📞 Questions or Concerns?</div>
                <div class='card-content'>
                    <p>If you believe this fine was issued in error or have questions about the details, please contact HR immediately.</p>
                </div>
            </div>";
        }

        private string GenerateFancyPaymentDelayContent(string employeeName, Dictionary<string, string> data)
        {
            return $@"
            <h1 style='color: #f59e0b; margin-bottom: 20px;'>Payment Delay Notification 🕒</h1>
            <p style='color: #4a5568; font-size: 16px; line-height: 1.6; margin-bottom: 20px;'>
                Dear {employeeName}, we sincerely apologize for the inconvenience, but we need to inform you of a delay in salary payments.
            </p>
            
            <div class='card' style='border-left-color: #f59e0b;'>
                <div class='card-title' style='color: #f59e0b;'>⏰ Delay Information</div>
                <div class='card-content'>
                    <p><strong>Expected Delay:</strong> {data.GetValueOrDefault("DelayDuration", "3-5 business days")}</p>
                    <p><strong>Reason:</strong> {data.GetValueOrDefault("Reason", "Unavoidable circumstances beyond our control")}</p>
                    <p><strong>New Expected Date:</strong> {data.GetValueOrDefault("NewPaymentDate", DateTime.Now.AddDays(5).ToString("MMMM dd, yyyy"))}</p>
                </div>
            </div>

            <div class='highlight' style='background: linear-gradient(135deg, #f59e0b, #d97706);'>
                <h3>🙏 Our Sincere Apologies</h3>
                <p>We understand this may cause inconvenience and we're working hard to resolve this as quickly as possible.</p>
            </div>

            <div class='card'>
                <div class='card-title'>💬 Need Assistance?</div>
                <div class='card-content'>
                    <p>If this delay causes significant hardship, please reach out to HR to discuss possible emergency assistance options.</p>
                </div>
            </div>";
        }

        private string GenerateFancyTerminationContent(string employeeName, Dictionary<string, string> data)
        {
            return $@"
            <h1 style='color: #dc2626; margin-bottom: 20px;'>Employment Termination Notice</h1>
            <p style='color: #4a5568; font-size: 16px; line-height: 1.6; margin-bottom: 20px;'>
                Dear {employeeName}, we regret to inform you that your employment with PixelSolution will be terminated.
            </p>
            
            <div class='card' style='border-left-color: #dc2626;'>
                <div class='card-title' style='color: #dc2626;'>📋 Termination Details</div>
                <div class='card-content'>
                    <p><strong>Effective Date:</strong> {data.GetValueOrDefault("TerminationDate", DateTime.Now.AddDays(30).ToString("MMMM dd, yyyy"))}</p>
                    <p><strong>Reason:</strong> {data.GetValueOrDefault("Reason", "As per company policy")}</p>
                    <p><strong>Final Working Day:</strong> {data.GetValueOrDefault("LastWorkingDay", DateTime.Now.AddDays(14).ToString("MMMM dd, yyyy"))}</p>
                </div>
            </div>

            <div class='card'>
                <div class='card-title'>📞 Appeal Process</div>
                <div class='card-content'>
                    <p>If you wish to appeal this decision, you have 14 days from the date of this notice to submit a formal appeal to HR.</p>
                </div>
            </div>";
        }

        private string GenerateFancyDefaultContent(string employeeName, Dictionary<string, string> data)
        {
            return $@"
            <h1 style='color: #2d3748; margin-bottom: 20px;'>Important Notification</h1>
            <p style='color: #4a5568; font-size: 16px; line-height: 1.6; margin-bottom: 20px;'>
                Dear {employeeName}, we have an important update for you.
            </p>
            
            <div class='card'>
                <div class='card-title'>📋 Message Details</div>
                <div class='card-content'>
                    <p>{data.GetValueOrDefault("Message", "Please contact HR for more information.")}</p>
                </div>
            </div>";
        }

        // Legacy template methods for backward compatibility
        private string GenerateWelcomeTemplate(Dictionary<string, string> data) => GenerateFancyWelcomeContent(data.GetValueOrDefault("EmployeeName", "Employee"), data);
        private string GeneratePaymentDelayTemplate(Dictionary<string, string> data) => GenerateFancyPaymentDelayContent(data.GetValueOrDefault("EmployeeName", "Employee"), data);
        private string GenerateTerminationTemplate(Dictionary<string, string> data) => GenerateFancyTerminationContent(data.GetValueOrDefault("EmployeeName", "Employee"), data);
        private string GenerateSalaryUpdateTemplate(Dictionary<string, string> data) => GenerateFancySalaryUpdateContent(data.GetValueOrDefault("EmployeeName", "Employee"), data);
        private string GenerateFineIssuedTemplate(Dictionary<string, string> data) => GenerateFancyFineIssuedContent(data.GetValueOrDefault("EmployeeName", "Employee"), data);
        private string GenerateDefaultTemplate(Dictionary<string, string> data) => GenerateFancyDefaultContent(data.GetValueOrDefault("EmployeeName", "Employee"), data);
    }
}
