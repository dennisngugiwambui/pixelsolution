using Microsoft.EntityFrameworkCore;
using PixelSolution.Data;
using PixelSolution.Models;
using System.Text.RegularExpressions;

namespace PixelSolution.Services
{
    public interface IQRCodePaymentService
    {
        Task<QRCodePayment> CreateQRCodePaymentAsync(decimal amount, string? customerPhone, string? customerName, string? description, int userId);
        Task<QRCodePayment?> GetQRCodePaymentAsync(string qrReference);
        Task<bool> MarkQRCodeAsPaidAsync(string qrReference, string mpesaReceiptNumber, string transactionCode);
        Task<List<QRCodePayment>> GetPendingQRCodePaymentsAsync();
        Task<bool> LinkQRCodeToSaleAsync(string qrReference, int saleId);
        Task ExpireOldQRCodesAsync();
        
        Task<ManualMpesaEntry> CreateManualMpesaEntryAsync(string mpesaMessage, int userId);
        Task<ManualMpesaEntry?> GetManualMpesaEntryAsync(string transactionCode);
        Task<bool> VerifyManualMpesaEntryAsync(int entryId, bool isValid, string? notes);
        Task<bool> LinkManualMpesaToSaleAsync(int entryId, int saleId);
        Task<List<ManualMpesaEntry>> GetPendingManualMpesaEntriesAsync();
        Task<bool> CheckForMatchingPaymentsAsync();
    }

    public class QRCodePaymentService : IQRCodePaymentService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<QRCodePaymentService> _logger;

        public QRCodePaymentService(ApplicationDbContext context, ILogger<QRCodePaymentService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<QRCodePayment> CreateQRCodePaymentAsync(decimal amount, string? customerPhone, string? customerName, string? description, int userId)
        {
            var qrReference = GenerateQRReference();
            
            var qrPayment = new QRCodePayment
            {
                QRCodeReference = qrReference,
                Amount = amount,
                CustomerPhone = customerPhone,
                CustomerName = customerName,
                Description = description,
                CreatedByUserId = userId,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddMinutes(30),
                Status = "Pending"
            };

            _context.QRCodePayments.Add(qrPayment);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Created QR code payment with reference: {QRReference} for amount: {Amount}", qrReference, amount);
            return qrPayment;
        }

        public async Task<QRCodePayment?> GetQRCodePaymentAsync(string qrReference)
        {
            return await _context.QRCodePayments
                .Include(q => q.CreatedByUser)
                .Include(q => q.Sale)
                .FirstOrDefaultAsync(q => q.QRCodeReference == qrReference);
        }

        public async Task<bool> MarkQRCodeAsPaidAsync(string qrReference, string mpesaReceiptNumber, string transactionCode)
        {
            var qrPayment = await _context.QRCodePayments
                .FirstOrDefaultAsync(q => q.QRCodeReference == qrReference && q.Status == "Pending");

            if (qrPayment == null)
            {
                _logger.LogWarning("QR code payment not found or already processed: {QRReference}", qrReference);
                return false;
            }

            qrPayment.Status = "Paid";
            qrPayment.PaidAt = DateTime.UtcNow;
            qrPayment.MpesaReceiptNumber = mpesaReceiptNumber;
            qrPayment.TransactionCode = transactionCode;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Marked QR code payment as paid: {QRReference} with receipt: {Receipt}", qrReference, mpesaReceiptNumber);
            return true;
        }

        public async Task<List<QRCodePayment>> GetPendingQRCodePaymentsAsync()
        {
            return await _context.QRCodePayments
                .Include(q => q.CreatedByUser)
                .Where(q => q.Status == "Pending" && q.ExpiresAt > DateTime.UtcNow)
                .OrderByDescending(q => q.CreatedAt)
                .ToListAsync();
        }

        public async Task<bool> LinkQRCodeToSaleAsync(string qrReference, int saleId)
        {
            var qrPayment = await _context.QRCodePayments
                .FirstOrDefaultAsync(q => q.QRCodeReference == qrReference);

            if (qrPayment == null)
            {
                return false;
            }

            qrPayment.SaleId = saleId;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Linked QR code payment {QRReference} to sale {SaleId}", qrReference, saleId);
            return true;
        }

        public async Task ExpireOldQRCodesAsync()
        {
            var expiredQRCodes = await _context.QRCodePayments
                .Where(q => q.Status == "Pending" && q.ExpiresAt <= DateTime.UtcNow)
                .ToListAsync();

            foreach (var qr in expiredQRCodes)
            {
                qr.Status = "Expired";
            }

            if (expiredQRCodes.Any())
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation("Expired {Count} QR code payments", expiredQRCodes.Count);
            }
        }

        public async Task<ManualMpesaEntry> CreateManualMpesaEntryAsync(string mpesaMessage, int userId)
        {
            var parsedData = ParseMpesaMessage(mpesaMessage);
            
            var entry = new ManualMpesaEntry
            {
                MpesaMessage = mpesaMessage,
                TransactionCode = parsedData.TransactionCode,
                Amount = parsedData.Amount,
                SenderPhone = parsedData.SenderPhone,
                SenderName = parsedData.SenderName,
                TransactionDate = parsedData.TransactionDate,
                EnteredByUserId = userId,
                CreatedAt = DateTime.UtcNow,
                Status = "Pending"
            };

            _context.ManualMpesaEntries.Add(entry);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Created manual M-Pesa entry with transaction code: {TransactionCode} for amount: {Amount}", 
                parsedData.TransactionCode, parsedData.Amount);
            
