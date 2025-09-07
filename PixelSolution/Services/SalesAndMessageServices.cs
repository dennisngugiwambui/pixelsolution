using Microsoft.EntityFrameworkCore;
using PixelSolution.Data;
using PixelSolution.Models;
using PixelSolution.Services.Interfaces;
using PixelSolution.ViewModels;

namespace PixelSolution.Services
{
    public class SaleService : ISaleService
    {
        private readonly ApplicationDbContext _context;

        public SaleService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Sale>> GetAllSalesAsync()
        {
            return await _context.Sales
                .Include(s => s.User)
                .Include(s => s.SaleItems)
                    .ThenInclude(si => si.Product)
                .OrderByDescending(s => s.SaleDate)
                .ToListAsync();
        }

        public async Task<IEnumerable<Sale>> GetSalesByDateRangeAsync(DateTime startDate, DateTime endDate)
        {
            return await _context.Sales
                .Include(s => s.User)
                .Include(s => s.SaleItems)
                    .ThenInclude(si => si.Product)
                .Where(s => s.SaleDate >= startDate && s.SaleDate <= endDate)
                .OrderByDescending(s => s.SaleDate)
                .ToListAsync();
        }

        public async Task<IEnumerable<Sale>> GetSalesByUserAsync(int userId)
        {
            return await _context.Sales
                .Include(s => s.User)
                .Include(s => s.SaleItems)
                    .ThenInclude(si => si.Product)
                .Where(s => s.UserId == userId)
                .OrderByDescending(s => s.SaleDate)
                .ToListAsync();
        }

        public async Task<Sale?> GetSaleByIdAsync(int saleId)
        {
            return await _context.Sales
                .Include(s => s.User)
                .Include(s => s.SaleItems)
                    .ThenInclude(si => si.Product)
                        .ThenInclude(p => p.Category)
                .FirstOrDefaultAsync(s => s.SaleId == saleId);
        }

        public async Task<Sale?> GetSaleByNumberAsync(string saleNumber)
        {
            return await _context.Sales
                .Include(s => s.User)
                .Include(s => s.SaleItems)
                    .ThenInclude(si => si.Product)
                .FirstOrDefaultAsync(s => s.SaleNumber == saleNumber);
        }

