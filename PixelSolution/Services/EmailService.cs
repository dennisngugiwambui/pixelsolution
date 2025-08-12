using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using PixelSolution.Services.Interfaces;

namespace PixelSolution.Services
{
    public interface IEmailService
    {
        Task<bool> SendEmailAsync(string to, string subject, string body, bool isHtml = true);
        Task<bool> SendEmailWithAttachmentAsync(string to, string subject, string body, byte[] attachmentData, string attachmentName);
        Task<bool> SendReportEmailAsync(string to, string reportType, byte[] reportData, string fileName);
        Task<bool> SendReceiptEmailAsync(string to, int saleId, byte[] receiptData);
        Task<bool> SendBulkEmailAsync(List<string> recipients, string subject, string body);
    }

    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailService> _logger;
        private readonly string _smtpHost;
        private readonly int _smtpPort;
        private readonly string _smtpUsername;
        private readonly string _smtpPassword;
        private readonly string _fromEmail;
        private readonly string _fromName;
        private readonly bool _enableSsl;

        public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
        {
            _configuration = configuration;
            _logger = logger;

            // Load email configuration from appsettings.json
            _smtpHost = _configuration["EmailSettings:SmtpHost"] ?? "smtp.gmail.com";
            _smtpPort = int.Parse(_configuration["EmailSettings:SmtpPort"] ?? "587");
            _smtpUsername = _configuration["EmailSettings:SmtpUsername"] ?? "";
            _smtpPassword = _configuration["EmailSettings:SmtpPassword"] ?? "";
            _fromEmail = _configuration["EmailSettings:FromEmail"] ?? "noreply@pixelsolution.com";
            _fromName = _configuration["EmailSettings:FromName"] ?? "PixelSolution";
            _enableSsl = bool.Parse(_configuration["EmailSettings:EnableSsl"] ?? "true");
        }

        public async Task<bool> SendEmailAsync(string to, string subject, string body, bool isHtml = true)
        {
            try
            {
                using var mailMessage = new MailMessage
                {
                    From = new MailAddress(_fromEmail, _fromName),
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = isHtml
                };

                mailMessage.To.Add(new MailAddress(to));

                using var smtpClient = new SmtpClient(_smtpHost, _smtpPort)
                {
                    Credentials = new NetworkCredential(_smtpUsername, _smtpPassword),
                    EnableSsl = _enableSsl,
                    DeliveryMethod = SmtpDeliveryMethod.Network
                };

                await smtpClient.SendMailAsync(mailMessage);
                _logger.LogInformation("Email sent successfully to {To} with subject: {Subject}", to, subject);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending email to {To}", to);
                return false;
            }
        }

        public async Task<bool> SendEmailWithAttachmentAsync(string to, string subject, string body, byte[] attachmentData, string attachmentName)
        {
            try
            {
                using var mailMessage = new MailMessage
                {
                    From = new MailAddress(_fromEmail, _fromName),
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = true
                };

                mailMessage.To.Add(new MailAddress(to));

                // Add attachment
                using var stream = new MemoryStream(attachmentData);
                var attachment = new Attachment(stream, attachmentName);
                mailMessage.Attachments.Add(attachment);

                using var smtpClient = new SmtpClient(_smtpHost, _smtpPort)
                {
                    Credentials = new NetworkCredential(_smtpUsername, _smtpPassword),
                    EnableSsl = _enableSsl,
                    DeliveryMethod = SmtpDeliveryMethod.Network
                };

                await smtpClient.SendMailAsync(mailMessage);
                _logger.LogInformation("Email with attachment sent successfully to {To}", to);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending email with attachment to {To}", to);
                return false;
            }
        }

