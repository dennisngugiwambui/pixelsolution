using PixelSolution.Services.Interfaces;
using System.Drawing;
using System.Drawing.Imaging;
using System.Text;

namespace PixelSolution.Services
{
    public class BarcodeService : IBarcodeService
    {
        private readonly ILogger<BarcodeService> _logger;

        public BarcodeService(ILogger<BarcodeService> logger)
        {
            _logger = logger;
        }

        public async Task<byte[]> GenerateBarcodeAsync(string data, int width = 200, int height = 50)
        {
            try
            {
                // Simple barcode generation using basic graphics
                // In a real implementation, you might use a library like ZXing.Net
                using var bitmap = new Bitmap(width, height);
                using var graphics = Graphics.FromImage(bitmap);
                
                graphics.Clear(Color.White);
                graphics.FillRectangle(Brushes.Black, 0, 0, width, height);
                
                // Draw barcode pattern (simplified)
                var barWidth = width / data.Length;
                for (int i = 0; i < data.Length; i++)
                {
                    var x = i * barWidth;
                    var color = (data[i] % 2 == 0) ? Brushes.Black : Brushes.White;
                    graphics.FillRectangle(color, x, 0, barWidth, height - 20);
                }
                
                // Add text below barcode
                using var font = new Font("Arial", 8);
                graphics.DrawString(data, font, Brushes.Black, 10, height - 18);
                
                using var stream = new MemoryStream();
                bitmap.Save(stream, ImageFormat.Png);
                return stream.ToArray();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating barcode for data: {Data}", data);
                throw new Exception($"Failed to generate barcode: {ex.Message}", ex);
            }
        }

        public async Task<string> GenerateBarcodeBase64Async(string data, int width = 200, int height = 50)
        {
            try
            {
                var barcodeBytes = await GenerateBarcodeAsync(data, width, height);
                return Convert.ToBase64String(barcodeBytes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating base64 barcode for data: {Data}", data);
                throw;
            }
        }

        public bool ValidateBarcode(string barcode)
        {
            try
            {
                // Basic validation - check if barcode is not null/empty and has valid format
                if (string.IsNullOrWhiteSpace(barcode))
                    return false;

                // Check if barcode contains only valid characters (alphanumeric)
                return barcode.All(c => char.IsLetterOrDigit(c) || c == '-' || c == '_');
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating barcode: {Barcode}", barcode);
                return false;
            }
        }

        public string GenerateProductBarcode(int productId)
        {
            try
            {
                // Generate a simple product barcode using product ID
                var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
                return $"PS{productId:D6}{timestamp.Substring(timestamp.Length - 4)}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating product barcode for ID: {ProductId}", productId);
                throw new Exception($"Failed to generate product barcode: {ex.Message}", ex);
            }
        }

        public async Task<string> GenerateProductBarcodeAsync(int productId, int width = 200, int height = 50)
        {
            try
            {
                var barcodeData = GenerateProductBarcode(productId);
                return barcodeData;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating product barcode async for ID: {ProductId}", productId);
                throw;
            }
        }

        public async Task<byte[]> GenerateQRCodeAsync(string data, int width = 200, int height = 200)
        {
            try
            {
                // Simple QR code generation using basic graphics
                // In a real implementation, you might use a library like QRCoder
                using var bitmap = new Bitmap(width, height);
                using var graphics = Graphics.FromImage(bitmap);
                
                graphics.Clear(Color.White);
                graphics.FillRectangle(Brushes.Black, 10, 10, width - 20, height - 20);
                
                // Create a simple QR-like pattern
                var cellSize = (width - 20) / 25;
                for (int i = 0; i < 25; i++)
                {
                    for (int j = 0; j < 25; j++)
                    {
                        var x = 10 + i * cellSize;
                        var y = 10 + j * cellSize;
                        var color = ((i + j + data.Length) % 2 == 0) ? Brushes.Black : Brushes.White;
                        graphics.FillRectangle(color, x, y, cellSize, cellSize);
                    }
                }
                
                using var stream = new MemoryStream();
                bitmap.Save(stream, ImageFormat.Png);
                return stream.ToArray();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating QR code for data: {Data}", data);
                throw new Exception($"Failed to generate QR code: {ex.Message}", ex);
            }
        }

