using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PixelSolution.Services;
using System.Security.Claims;
using QRCoder;
using System.Drawing;
using System.Drawing.Imaging;

namespace PixelSolution.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class QRPaymentController : ControllerBase
    {
        private readonly IQRCodePaymentService _qrPaymentService;
        private readonly ILogger<QRPaymentController> _logger;

        public QRPaymentController(IQRCodePaymentService qrPaymentService, ILogger<QRPaymentController> logger)
        {
            _qrPaymentService = qrPaymentService;
            _logger = logger;
        }

        [HttpPost("create-qr")]
        public async Task<IActionResult> CreateQRCode([FromBody] CreateQRRequest request)
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
                
                var qrPayment = await _qrPaymentService.CreateQRCodePaymentAsync(
                    request.Amount, 
                    request.CustomerPhone, 
                    request.CustomerName, 
                    request.Description, 
                    userId);

                // Generate QR code image
                var qrCodeData = GenerateQRCodeData(qrPayment.QRCodeReference, qrPayment.Amount);
                var qrCodeImage = GenerateQRCodeImage(qrCodeData);

                return Ok(new
                {
                    success = true,
                    qrReference = qrPayment.QRCodeReference,
                    qrCodeImage = Convert.ToBase64String(qrCodeImage),
                    amount = qrPayment.Amount,
                    expiresAt = qrPayment.ExpiresAt,
                    tillNumber = qrPayment.TillNumber
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating QR code payment");
                return BadRequest(new { success = false, message = "Error creating QR code payment" });
            }
        }

        [HttpGet("check-qr-status/{qrReference}")]
        public async Task<IActionResult> CheckQRStatus(string qrReference)
        {
            try
            {
                var qrPayment = await _qrPaymentService.GetQRCodePaymentAsync(qrReference);
                
                if (qrPayment == null)
                {
                    return NotFound(new { success = false, message = "QR code payment not found" });
                }

                // Check for matching payments
                await _qrPaymentService.CheckForMatchingPaymentsAsync();
                
                // Refresh the payment status
                qrPayment = await _qrPaymentService.GetQRCodePaymentAsync(qrReference);

                return Ok(new
                {
                    success = true,
                    status = qrPayment.Status,
                    amount = qrPayment.Amount,
                    paidAt = qrPayment.PaidAt,
                    mpesaReceiptNumber = qrPayment.MpesaReceiptNumber,
                    transactionCode = qrPayment.TransactionCode,
                    expiresAt = qrPayment.ExpiresAt,
                    isExpired = qrPayment.ExpiresAt <= DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking QR status for reference: {QRReference}", qrReference);
                return BadRequest(new { success = false, message = "Error checking QR status" });
            }
        }

        [HttpPost("manual-mpesa-entry")]
        public async Task<IActionResult> CreateManualMpesaEntry([FromBody] ManualMpesaRequest request)
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
                
                var entry = await _qrPaymentService.CreateManualMpesaEntryAsync(request.MpesaMessage, userId);

                return Ok(new
                {
                    success = true,
                    entryId = entry.ManualMpesaEntryId,
                    transactionCode = entry.TransactionCode,
                    amount = entry.Amount,
                    senderPhone = entry.SenderPhone,
                    senderName = entry.SenderName,
                    transactionDate = entry.TransactionDate,
                    status = entry.Status
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating manual M-Pesa entry");
                return BadRequest(new { success = false, message = "Error processing M-Pesa message" });
            }
        }

        [HttpPost("verify-manual-entry/{entryId}")]
        public async Task<IActionResult> VerifyManualEntry(int entryId, [FromBody] VerifyEntryRequest request)
        {
            try
            {
                var success = await _qrPaymentService.VerifyManualMpesaEntryAsync(entryId, request.IsValid, request.Notes);
                
                if (!success)
                {
                    return NotFound(new { success = false, message = "Manual entry not found" });
                }

                return Ok(new { success = true, message = "Manual entry verified successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying manual M-Pesa entry: {EntryId}", entryId);
                return BadRequest(new { success = false, message = "Error verifying manual entry" });
            }
        }

        [HttpGet("pending-qr-payments")]
        public async Task<IActionResult> GetPendingQRPayments()
        {
            try
            {
                var pendingPayments = await _qrPaymentService.GetPendingQRCodePaymentsAsync();
                
                return Ok(new
                {
                    success = true,
                    payments = pendingPayments.Select(p => new
                    {
                        qrReference = p.QRCodeReference,
                        amount = p.Amount,
                        customerPhone = p.CustomerPhone,
                        customerName = p.CustomerName,
                        description = p.Description,
                        createdAt = p.CreatedAt,
                        expiresAt = p.ExpiresAt,
                        status = p.Status,
                        createdBy = p.CreatedByUser.FirstName + " " + p.CreatedByUser.LastName
                    })
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting pending QR payments");
                return BadRequest(new { success = false, message = "Error getting pending QR payments" });
            }
        }

        [HttpGet("pending-manual-entries")]
        public async Task<IActionResult> GetPendingManualEntries()
        {
            try
            {
                var pendingEntries = await _qrPaymentService.GetPendingManualMpesaEntriesAsync();
                
                return Ok(new
                {
                    success = true,
                    entries = pendingEntries.Select(e => new
                    {
                        entryId = e.ManualMpesaEntryId,
                        transactionCode = e.TransactionCode,
                        amount = e.Amount,
                        senderPhone = e.SenderPhone,
                        senderName = e.SenderName,
                        transactionDate = e.TransactionDate,
                        createdAt = e.CreatedAt,
                        status = e.Status,
                        isVerified = e.IsVerified,
                        verificationNotes = e.VerificationNotes,
                        enteredBy = e.EnteredByUser.FirstName + " " + e.EnteredByUser.LastName,
                        mpesaMessage = e.MpesaMessage
                    })
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting pending manual entries");
                return BadRequest(new { success = false, message = "Error getting pending manual entries" });
            }
        }

        [HttpPost("link-qr-to-sale")]
        public async Task<IActionResult> LinkQRToSale([FromBody] LinkQRToSaleRequest request)
        {
            try
            {
                var success = await _qrPaymentService.LinkQRCodeToSaleAsync(request.QRReference, request.SaleId);
                
                if (!success)
                {
                    return NotFound(new { success = false, message = "QR code payment not found" });
                }

                return Ok(new { success = true, message = "QR code linked to sale successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error linking QR to sale");
                return BadRequest(new { success = false, message = "Error linking QR to sale" });
            }
        }

        [HttpPost("link-manual-to-sale")]
        public async Task<IActionResult> LinkManualToSale([FromBody] LinkManualToSaleRequest request)
        {
            try
            {
                var success = await _qrPaymentService.LinkManualMpesaToSaleAsync(request.EntryId, request.SaleId);
                
                if (!success)
                {
                    return NotFound(new { success = false, message = "Manual entry not found" });
                }

                return Ok(new { success = true, message = "Manual entry linked to sale successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error linking manual entry to sale");
                return BadRequest(new { success = false, message = "Error linking manual entry to sale" });
            }
        }

        [HttpPost("check-matching-payments")]
        public async Task<IActionResult> CheckMatchingPayments()
        {
            try
            {
                var foundMatches = await _qrPaymentService.CheckForMatchingPaymentsAsync();
                
                return Ok(new 
                { 
                    success = true, 
                    foundMatches = foundMatches,
                    message = foundMatches ? "Found and processed matching payments" : "No matching payments found"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking matching payments");
                return BadRequest(new { success = false, message = "Error checking matching payments" });
            }
        }

        private string GenerateQRCodeData(string qrReference, decimal amount)
        {
            // Generate QR code data for M-Pesa till payment
            // Format: Till number, amount, reference
            return $"Till:6509715|Amount:{amount}|Ref:{qrReference}|Desc:PixelSolution Payment";
        }

        private byte[] GenerateQRCodeImage(string data)
        {
            using var qrGenerator = new QRCodeGenerator();
            using var qrCodeData = qrGenerator.CreateQrCode(data, QRCodeGenerator.ECCLevel.Q);
            using var qrCode = new QRCode(qrCodeData);
            using var qrCodeImage = qrCode.GetGraphic(20);
            
            using var stream = new MemoryStream();
            qrCodeImage.Save(stream, ImageFormat.Png);
            return stream.ToArray();
        }
    }

    public class CreateQRRequest
    {
        public decimal Amount { get; set; }
        public string? CustomerPhone { get; set; }
        public string? CustomerName { get; set; }
        public string? Description { get; set; }
    }

    public class ManualMpesaRequest
    {
        public string MpesaMessage { get; set; } = string.Empty;
    }

    public class VerifyEntryRequest
    {
        public bool IsValid { get; set; }
        public string? Notes { get; set; }
    }

    public class LinkQRToSaleRequest
    {
        public string QRReference { get; set; } = string.Empty;
        public int SaleId { get; set; }
    }

    public class LinkManualToSaleRequest
    {
        public int EntryId { get; set; }
        public int SaleId { get; set; }
    }
}