        public async Task<bool> SendReportEmailAsync(string to, string reportType, byte[] reportData, string fileName)
        {
            try
            {
                var subject = $"PixelSolution - {reportType} Report";
                var body = GenerateReportEmailBody(reportType);

                return await SendEmailWithAttachmentAsync(to, subject, body, reportData, fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending report email to {To}", to);
                return false;
            }
        }

        public async Task<bool> SendReceiptEmailAsync(string to, int saleId, byte[] receiptData)
        {
            try
            {
                var subject = $"PixelSolution - Sales Receipt #{saleId}";
                var body = GenerateReceiptEmailBody(saleId);

                var fileName = $"Receipt_{saleId}_{DateTime.Now:yyyyMMdd}.pdf";
                return await SendEmailWithAttachmentAsync(to, subject, body, receiptData, fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending receipt email to {To} for sale {SaleId}", to, saleId);
                return false;
            }
        }

        public async Task<bool> SendBulkEmailAsync(List<string> recipients, string subject, string body)
        {
            var successCount = 0;
            var tasks = recipients.Select(async recipient =>
            {
                var result = await SendEmailAsync(recipient, subject, body);
                if (result) Interlocked.Increment(ref successCount);
                return result;
            });

            await Task.WhenAll(tasks);

            _logger.LogInformation("Bulk email sent to {SuccessCount}/{TotalCount} recipients", successCount, recipients.Count);
            return successCount > 0;
        }

        private string GenerateReportEmailBody(string reportType)
        {
            return $@"
                <!DOCTYPE html>
                <html>
                <head>
                    <style>
                        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
                        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
                        .header {{ background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: white; padding: 30px; text-align: center; border-radius: 10px 10px 0 0; }}
                        .content {{ background: #f8f9fa; padding: 30px; border-radius: 0 0 10px 10px; }}
                        .footer {{ text-align: center; margin-top: 30px; color: #666; font-size: 12px; }}
                        .button {{ display: inline-block; padding: 12px 30px; background: #667eea; color: white; text-decoration: none; border-radius: 5px; margin-top: 20px; }}
                    </style>
                </head>
                <body>
                    <div class='container'>
                        <div class='header'>
                            <h1>PixelSolution</h1>
                            <p>{reportType} Report</p>
                        </div>
                        <div class='content'>
                            <h2>Your Report is Ready!</h2>
                            <p>Dear Valued Customer,</p>
                            <p>Please find attached your requested <strong>{reportType} Report</strong> generated on <strong>{DateTime.Now:dddd, MMMM dd, yyyy}</strong>.</p>
                            <p>This report contains comprehensive analytics and insights for your business operations.</p>
                            <p>If you have any questions or need further assistance, please don't hesitate to contact our support team.</p>
                            <p>Best regards,<br>The PixelSolution Team</p>
                        </div>
                        <div class='footer'>
                            <p>© {DateTime.Now.Year} PixelSolution. All rights reserved.</p>
                            <p>This is an automated email. Please do not reply directly to this message.</p>
                        </div>
                    </div>
                </body>
                </html>";
        }

        private string GenerateReceiptEmailBody(int saleId)
        {
            return $@"
                <!DOCTYPE html>
                <html>
                <head>
                    <style>
                        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
                        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
                        .header {{ background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: white; padding: 30px; text-align: center; border-radius: 10px 10px 0 0; }}
                        .content {{ background: #f8f9fa; padding: 30px; border-radius: 0 0 10px 10px; }}
                        .footer {{ text-align: center; margin-top: 30px; color: #666; font-size: 12px; }}
                        .receipt-info {{ background: white; padding: 20px; border-radius: 5px; margin: 20px 0; }}
                    </style>
                </head>
                <body>
                    <div class='container'>
                        <div class='header'>
                            <h1>PixelSolution</h1>
                            <p>Sales Receipt</p>
                        </div>
                        <div class='content'>
                            <h2>Thank You for Your Purchase!</h2>
                            <p>Dear Customer,</p>
                            <p>Please find attached your sales receipt for transaction <strong>#{saleId}</strong>.</p>
                            <div class='receipt-info'>
                                <p><strong>Transaction Date:</strong> {DateTime.Now:dddd, MMMM dd, yyyy}</p>
                                <p><strong>Receipt Number:</strong> #{saleId}</p>
                            </div>
                            <p>We appreciate your business and look forward to serving you again.</p>
                            <p>If you have any questions about your purchase, please contact our customer service team.</p>
                            <p>Best regards,<br>The PixelSolution Team</p>
                        </div>
                        <div class='footer'>
                            <p>© {DateTime.Now.Year} PixelSolution. All rights reserved.</p>
                            <p>Please keep this receipt for your records.</p>
                        </div>
                    </div>
                </body>
                </html>";
        }
    }
}