        public async Task<byte[]> GenerateCode128BarcodeAsync(string data, int width = 200, int height = 50)
        {
            try
            {
                // Code128 barcode generation (simplified)
                using var bitmap = new Bitmap(width, height);
                using var graphics = Graphics.FromImage(bitmap);
                
                graphics.Clear(Color.White);
                
                // Draw Code128-style barcode pattern
                var barWidth = width / (data.Length * 2);
                for (int i = 0; i < data.Length; i++)
                {
                    var x = i * barWidth * 2;
                    var barHeight = height - 20;
                    
                    // Alternating thick and thin bars based on character value
                    var charValue = (int)data[i];
                    var thickBar = (charValue % 3 == 0);
                    var currentBarWidth = thickBar ? barWidth * 2 : barWidth;
                    
                    graphics.FillRectangle(Brushes.Black, x, 0, currentBarWidth, barHeight);
                }
                
                // Add text below barcode
                using var font = new Font("Arial", 8);
                graphics.DrawString(data, font, Brushes.Black, 10, height - 18);
                
                using var stream = new MemoryStream();
                bitmap.Save(stream, ImageFormat.Png);
                return stream.ToArray();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating Code128 barcode for data: {Data}", data);
                throw new Exception($"Failed to generate Code128 barcode: {ex.Message}", ex);
            }
        }

        public async Task<byte[]> GenerateProductStickerAsync(int productId, bool includePrice, int width = 300, int height = 200)
        {
            try
            {
                // Generate a product sticker with barcode and product info
                using var bitmap = new Bitmap(width, height);
                using var graphics = Graphics.FromImage(bitmap);
                
                graphics.Clear(Color.White);
                graphics.DrawRectangle(Pens.Black, 0, 0, width - 1, height - 1);
                
                // Generate barcode for the product
                var barcodeData = GenerateProductBarcode(productId);
                var barcodeHeight = height / 3;
                var barcodeWidth = width - 20;
                
                // Draw barcode area
                var barWidth = barcodeWidth / barcodeData.Length;
                for (int i = 0; i < barcodeData.Length; i++)
                {
                    var x = 10 + i * barWidth;
                    var color = (barcodeData[i] % 2 == 0) ? Brushes.Black : Brushes.White;
                    graphics.FillRectangle(color, x, 10, barWidth, barcodeHeight);
                }
                
                // Add product information
                using var titleFont = new Font("Arial", 12, FontStyle.Bold);
                using var textFont = new Font("Arial", 10);
                
                graphics.DrawString($"Product ID: {productId}", titleFont, Brushes.Black, 10, barcodeHeight + 20);
                graphics.DrawString($"Barcode: {barcodeData}", textFont, Brushes.Black, 10, barcodeHeight + 45);
                
                if (includePrice)
                {
                    graphics.DrawString($"Price: $0.00", textFont, Brushes.Black, 10, barcodeHeight + 65);
                    graphics.DrawString($"Generated: {DateTime.Now:yyyy-MM-dd}", textFont, Brushes.Black, 10, barcodeHeight + 85);
                }
                else
                {
                    graphics.DrawString($"Generated: {DateTime.Now:yyyy-MM-dd}", textFont, Brushes.Black, 10, barcodeHeight + 65);
                }
                
                using var stream = new MemoryStream();
                bitmap.Save(stream, ImageFormat.Png);
                return stream.ToArray();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating product sticker for ID: {ProductId}", productId);
                throw new Exception($"Failed to generate product sticker: {ex.Message}", ex);
            }
        }
    }
}
