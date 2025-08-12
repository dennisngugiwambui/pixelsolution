using PixelSolution.Services.Interfaces;
using System.Drawing;
using System.Drawing.Printing;
using System.Text;

namespace PixelSolution.Services
{
    public class ReceiptPrintingService : IReceiptPrintingService
    {
        private readonly ILogger<ReceiptPrintingService> _logger;
        private readonly IReportService _reportService;

        public ReceiptPrintingService(ILogger<ReceiptPrintingService> logger, IReportService reportService)
        {
            _logger = logger;
            _reportService = reportService;
        }

        public async Task<bool> PrintReceiptAsync(byte[] receiptData, string printerName = null)
        {
            try
            {
                if (receiptData == null || receiptData.Length == 0)
                {
                    _logger.LogWarning("Receipt data is null or empty");
                    return false;
                }

                // Convert receipt data to string
                var receiptContent = Encoding.UTF8.GetString(receiptData);
                
                // Get printer name or use default
                var targetPrinter = printerName ?? GetDefaultPrinter();
                
                if (string.IsNullOrEmpty(targetPrinter))
                {
                    _logger.LogWarning("No printer available for printing");
                    return false;
                }

                // Check if printer is available
                if (!await IsPrinterAvailableAsync(targetPrinter))
                {
                    _logger.LogWarning("Printer {PrinterName} is not available", targetPrinter);
                    return false;
                }

                // Create print document
                var printDocument = new PrintDocument();
                printDocument.PrinterSettings.PrinterName = targetPrinter;
                
                // Set up print event handler
                printDocument.PrintPage += (sender, e) =>
                {
                    var font = new Font("Courier New", 10);
                    var brush = new SolidBrush(Color.Black);
                    var lines = receiptContent.Split('\n');
                    
                    float yPosition = e.MarginBounds.Top;
                    float lineHeight = font.GetHeight(e.Graphics);
                    
                    foreach (var line in lines)
                    {
                        if (yPosition + lineHeight > e.MarginBounds.Bottom)
                            break;
                            
                        e.Graphics.DrawString(line, font, brush, e.MarginBounds.Left, yPosition);
                        yPosition += lineHeight;
                    }
                    
                    font.Dispose();
                    brush.Dispose();
                };

                // Print the document
                printDocument.Print();
                printDocument.Dispose();

                _logger.LogInformation("Receipt printed successfully to {PrinterName}", targetPrinter);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error printing receipt to printer: {PrinterName}", printerName);
                return false;
            }
        }

        public async Task<bool> PrintReceiptAsync(int saleId, string printerName = null)
        {
            try
            {
                var receiptData = await _reportService.GenerateSalesReceiptAsync(saleId);
                return await PrintReceiptAsync(receiptData, printerName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error printing receipt for sale ID: {SaleId}", saleId);
                return false;
            }
        }

        public async Task<bool> PrintSalesReceiptAsync(int saleId, string printerName = null)
        {
            try
            {
                var receiptData = await _reportService.GenerateSalesReceiptAsync(saleId);
                return await PrintReceiptAsync(receiptData, printerName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error printing sales receipt for sale ID: {SaleId}", saleId);
                return false;
            }
        }

        public async Task<bool> PrintPurchaseRequestReceiptAsync(int purchaseRequestId, string printerName = null)
        {
            try
            {
                var receiptData = await _reportService.GeneratePurchaseRequestReceiptAsync(purchaseRequestId);
                return await PrintReceiptAsync(receiptData, printerName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error printing purchase request receipt for ID: {PurchaseRequestId}", purchaseRequestId);
                return false;
            }
        }

        public async Task<List<string>> GetAvailablePrintersAsync()
        {
            try
            {
                var printers = new List<string>();
                
                foreach (string printerName in PrinterSettings.InstalledPrinters)
                {
                    printers.Add(printerName);
                }
                
                _logger.LogInformation("Found {Count} available printers", printers.Count);
                return printers;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting available printers");
                return new List<string>();
            }
        }

        public async Task<bool> IsPrinterAvailableAsync(string printerName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(printerName))
                    return false;

                var availablePrinters = await GetAvailablePrintersAsync();
                return availablePrinters.Contains(printerName, StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking printer availability: {PrinterName}", printerName);
                return false;
            }
        }

        private string GetDefaultPrinter()
        {
            try
            {
                var printDocument = new PrintDocument();
                var defaultPrinter = printDocument.PrinterSettings.PrinterName;
                printDocument.Dispose();
                return defaultPrinter;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting default printer");
                return string.Empty;
            }
        }
    }
}