            return entry;
        }

        public async Task<ManualMpesaEntry?> GetManualMpesaEntryAsync(string transactionCode)
        {
            return await _context.ManualMpesaEntries
                .Include(m => m.EnteredByUser)
                .Include(m => m.Sale)
                .FirstOrDefaultAsync(m => m.TransactionCode == transactionCode);
        }

        public async Task<bool> VerifyManualMpesaEntryAsync(int entryId, bool isValid, string? notes)
        {
            var entry = await _context.ManualMpesaEntries.FindAsync(entryId);
            if (entry == null) return false;

            entry.IsVerified = true;
            entry.VerifiedAt = DateTime.UtcNow;
            entry.Status = isValid ? "Verified" : "Invalid";
            entry.VerificationNotes = notes;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Verified manual M-Pesa entry {EntryId} as {Status}", entryId, entry.Status);
            return true;
        }

        public async Task<bool> LinkManualMpesaToSaleAsync(int entryId, int saleId)
        {
            var entry = await _context.ManualMpesaEntries.FindAsync(entryId);
            if (entry == null) return false;

            entry.SaleId = saleId;
            entry.Status = "Linked";
            await _context.SaveChangesAsync();

            _logger.LogInformation("Linked manual M-Pesa entry {EntryId} to sale {SaleId}", entryId, saleId);
            return true;
        }

        public async Task<List<ManualMpesaEntry>> GetPendingManualMpesaEntriesAsync()
        {
            return await _context.ManualMpesaEntries
                .Include(m => m.EnteredByUser)
                .Where(m => m.Status == "Pending" || m.Status == "Verified")
                .OrderByDescending(m => m.CreatedAt)
                .ToListAsync();
        }

        public async Task<bool> CheckForMatchingPaymentsAsync()
        {
            // Check for QR codes that might have been paid via C2B
            var pendingQRCodes = await GetPendingQRCodePaymentsAsync();
            var unusedTransactions = await _context.UnusedMpesaTransactions
                .Where(u => u.SaleId == null)
                .ToListAsync();

            bool foundMatches = false;

            foreach (var qr in pendingQRCodes)
            {
                var matchingTransaction = unusedTransactions.FirstOrDefault(u => 
                    Math.Abs(u.Amount - qr.Amount) < 0.01m && // Amount matches within 1 cent
                    u.ReceivedAt >= qr.CreatedAt && // Transaction after QR creation
                    u.ReceivedAt <= qr.ExpiresAt); // Transaction before QR expiry

                if (matchingTransaction != null)
                {
                    await MarkQRCodeAsPaidAsync(qr.QRCodeReference, 
                        matchingTransaction.TransactionCode, 
                        matchingTransaction.TransactionCode);
                    
                    // Link the unused transaction to prevent double-use
                    matchingTransaction.SaleId = -1; // Mark as used for QR
                    foundMatches = true;

                    _logger.LogInformation("Auto-matched QR code {QRReference} with transaction {TransactionCode}", 
                        qr.QRCodeReference, matchingTransaction.TransactionCode);
                }
            }

            if (foundMatches)
            {
                await _context.SaveChangesAsync();
            }

            return foundMatches;
        }

        private string GenerateQRReference()
        {
            return $"QR{DateTime.UtcNow:yyyyMMddHHmmss}{Random.Shared.Next(1000, 9999)}";
        }

        private (string TransactionCode, decimal Amount, string SenderPhone, string SenderName, DateTime TransactionDate) ParseMpesaMessage(string message)
        {
            // Parse different M-Pesa message formats
            // Format 1: "RK61H8I2Q7 Confirmed. Ksh500.00 received from JOHN DOE 254712345678 on 12/10/2024 at 2:30 PM"
            // Format 2: "You have received Ksh 500.00 from JOHN DOE 254712345678 on 12/10/2024 at 2:30 PM. Transaction Cost: Ksh 0.00. Amount: Ksh 500.00. M-PESA balance is Ksh 1,000.00. Transaction ID: RK61H8I2Q7"

            var transactionCode = "";
            var amount = 0m;
            var senderPhone = "";
            var senderName = "";
            var transactionDate = DateTime.UtcNow;

            try
            {
                // Extract transaction code
                var codeMatch = Regex.Match(message, @"([A-Z0-9]{10})", RegexOptions.IgnoreCase);
                if (codeMatch.Success)
                {
                    transactionCode = codeMatch.Groups[1].Value.ToUpper();
                }

                // Extract amount
                var amountMatch = Regex.Match(message, @"Ksh\s*([0-9,]+\.?\d*)", RegexOptions.IgnoreCase);
                if (amountMatch.Success)
                {
                    var amountStr = amountMatch.Groups[1].Value.Replace(",", "");
                    decimal.TryParse(amountStr, out amount);
                }

                // Extract phone number
                var phoneMatch = Regex.Match(message, @"(254\d{9})", RegexOptions.IgnoreCase);
                if (phoneMatch.Success)
                {
                    senderPhone = phoneMatch.Groups[1].Value;
                }

                // Extract sender name (text before phone number)
                var nameMatch = Regex.Match(message, @"from\s+([A-Z\s]+)\s+254\d{9}", RegexOptions.IgnoreCase);
                if (nameMatch.Success)
                {
                    senderName = nameMatch.Groups[1].Value.Trim();
                }

                // Extract date (basic parsing - can be enhanced)
                var dateMatch = Regex.Match(message, @"(\d{1,2}/\d{1,2}/\d{4})", RegexOptions.IgnoreCase);
                if (dateMatch.Success && DateTime.TryParse(dateMatch.Groups[1].Value, out var parsedDate))
                {
                    transactionDate = parsedDate;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing M-Pesa message: {Message}", message);
            }

            return (transactionCode, amount, senderPhone, senderName, transactionDate);
        }
    }
}
