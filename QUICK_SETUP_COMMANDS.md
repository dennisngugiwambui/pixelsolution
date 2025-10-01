# Quick Setup Commands - M-Pesa C2B Implementation

## 🚀 Run These Commands in Order

### 1. Apply Database Changes

**Option A: Run SQL Script (RECOMMENDED - No migration conflicts)**
```sql
-- Open SQL Server Management Studio or Azure Data Studio
-- Run file: ADD_MPESA_RECEIPT_COLUMN.sql
-- Or copy-paste this:

ALTER TABLE [Sales] ADD [MpesaReceiptNumber] NVARCHAR(50) NULL;
CREATE INDEX IX_Sales_MpesaReceiptNumber ON [Sales] ([MpesaReceiptNumber]);
```

**Option B: Use EF Migrations (If you fix conflicts first)**
```powershell
# Only if you've removed duplicate CompletedDate migrations
cd C:\Users\Denno\source\repos\PixelSolution\PixelSolution
dotnet ef database update
```

---

### 2. Start Ngrok
```powershell
cd C:\ngrok
.\ngrok http https://localhost:5001
```

**Copy the URL** (e.g., `https://abc123.ngrok-free.app`)

---

### 3. Update Configuration

Edit `appsettings.Development.json`:
```json
"CallbackUrl": "https://abc123.ngrok-free.app/api/mpesa/callback",
"ConfirmationUrl": "https://abc123.ngrok-free.app/api/mpesa/c2b/confirmation",
"ValidationUrl": "https://abc123.ngrok-free.app/api/mpesa/c2b/validation"
```

---

### 4. Restart Application
```
1. Stop (Shift+F5)
2. Start (F5)
```

---

### 5. Register C2B URLs (One Time Only)

**Using Postman or Browser:**
```
POST https://localhost:5001/api/MpesaTest/register-c2b
```

**Expected Response:**
```json
{
  "success": true,
  "message": "C2B URLs registered successfully",
  "data": {
    "ResponseCode": "0",
    "ResponseDescription": "Success"
  }
}
```

---

### 6. Test Payment

**Test STK Push:**
```
1. Add item to cart
2. Select M-Pesa
3. Enter phone: 758024400
4. Complete payment
5. Check receipt for M-Pesa code
```

**Test QR Code:**
```
1. Generate QR code
2. Scan with M-Pesa app
3. Enter PIN
4. Check receipt for M-Pesa code
```

---

## 🔍 Verification Commands

### Check Database Column Exists
```sql
SELECT COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'Sales' AND COLUMN_NAME = 'MpesaReceiptNumber';
```

### Check Recent M-Pesa Sales
```sql
SELECT TOP 10 
    SaleNumber, 
    MpesaReceiptNumber, 
    TotalAmount, 
    PaymentMethod,
    Status,
    SaleDate
FROM Sales 
WHERE PaymentMethod = 'M-Pesa'
ORDER BY SaleDate DESC;
```

### Check Pending M-Pesa Sales
```sql
SELECT * FROM Sales 
WHERE PaymentMethod = 'M-Pesa' 
AND Status = 'Pending'
ORDER BY SaleDate DESC;
```

---

## ⚠️ Troubleshooting

### If Migration Fails with "CompletedDate" Error:
**Use SQL Script instead:**
```sql
-- Just run ADD_MPESA_RECEIPT_COLUMN.sql
-- This avoids migration conflicts
```

### If C2B Confirmation Not Received:
```
1. Check ngrok is running
2. Verify callback URLs in config
3. Re-register C2B URLs
4. Check application logs
```

### If M-Pesa Code Not Showing on Receipt:
```
1. Check database has MpesaReceiptNumber column
2. Verify sale has PaymentMethod = "M-Pesa"
3. Check MpesaReceiptNumber is not null
4. Restart application
```

---

## ✅ Success Checklist

- [ ] Database column added (`MpesaReceiptNumber`)
- [ ] Ngrok running
- [ ] Callback URLs updated in config
- [ ] Application restarted
- [ ] C2B URLs registered with Safaricom
- [ ] STK Push test successful
- [ ] QR Code test successful
- [ ] Receipt shows M-Pesa code

---

**All commands ready to execute!** 🎉
