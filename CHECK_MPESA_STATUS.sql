-- Check Recent M-Pesa Transactions
SELECT TOP 10
    MpesaTransactionId,
    CheckoutRequestId,
    MerchantRequestId,
    PhoneNumber,
    Amount,
    Status,
    ErrorMessage,
    MpesaReceiptNumber,
    CreatedAt,
    CompletedAt
FROM MpesaTransactions
ORDER BY CreatedAt DESC;

-- Check if any transactions succeeded
SELECT 
    Status,
    COUNT(*) as Count,
    MAX(CreatedAt) as LastOccurrence
FROM MpesaTransactions
GROUP BY Status;

-- Check recent sales with M-Pesa payment
SELECT TOP 10
    s.SaleId,
    s.SaleNumber,
    s.PaymentMethod,
    s.Status as SaleStatus,
    s.TotalAmount,
    s.MpesaReceiptNumber,
    mt.Status as MpesaStatus,
    mt.ErrorMessage,
    s.SaleDate
FROM Sales s
LEFT JOIN MpesaTransactions mt ON s.SaleId = mt.SaleId
WHERE s.PaymentMethod = 'M-Pesa'
ORDER BY s.SaleDate DESC;
