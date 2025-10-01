# M-Pesa Callback - Final Fix

## 🔴 Two Issues Found

### Issue 1: Migration Error (Blocking App Startup)
```
Column names in each table must be unique. Column name 'CompletedDate' in table 'PurchaseRequests' is specified more than once.
```

### Issue 2: Callback Parsing Error
```
KeyNotFoundException: The given key was not present in the dictionary.
at item.GetProperty("Value")
```

---

## ✅ Fixes Applied

### Fix 1: Added Safe Property Access
Changed from:
```csharp
var items = metadata.GetProperty("Item");  // ❌ Crashes if missing
foreach (var item in items.EnumerateArray())
{
    var name = item.GetProperty("Name").GetString();  // ❌ Crashes if missing
    var value = item.GetProperty("Value");  // ❌ Crashes if missing
}
```

To:
```csharp
if (metadata.TryGetProperty("Item", out var itemsProperty))  // ✅ Safe
{
    foreach (var item in itemsProperty.EnumerateArray())
    {
        if (!item.TryGetProperty("Name", out var nameProperty) ||  // ✅ Safe
            !item.TryGetProperty("Value", out var value))
        {
            continue; // Skip invalid items
        }
        var name = nameProperty.GetString();
    }
}
```

### Fix 2: Added Full Callback Logging
```csharp
_logger.LogInformation("📥 Full callback data: {CallbackData}", callbackData.ToString());
```

This will show exactly what Safaricom is sending.

---

## 🚀 Steps to Fix

### Step 1: Fix Migration Error

**Option A: Remove the problematic migration**
```powershell
cd C:\Users\Denno\source\repos\PixelSolution\PixelSolution
dotnet ef migrations remove
```

**Option B: Run SQL to check/add column**
```sql
IF NOT EXISTS (SELECT * FROM sys.columns 
               WHERE object_id = OBJECT_ID(N'[dbo].[PurchaseRequests]') 
               AND name = 'CompletedDate')
BEGIN
    ALTER TABLE [PurchaseRequests] ADD [CompletedDate] datetime2 NULL;
END
```

### Step 2: Restart Application
```
Stop (Shift+F5)
Start (F5)
```

### Step 3: Test Payment
```
1. Add item (KSH 150)
2. Select M-Pesa
3. Enter phone: 758024400
4. Complete payment
5. Enter correct PIN
```

### Step 4: Check Logs
```
Look for:
📥 MPESA Callback received
📥 Full callback data: {...}  ← This will show what Safaricom sent
💳 MPESA Receipt: TJ1LL68ZST
✅ Payment successful
```

---

## 🔍 What to Look For in Logs

### Successful Callback:
```json
{
  "Body": {
    "stkCallback": {
      "MerchantRequestID": "...",
      "CheckoutRequestID": "...",
      "ResultCode": 0,
      "ResultDesc": "Success",
      "CallbackMetadata": {
        "Item": [
          {"Name": "Amount", "Value": 150},
          {"Name": "MpesaReceiptNumber", "Value": "TJ1LL68ZST"},
          {"Name": "TransactionDate", "Value": 20251001175000},
          {"Name": "PhoneNumber", "Value": 254758024400}
        ]
      }
    }
  }
}
```

### Failed Callback (User Cancelled):
```json
{
  "Body": {
    "stkCallback": {
      "MerchantRequestID": "...",
      "CheckoutRequestID": "...",
      "ResultCode": 1032,
      "ResultDesc": "Request cancelled by user"
    }
  }
}
```

### Wrong PIN:
```json
{
  "Body": {
    "stkCallback": {
      "MerchantRequestID": "...",
      "CheckoutRequestID": "...",
      "ResultCode": 2001,
      "ResultDesc": "Wrong PIN"
    }
  }
}
```

---

## ⚠️ Common Issues

### If Callback Still Not Received:

1. **Check ngrok is running**
   ```
   http://127.0.0.1:4040
   Look for POST /api/mpesa/callback
   ```

2. **Test callback URL**
   ```
   https://125023d8524c.ngrok-free.app/api/mpesa/callback
   Should show: 405 Method Not Allowed ✅
   ```

3. **Check application logs**
   ```
   Look for: "📥 MPESA Callback received"
   If missing → Callback not reaching server
   ```

4. **Verify callback URL in config**
   ```json
   "CallbackUrl": "https://125023d8524c.ngrok-free.app/api/mpesa/callback"
   ```
   No `/admin/` prefix!

---

## ✅ Success Indicators

**When everything works:**

1. **Application starts without errors** ✅
2. **Logs show:**
   ```
   📥 MPESA Callback received
   📥 Full callback data: {...}
   💳 MPESA Receipt: TJ1LL68ZST
   💰 Amount received: 150
   ✅ Payment successful
   ```
3. **Database updated:**
   ```
   Sales.Status = "Completed"
   Sales.MpesaReceiptNumber = "TJ1LL68ZST"
   ```
4. **Spinner closes** ✅
5. **Receipt generated** ✅

---

## 🎯 Next Steps

1. **Fix migration** (remove or run SQL)
2. **Restart app**
3. **Test payment**
4. **Check logs for full callback data**
5. **Share logs if still failing**

---

**The callback handler is now more robust and will log exactly what Safaricom sends!** 🎉