        public async Task<Sale> CreateSaleAsync(Sale sale)
        {
            Console.WriteLine($"[DEBUG] CreateSaleAsync called with {sale.SaleItems?.Count ?? 0} items, UserId: {sale.UserId}, PaymentMethod: {sale.PaymentMethod}");
            
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                Console.WriteLine("[DEBUG] Database transaction started");
                
                // Generate sale number
                sale.SaleNumber = await GenerateSaleNumberAsync();
                sale.SaleDate = DateTime.UtcNow;
                Console.WriteLine($"[DEBUG] Generated sale number: {sale.SaleNumber}");

                // Calculate total amount from sale items
                sale.TotalAmount = sale.SaleItems.Sum(si => si.TotalPrice);
                Console.WriteLine($"[DEBUG] Calculated total amount: {sale.TotalAmount}");

                Console.WriteLine("[DEBUG] Adding sale to context");
                _context.Sales.Add(sale);
                
                Console.WriteLine("[DEBUG] Saving sale to database");
                await _context.SaveChangesAsync();
                Console.WriteLine($"[DEBUG] Sale saved with ID: {sale.SaleId}");

                // Update product stock quantities (only if not from purchase request)
                bool isFromPurchaseRequest = sale.PaymentMethod == "Purchase Request";
                Console.WriteLine($"[DEBUG] Updating stock for {sale.SaleItems.Count} products. From Purchase Request: {isFromPurchaseRequest}");
                
                foreach (var saleItem in sale.SaleItems)
                {
                    Console.WriteLine($"[DEBUG] Processing product ID: {saleItem.ProductId}, Quantity: {saleItem.Quantity}");
                    var product = await _context.Products.FindAsync(saleItem.ProductId);
                    if (product != null)
                    {
                        if (isFromPurchaseRequest)
                        {
                            Console.WriteLine($"[DEBUG] Product {product.Name} - Skipping stock deduction (already deducted in purchase request)");
                        }
                        else
                        {
                            Console.WriteLine($"[DEBUG] Product {product.Name} - Old stock: {product.StockQuantity}, Deducting: {saleItem.Quantity}");
                            product.StockQuantity -= saleItem.Quantity;
                            product.UpdatedAt = DateTime.UtcNow;
                            Console.WriteLine($"[DEBUG] Product {product.Name} - New stock: {product.StockQuantity}");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"[DEBUG] WARNING: Product with ID {saleItem.ProductId} not found!");
                    }
                }

                Console.WriteLine("[DEBUG] Saving stock updates");
                await _context.SaveChangesAsync();
                
                Console.WriteLine("[DEBUG] Committing transaction");
                await transaction.CommitAsync();
                Console.WriteLine("[DEBUG] Transaction committed successfully");

                var result = await GetSaleByIdAsync(sale.SaleId) ?? sale;
                Console.WriteLine($"[DEBUG] Returning sale: {result.SaleNumber} with ID: {result.SaleId}");
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG] ERROR in CreateSaleAsync: {ex.Message}");
                Console.WriteLine($"[DEBUG] Stack trace: {ex.StackTrace}");
                await transaction.RollbackAsync();
                Console.WriteLine("[DEBUG] Transaction rolled back");
                throw;
            }
        }

        public async Task<Sale> UpdateSaleAsync(Sale sale)
        {
            _context.Sales.Update(sale);
            await _context.SaveChangesAsync();
            return await GetSaleByIdAsync(sale.SaleId) ?? sale;
        }

        public async Task<bool> CancelSaleAsync(int saleId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var sale = await GetSaleByIdAsync(saleId);
                if (sale == null || sale.Status == "Cancelled")
                    return false;

                sale.Status = "Cancelled";

                // Restore product stock quantities
                foreach (var saleItem in sale.SaleItems)
                {
                    var product = await _context.Products.FindAsync(saleItem.ProductId);
                    if (product != null)
                    {
                        product.StockQuantity += saleItem.Quantity;
                        product.UpdatedAt = DateTime.UtcNow;
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return true;
            }
            catch
            {
                await transaction.RollbackAsync();
                return false;
            }
        }

        public async Task<decimal> GetTotalSalesAmountAsync(DateTime? startDate = null, DateTime? endDate = null)
        {
            var query = _context.Sales.Where(s => s.Status == "Completed");

            if (startDate.HasValue)
                query = query.Where(s => s.SaleDate >= startDate.Value);

            if (endDate.HasValue)
                query = query.Where(s => s.SaleDate <= endDate.Value);

            return await query.SumAsync(s => s.TotalAmount);
        }

        public async Task<int> GetTotalSalesCountAsync(DateTime? startDate = null, DateTime? endDate = null)
        {
            var query = _context.Sales.Where(s => s.Status == "Completed");

            if (startDate.HasValue)
                query = query.Where(s => s.SaleDate >= startDate.Value);

            if (endDate.HasValue)
                query = query.Where(s => s.SaleDate <= endDate.Value);

            return await query.CountAsync();
        }

        public async Task<IEnumerable<object>> GetSalesAnalyticsAsync()
        {
            var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);

            var analytics = await _context.Sales
                .Where(s => s.SaleDate >= thirtyDaysAgo && s.Status == "Completed")
                .GroupBy(s => s.SaleDate.Date)
                .Select(g => new
                {
                    Date = g.Key,
                    TotalSales = g.Sum(s => s.TotalAmount),
                    SalesCount = g.Count()
                })
                .OrderBy(x => x.Date)
                .ToListAsync();

            return analytics;
        }

        private async Task<string> GenerateSaleNumberAsync()
        {
            var today = DateTime.Today;
            var prefix = $"SAL{today:yyyyMMdd}";

            var lastSale = await _context.Sales
                .Where(s => s.SaleNumber.StartsWith(prefix))
                .OrderByDescending(s => s.SaleNumber)
                .FirstOrDefaultAsync();

            int nextNumber = 1;
            if (lastSale != null)
            {
                var numberPart = lastSale.SaleNumber.Substring(prefix.Length);
                if (int.TryParse(numberPart, out int lastNumber))
                {
                    nextNumber = lastNumber + 1;
                }
            }

            return $"{prefix}{nextNumber:D4}";
        }
    }

    public class PurchaseRequestService : IPurchaseRequestService
    {
        private readonly ApplicationDbContext _context;

        public PurchaseRequestService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<PurchaseRequest>> GetAllPurchaseRequestsAsync()
        {
            return await _context.PurchaseRequests
                .Include(pr => pr.User)
                .Include(pr => pr.Supplier)
                .Include(pr => pr.PurchaseRequestItems)
                    .ThenInclude(pri => pri.Product)
                .OrderByDescending(pr => pr.RequestDate)
                .ToListAsync();
        }

        public async Task<IEnumerable<PurchaseRequest>> GetPurchaseRequestsByStatusAsync(string status)
        {
            return await _context.PurchaseRequests
                .Include(pr => pr.User)
                .Include(pr => pr.Supplier)
                .Include(pr => pr.PurchaseRequestItems)
                    .ThenInclude(pri => pri.Product)
                .Where(pr => pr.Status == status)
                .OrderByDescending(pr => pr.RequestDate)
                .ToListAsync();
        }

        public async Task<IEnumerable<PurchaseRequest>> GetPurchaseRequestsByUserAsync(int userId)
        {
            return await _context.PurchaseRequests
                .Include(pr => pr.User)
                .Include(pr => pr.Supplier)
                .Include(pr => pr.PurchaseRequestItems)
                    .ThenInclude(pri => pri.Product)
                .Where(pr => pr.UserId == userId)
                .OrderByDescending(pr => pr.RequestDate)
                .ToListAsync();
        }

        public async Task<PurchaseRequest?> GetPurchaseRequestByIdAsync(int purchaseRequestId)
        {
            return await _context.PurchaseRequests
                .Include(pr => pr.User)
                .Include(pr => pr.Supplier)
                .Include(pr => pr.PurchaseRequestItems)
                    .ThenInclude(pri => pri.Product)
                        .ThenInclude(p => p.Category)
                .FirstOrDefaultAsync(pr => pr.PurchaseRequestId == purchaseRequestId);
        }

        public async Task<PurchaseRequest> CreatePurchaseRequestAsync(PurchaseRequest purchaseRequest)
        {
            purchaseRequest.RequestNumber = await GenerateRequestNumberAsync();
            purchaseRequest.RequestDate = DateTime.UtcNow;
            purchaseRequest.Status = "Pending";

            // Calculate total amount from items
            purchaseRequest.TotalAmount = purchaseRequest.PurchaseRequestItems.Sum(pri => pri.TotalPrice);

            _context.PurchaseRequests.Add(purchaseRequest);
            await _context.SaveChangesAsync();

            return await GetPurchaseRequestByIdAsync(purchaseRequest.PurchaseRequestId) ?? purchaseRequest;
        }

        public async Task<PurchaseRequest> UpdatePurchaseRequestAsync(PurchaseRequest purchaseRequest)
        {
            _context.PurchaseRequests.Update(purchaseRequest);
            await _context.SaveChangesAsync();
            return await GetPurchaseRequestByIdAsync(purchaseRequest.PurchaseRequestId) ?? purchaseRequest;
        }

        public async Task<bool> ApprovePurchaseRequestAsync(int purchaseRequestId)
        {
            try
            {
                var purchaseRequest = await _context.PurchaseRequests.FindAsync(purchaseRequestId);
                if (purchaseRequest == null || purchaseRequest.Status != "Pending")
                    return false;

                purchaseRequest.Status = "Approved";
                purchaseRequest.ApprovedDate = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> CancelPurchaseRequestAsync(int purchaseRequestId)
        {
            try
            {
                var purchaseRequest = await _context.PurchaseRequests.FindAsync(purchaseRequestId);
                if (purchaseRequest == null)
                    return false;

                purchaseRequest.Status = "Cancelled";
                await _context.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<string> GenerateRequestNumberAsync()
        {
            var today = DateTime.Today;
            var prefix = $"PUR{today:yyyyMMdd}";

            var lastRequest = await _context.PurchaseRequests
                .Where(pr => pr.RequestNumber.StartsWith(prefix))
                .OrderByDescending(pr => pr.RequestNumber)
                .FirstOrDefaultAsync();

            int nextNumber = 1;
            if (lastRequest != null)
            {
                var numberPart = lastRequest.RequestNumber.Substring(prefix.Length);
                if (int.TryParse(numberPart, out int lastNumber))
                {
                    nextNumber = lastNumber + 1;
                }
            }

            return $"{prefix}{nextNumber:D4}";
        }
    }

    // ======================================
    // ENHANCED MESSAGE SERVICE - COMPLETE REWRITE
    // ======================================
    public class MessageService : IMessageService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<MessageService> _logger;

        public MessageService(ApplicationDbContext context, ILogger<MessageService> logger)
        {
            _context = context;
            _logger = logger;
        }

        #region Basic Message Operations

        public async Task<IEnumerable<Message>> GetAllMessagesAsync()
        {
            try
            {
                return await _context.Messages
                    .Include(m => m.FromUser)
                    .Include(m => m.ToUser)
                    .OrderByDescending(m => m.SentDate)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all messages");
                return new List<Message>();
            }
        }

        public async Task<IEnumerable<Message>> GetMessagesByUserAsync(int userId)
        {
            try
            {
                return await _context.Messages
                    .Include(m => m.FromUser)
                    .Include(m => m.ToUser)
                    .Where(m => m.ToUserId == userId || m.FromUserId == userId)
                    .OrderByDescending(m => m.SentDate)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting messages for user {UserId}", userId);
                return new List<Message>();
            }
        }

        public async Task<IEnumerable<Message>> GetUnreadMessagesAsync(int userId)
        {
            try
            {
                return await _context.Messages
                    .Include(m => m.FromUser)
                    .Include(m => m.ToUser)
                    .Where(m => m.ToUserId == userId && !m.IsRead)
                    .OrderByDescending(m => m.SentDate)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting unread messages for user {UserId}", userId);
                return new List<Message>();
            }
        }

        public async Task<Message?> GetMessageByIdAsync(int messageId)
        {
            try
            {
                return await _context.Messages
                    .Include(m => m.FromUser)
                    .Include(m => m.ToUser)
                    .FirstOrDefaultAsync(m => m.MessageId == messageId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting message by ID {MessageId}", messageId);
                return null;
            }
        }

        public async Task<Message> SendMessageAsync(Message message)
        {
            try
            {
                message.SentDate = DateTime.UtcNow;
                message.IsRead = false;

                _context.Messages.Add(message);
                await _context.SaveChangesAsync();

                _logger.LogInformation("✅ Message sent from user {FromUserId} to user {ToUserId} - Subject: {Subject}",
                    message.FromUserId, message.ToUserId, message.Subject);

                return await GetMessageByIdAsync(message.MessageId) ?? message;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error sending message from {FromUserId} to {ToUserId}",
                    message.FromUserId, message.ToUserId);
                throw;
            }
        }

        public async Task<bool> MarkAsReadAsync(int messageId)
        {
            try
            {
                var message = await _context.Messages.FindAsync(messageId);
                if (message == null)
                {
                    _logger.LogWarning("⚠️ Message not found for marking as read: {MessageId}", messageId);
                    return false;
                }

                message.IsRead = true;
                message.ReadDate = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                _logger.LogInformation("👁️ Message marked as read: {MessageId}", messageId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error marking message as read: {MessageId}", messageId);
                return false;
            }
        }

        public async Task<bool> DeleteMessageAsync(int messageId)
        {
            try
            {
                var message = await _context.Messages.FindAsync(messageId);
                if (message == null)
                {
                    _logger.LogWarning("⚠️ Message not found for deletion: {MessageId}", messageId);
                    return false;
                }

                _context.Messages.Remove(message);
                await _context.SaveChangesAsync();

                _logger.LogInformation("🗑️ Message deleted: {MessageId}", messageId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error deleting message: {MessageId}", messageId);
                return false;
            }
        }

        public async Task<int> GetUnreadCountAsync(int userId)
        {
            try
            {
                return await _context.Messages
                    .CountAsync(m => m.ToUserId == userId && !m.IsRead);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error getting unread count for user {UserId}", userId);
                return 0;
            }
        }

        #endregion

        #region Enhanced Message Operations

        public async Task<bool> MarkConversationAsReadAsync(int currentUserId, int otherUserId)
        {
            try
            {
                var unreadMessages = await _context.Messages
                    .Where(m => m.FromUserId == otherUserId &&
                               m.ToUserId == currentUserId &&
                               !m.IsRead)
                    .ToListAsync();

                foreach (var message in unreadMessages)
                {
                    message.IsRead = true;
                    message.ReadDate = DateTime.UtcNow;
                }

                if (unreadMessages.Any())
                {
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("👁️ Marked {Count} messages as read in conversation between {CurrentUserId} and {OtherUserId}",
                        unreadMessages.Count, currentUserId, otherUserId);
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error marking conversation as read between {CurrentUserId} and {OtherUserId}",
                    currentUserId, otherUserId);
                return false;
            }
        }

        public async Task<List<Message>> GetConversationMessagesAsync(int user1Id, int user2Id, int limit = 50, int offset = 0)
        {
            try
            {
                return await _context.Messages
                    .Include(m => m.FromUser)
                    .Include(m => m.ToUser)
                    .Where(m => (m.FromUserId == user1Id && m.ToUserId == user2Id) ||
                               (m.FromUserId == user2Id && m.ToUserId == user1Id))
                    .OrderByDescending(m => m.SentDate)
                    .Skip(offset)
                    .Take(limit)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error getting conversation messages between {User1Id} and {User2Id}", user1Id, user2Id);
                return new List<Message>();
            }
        }

        public async Task<List<Message>> SearchMessagesAsync(int userId, string searchTerm, string? messageType = null, bool? isRead = null)
        {
            try
            {
                var query = _context.Messages
                    .Include(m => m.FromUser)
                    .Include(m => m.ToUser)
                    .Where(m => m.FromUserId == userId || m.ToUserId == userId);

                if (!string.IsNullOrEmpty(searchTerm))
                {
                    var lowerSearchTerm = searchTerm.ToLower();
                    query = query.Where(m =>
                        m.Subject.ToLower().Contains(lowerSearchTerm) ||
                        m.Content.ToLower().Contains(lowerSearchTerm) ||
                        m.FromUser.FirstName.ToLower().Contains(lowerSearchTerm) ||
                        m.FromUser.LastName.ToLower().Contains(lowerSearchTerm) ||
                        m.ToUser.FirstName.ToLower().Contains(lowerSearchTerm) ||
                        m.ToUser.LastName.ToLower().Contains(lowerSearchTerm));
                }

                if (!string.IsNullOrEmpty(messageType))
                {
                    query = query.Where(m => m.MessageType == messageType);
                }

                if (isRead.HasValue)
                {
                    query = query.Where(m => m.IsRead == isRead.Value);
                }

                return await query
                    .OrderByDescending(m => m.SentDate)
                    .Take(100)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error searching messages for user {UserId}", userId);
                return new List<Message>();
            }
        }

        public async Task<Dictionary<int, int>> GetUnreadCountsByUserAsync(int currentUserId)
        {
            try
            {
                return await _context.Messages
                    .Where(m => m.ToUserId == currentUserId && !m.IsRead)
                    .GroupBy(m => m.FromUserId)
                    .ToDictionaryAsync(g => g.Key, g => g.Count());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error getting unread counts by user for {UserId}", currentUserId);
                return new Dictionary<int, int>();
            }
        }

        public async Task<List<Message>> GetRecentMessagesAsync(int userId, int limit = 10)
        {
            try
            {
                return await _context.Messages
                    .Include(m => m.FromUser)
                    .Include(m => m.ToUser)
                    .Where(m => m.FromUserId == userId || m.ToUserId == userId)
                    .OrderByDescending(m => m.SentDate)
                    .Take(limit)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error getting recent messages for user {UserId}", userId);
                return new List<Message>();
            }
        }

        public async Task<bool> BulkMarkAsReadAsync(int userId, List<int> messageIds)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var messages = await _context.Messages
                    .Where(m => messageIds.Contains(m.MessageId) && m.ToUserId == userId && !m.IsRead)
                    .ToListAsync();

                foreach (var message in messages)
                {
                    message.IsRead = true;
                    message.ReadDate = DateTime.UtcNow;
                }

                if (messages.Any())
                {
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                    _logger.LogInformation("👁️ Bulk marked {Count} messages as read for user {UserId}", messages.Count, userId);
                }
                else
                {
                    await transaction.CommitAsync();
                }

                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "❌ Error bulk marking messages as read for user {UserId}", userId);
                return false;
            }
        }

        public async Task<bool> BulkDeleteMessagesAsync(int userId, List<int> messageIds)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var messages = await _context.Messages
                    .Where(m => messageIds.Contains(m.MessageId) &&
                               (m.FromUserId == userId || m.ToUserId == userId))
                    .ToListAsync();

                if (messages.Any())
                {
                    _context.Messages.RemoveRange(messages);
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                    _logger.LogInformation("🗑️ Bulk deleted {Count} messages for user {UserId}", messages.Count, userId);
                }
                else
                {
                    await transaction.CommitAsync();
                }

                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "❌ Error bulk deleting messages for user {UserId}", userId);
                return false;
            }
        }

        public async Task<List<ConversationViewModel>> GetUserConversationsAsync(int currentUserId)
        {
            try
            {
                _logger.LogInformation("📝 Getting conversations for user {UserId}", currentUserId);

                // Get all users that have exchanged messages with current user
                var conversationUserIds = await _context.Messages
                    .Where(m => m.FromUserId == currentUserId || m.ToUserId == currentUserId)
                    .Select(m => m.FromUserId == currentUserId ? m.ToUserId : m.FromUserId)
                    .Distinct()
                    .ToListAsync();

                var conversations = new List<ConversationViewModel>();

                foreach (var userId in conversationUserIds)
                {
                    var user = await _context.Users.FindAsync(userId);
                    if (user == null || user.Status != "Active") continue;

                    // Get last message in conversation
                    var lastMessage = await _context.Messages
                        .Where(m => (m.FromUserId == currentUserId && m.ToUserId == userId) ||
                                   (m.FromUserId == userId && m.ToUserId == currentUserId))
                        .OrderByDescending(m => m.SentDate)
                        .FirstOrDefaultAsync();

                    // Count unread messages from this user
                    var unreadCount = await _context.Messages
                        .CountAsync(m => m.FromUserId == userId &&
                                       m.ToUserId == currentUserId &&
                                       !m.IsRead);

                    var conversation = new ConversationViewModel
                    {
                        UserId = user.UserId,
                        FullName = $"{user.FirstName} {user.LastName}".Trim(),
                        UserInitials = GetUserInitials(user.FirstName, user.LastName),
                        UserType = user.UserType,
                        Email = user.Email,
                        IsOnline = await IsUserOnlineAsync(user.UserId),
                        LastSeen = user.UpdatedAt,
                        LastSeenFormatted = GetLastSeenFormatted(user.UpdatedAt),
                        LastMessage = lastMessage?.Content?.Length > 50
                            ? lastMessage.Content.Substring(0, 47) + "..."
                            : lastMessage?.Content ?? "No messages yet",
                        LastMessageTime = lastMessage?.SentDate.ToString("HH:mm") ?? "",
                        LastMessageDate = lastMessage?.SentDate,
                        UnreadCount = unreadCount,
                        HasUnreadMessages = unreadCount > 0,
                        Status = user.Status
                    };

                    conversations.Add(conversation);
                }

                // Sort by last message date (most recent first), then by unread count
                var sortedConversations = conversations
                    .OrderByDescending(c => c.HasUnreadMessages)
                    .ThenByDescending(c => c.LastMessageDate ?? DateTime.MinValue)
                    .ToList();

                _logger.LogInformation("✅ Retrieved {Count} conversations for user {UserId}", sortedConversations.Count, currentUserId);
                return sortedConversations;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error getting user conversations for {UserId}", currentUserId);
                return new List<ConversationViewModel>();
            }
        }

        public async Task<List<MessageViewModel>> GetConversationMessagesViewModelAsync(int currentUserId, int otherUserId)
        {
            try
            {
                _logger.LogInformation("📨 Getting conversation messages between {CurrentUserId} and {OtherUserId}", currentUserId, otherUserId);

                var messages = await _context.Messages
                    .Include(m => m.FromUser)
                    .Include(m => m.ToUser)
                    .Where(m => (m.FromUserId == currentUserId && m.ToUserId == otherUserId) ||
                               (m.FromUserId == otherUserId && m.ToUserId == currentUserId))
                    .OrderBy(m => m.SentDate)
                    .ToListAsync();

                var messageViewModels = messages.Select(m => new MessageViewModel
                {
                    MessageId = m.MessageId,
                    FromUserId = m.FromUserId,
                    ToUserId = m.ToUserId,
                    FromUserName = $"{m.FromUser.FirstName} {m.FromUser.LastName}".Trim(),
                    ToUserName = $"{m.ToUser.FirstName} {m.ToUser.LastName}".Trim(),
                    FromUserInitials = GetUserInitials(m.FromUser.FirstName, m.FromUser.LastName),
                    Subject = m.Subject,
                    Content = m.Content,
                    MessageType = m.MessageType,
                    IsRead = m.IsRead,
                    SentDate = m.SentDate,
                    ReadDate = m.ReadDate,
                    IsFromCurrentUser = m.FromUserId == currentUserId,
                    FormattedSentDate = m.SentDate.ToString("HH:mm"),
                    FormattedReadDate = m.ReadDate?.ToString("HH:mm") ?? "",
                    TimeAgo = GetTimeAgo(m.SentDate)
                }).ToList();

                _logger.LogInformation("✅ Retrieved {Count} messages in conversation", messageViewModels.Count);
                return messageViewModels;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error getting conversation messages view model");
                return new List<MessageViewModel>();
            }
        }

        public async Task<MessageStatsViewModel> GetMessageStatsAsync(int userId)
        {
            try
            {
                var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);

                var stats = new MessageStatsViewModel
                {
                    TotalMessages = await _context.Messages
                        .CountAsync(m => m.FromUserId == userId || m.ToUserId == userId),

                    UnreadMessages = await _context.Messages
                        .CountAsync(m => m.ToUserId == userId && !m.IsRead),

                    TodayMessages = await _context.Messages
                        .CountAsync(m => (m.FromUserId == userId || m.ToUserId == userId) &&
                                        m.SentDate.Date == DateTime.Today),

                    ThisWeekMessages = await _context.Messages
                        .CountAsync(m => (m.FromUserId == userId || m.ToUserId == userId) &&
                                        m.SentDate >= DateTime.Today.AddDays(-7)),

                    SentMessages = await _context.Messages
                        .CountAsync(m => m.FromUserId == userId),

                    ReceivedMessages = await _context.Messages
                        .CountAsync(m => m.ToUserId == userId)
                };

                // Get message type statistics
                var messageTypeStats = await _context.Messages
                    .Where(m => (m.FromUserId == userId || m.ToUserId == userId) &&
                               m.SentDate >= thirtyDaysAgo)
                    .GroupBy(m => m.MessageType)
                    .Select(g => new MessageTypeStatsViewModel
                    {
                        MessageType = g.Key,
                        Count = g.Count()
                    })
                    .ToListAsync();

                var totalMessages = messageTypeStats.Sum(s => s.Count);
                foreach (var stat in messageTypeStats)
                {
                    stat.Percentage = totalMessages > 0 ? Math.Round((double)stat.Count / totalMessages * 100, 1) : 0;
                }

                stats.MessageTypeStats = messageTypeStats;

                // Get daily statistics
                var dailyStats = await _context.Messages
                    .Where(m => (m.FromUserId == userId || m.ToUserId == userId) &&
                               m.SentDate >= thirtyDaysAgo)
                    .GroupBy(m => m.SentDate.Date)
                    .Select(g => new DailyMessageStatsViewModel
                    {
                        Date = g.Key,
                        MessageCount = g.Count(),
                        DateLabel = g.Key.ToString("MMM dd")
                    })
                    .OrderBy(s => s.Date)
                    .ToListAsync();

                stats.DailyStats = dailyStats;

                return stats;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error getting message statistics for user {UserId}", userId);
                return new MessageStatsViewModel();
            }
        }

        public async Task<List<Message>> GetNewMessagesSinceAsync(int userId, DateTime lastCheck)
        {
            try
            {
                return await _context.Messages
                    .Include(m => m.FromUser)
                    .Include(m => m.ToUser)
                    .Where(m => (m.FromUserId == userId || m.ToUserId == userId) &&
                               m.SentDate > lastCheck)
                    .OrderBy(m => m.SentDate)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error getting new messages since {LastCheck} for user {UserId}", lastCheck, userId);
                return new List<Message>();
            }
        }

        public async Task<bool> SendBulkMessageAsync(int fromUserId, List<int> toUserIds, string subject, string content, string messageType = "General")
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var messages = toUserIds.Select(toUserId => new Message
                {
                    FromUserId = fromUserId,
                    ToUserId = toUserId,
                    Subject = subject,
                    Content = content,
                    MessageType = messageType,
                    SentDate = DateTime.UtcNow,
                    IsRead = false
                }).ToList();

                if (messages.Any())
                {
                    _context.Messages.AddRange(messages);
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    _logger.LogInformation("📢 Sent bulk message from {FromUserId} to {RecipientCount} recipients",
                        fromUserId, messages.Count);
                }
                else
                {
                    await transaction.CommitAsync();
                }

                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "❌ Error sending bulk message from {FromUserId}", fromUserId);
                return false;
            }
        }

        #endregion

        #region Required Interface Methods

        public async Task<bool> SendReminderToEmployeesAsync(int fromUserId, string subject, string content)
        {
            try
            {
                var employees = await _context.Users
                    .Where(u => u.UserType != "Admin" && u.Status == "Active" && u.UserId != fromUserId)
                    .ToListAsync();

                if (!employees.Any())
                {
                    _logger.LogWarning("⚠️ No employees found to send reminder to");
                    return false;
                }

                var messages = employees.Select(employee => new Message
                {
                    FromUserId = fromUserId,
                    ToUserId = employee.UserId,
                    Subject = subject,
                    Content = content,
                    MessageType = "Reminder",
                    SentDate = DateTime.UtcNow,
                    IsRead = false
                }).ToList();

                _context.Messages.AddRange(messages);
                await _context.SaveChangesAsync();

                _logger.LogInformation("📢 Sent reminder from {FromUserId} to {EmployeeCount} employees", fromUserId, messages.Count);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error sending reminder to employees from {FromUserId}", fromUserId);
                return false;
            }
        }

        public async Task<bool> SendPromotionMessageAsync(int fromUserId, string subject, string content)
        {
            try
            {
                var users = await _context.Users
                    .Where(u => u.Status == "Active" && u.UserId != fromUserId)
                    .ToListAsync();

                if (!users.Any())
                {
                    _logger.LogWarning("⚠️ No users found to send promotion message to");
                    return false;
                }

                var messages = users.Select(user => new Message
                {
                    FromUserId = fromUserId,
                    ToUserId = user.UserId,
                    Subject = subject,
                    Content = content,
                    MessageType = "Promotion",
                    SentDate = DateTime.UtcNow,
                    IsRead = false
                }).ToList();

                _context.Messages.AddRange(messages);
                await _context.SaveChangesAsync();

                _logger.LogInformation("📢 Sent promotion message from {FromUserId} to {UserCount} users", fromUserId, messages.Count);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error sending promotion message from {FromUserId}", fromUserId);
                return false;
            }
        }

        #endregion

        #region Private Helper Methods

        private async Task<bool> IsUserOnlineAsync(int userId)
        {
            try
            {
                // Check if user was active in the last 5 minutes
                var onlineThreshold = DateTime.UtcNow.AddMinutes(-5);
                var user = await _context.Users.FindAsync(userId);

                return user != null &&
                       user.Status == "Active" &&
                       user.UpdatedAt > onlineThreshold;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error checking online status for user {UserId}", userId);
                return false;
            }
        }

        private string GetLastSeenFormatted(DateTime lastSeen)
        {
            try
            {
                var timeDiff = DateTime.UtcNow - lastSeen;

                if (timeDiff.TotalMinutes < 5)
                    return "Just now";
                else if (timeDiff.TotalMinutes < 60)
                    return $"{(int)timeDiff.TotalMinutes}m ago";
                else if (timeDiff.TotalHours < 24)
                    return $"{(int)timeDiff.TotalHours}h ago";
                else if (timeDiff.TotalDays < 7)
                    return $"{(int)timeDiff.TotalDays}d ago";
                else
                    return lastSeen.ToString("MMM dd");
            }
            catch
            {
                return "Unknown";
            }
        }

        private string GetUserInitials(string firstName, string lastName)
        {
            try
            {
                var first = !string.IsNullOrEmpty(firstName) ? firstName.Substring(0, 1).ToUpper() : "U";
                var last = !string.IsNullOrEmpty(lastName) ? lastName.Substring(0, 1).ToUpper() : "N";
                return first + last;
            }
            catch
            {
                return "UN";
            }
        }

        private string GetTimeAgo(DateTime dateTime)
        {
            try
            {
                var timeDiff = DateTime.UtcNow - dateTime;

                if (timeDiff.TotalSeconds < 60)
                    return "Just now";
                else if (timeDiff.TotalMinutes < 60)
                    return $"{(int)timeDiff.TotalMinutes}m ago";
                else if (timeDiff.TotalHours < 24)
                    return $"{(int)timeDiff.TotalHours}h ago";
                else if (timeDiff.TotalDays < 7)
                    return $"{(int)timeDiff.TotalDays}d ago";
                else
                    return dateTime.ToString("MMM dd");
            }
            catch
            {
                return "Unknown";
            }
        }

        public async Task<bool> ValidateMessagePermissionsAsync(int fromUserId, int toUserId)
        {
            try
            {
                var fromUser = await _context.Users.FindAsync(fromUserId);
                var toUser = await _context.Users.FindAsync(toUserId);

                if (fromUser == null || toUser == null)
                    return false;

                // Basic validation - both users must be active
                if (fromUser.Status != "Active" || toUser.Status != "Active")
                    return false;

                // Additional business rules can be added here
                // For example: employees can only message managers/admins, etc.

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error validating message permissions between {FromUserId} and {ToUserId}", fromUserId, toUserId);
                return false;
            }
        }

        public async Task<List<User>> GetAvailableRecipientsAsync(int currentUserId)
        {
            try
            {
                return await _context.Users
                    .Where(u => u.UserId != currentUserId && u.Status == "Active")
                    .OrderBy(u => u.FirstName)
                    .ThenBy(u => u.LastName)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error getting available recipients for user {UserId}", currentUserId);
                return new List<User>();
            }
        }

        public async Task<bool> UpdateMessageStatusAsync(int messageId, bool isRead)
        {
            try
            {
                var message = await _context.Messages.FindAsync(messageId);
                if (message == null)
                    return false;

                message.IsRead = isRead;
                if (isRead && !message.ReadDate.HasValue)
                {
                    message.ReadDate = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error updating message status for {MessageId}", messageId);
                return false;
            }
        }

        #endregion
    }
}