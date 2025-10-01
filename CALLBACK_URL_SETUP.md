# M-Pesa Callback URL Setup Guide

## ✅ Fixed Issues

1. **Removed `/admin/` prefix from callback URLs** ✅
2. **Added `PublicDomain` configuration** ✅
3. **Corrected callback endpoints** ✅

---

## 🔧 Current Configuration

### Development (Using Ngrok)
```json
"MpesaSettings": {
  "Shortcode": "3560959",
  "TillNumber": "6509715",
  "CallbackUrl": "https://125023d8524c.ngrok-free.app/api/mpesa/callback",
  "ConfirmationUrl": "https://125023d8524c.ngrok-free.app/api/mpesa/c2b/confirmation",
  "ValidationUrl": "https://125023d8524c.ngrok-free.app/api/mpesa/c2b/validation",
  "PublicDomain": "https://125023d8524c.ngrok-free.app"
}
```

### Production (When Deployed)
```json
"MpesaSettings": {
  "Shortcode": "3560959",
  "TillNumber": "6509715",
  "CallbackUrl": "https://yourdomain.com/api/mpesa/callback",
  "ConfirmationUrl": "https://yourdomain.com/api/mpesa/c2b/confirmation",
  "ValidationUrl": "https://yourdomain.com/api/mpesa/c2b/validation",
  "PublicDomain": "https://yourdomain.com"
}
```

---

## 📍 Correct Callback Endpoints

### 1. STK Push Callback
**URL:** `/api/mpesa/callback`  
**Full URL:** `https://125023d8524c.ngrok-free.app/api/mpesa/callback`

**What it receives:**
- Payment success/failure
- M-Pesa receipt number
- Amount paid
- Customer phone number

**Status codes:**
- `0` = Success
- `1032` = User cancelled
- `1` = Insufficient funds
- `2001` = Wrong PIN

### 2. C2B Confirmation
**URL:** `/api/mpesa/c2b/confirmation`  
**Full URL:** `https://125023d8524c.ngrok-free.app/api/mpesa/c2b/confirmation`

**What it receives:**
- QR code payments
- Manual till payments
- Transaction ID
- Amount
- Customer phone

### 3. C2B Validation
**URL:** `/api/mpesa/c2b/validation`  
**Full URL:** `https://125023d8524c.ngrok-free.app/api/mpesa/c2b/validation`

**What it does:**
- Pre-validates payment before processing
- Can reject invalid payments
- Currently accepts all payments

---

## 🚀 Setup Steps

### Step 1: Restart Application
```
1. Stop (Shift+F5)
2. Start (F5)
```

### Step 2: Verify Ngrok URL
```
Check ngrok console shows:
https://125023d8524c.ngrok-free.app -> https://localhost:5001
```

### Step 3: Test Callback Endpoint
```
Open browser:
https://125023d8524c.ngrok-free.app/api/mpesa/callback

Should show: Method Not Allowed (405)
This is correct - it only accepts POST requests
```

### Step 4: Register C2B URLs
```
POST https://localhost:5001/api/MpesaTest/register-c2b

Expected response:
{
  "success": true,
  "message": "C2B URLs registered successfully"
}
```

### Step 5: Test Payment
```
1. Add item to cart
2. Select M-Pesa
3. Enter phone: 758024400
4. Complete payment
5. Check logs for callback
```

---

## 🔍 Debugging Callback Issues

### Check if Callback is Received

**Application Logs should show:**
```
📥 MPESA Callback received
💳 MPESA Receipt: QGH7SK61SU
✅ Payment successful for sale: SAL20250930...
```

**If you DON'T see these logs:**
1. Callback URL is wrong
2. Ngrok is not running
3. M-Pesa cannot reach your server

### Test Callback Manually

**Using Postman:**
```
POST https://125023d8524c.ngrok-free.app/api/mpesa/callback
Content-Type: application/json

{
  "Body": {
    "stkCallback": {
      "MerchantRequestID": "test123",
      "CheckoutRequestID": "ws_CO_30092025160000",
      "ResultCode": 0,
      "ResultDesc": "Success",
      "CallbackMetadata": {
        "Item": [
          { "Name": "Amount", "Value": 1000 },
          { "Name": "MpesaReceiptNumber", "Value": "TEST123" },
          { "Name": "PhoneNumber", "Value": "254758024400" }
        ]
      }
    }
  }
}
```

**Expected Response:**
```json
{
  "ResultCode": 0,
  "ResultDesc": "Success"
}
```

---

## 📊 Payment Status Flow

### Success Flow
```
1. STK Push sent
   Status: STK_SENT
   
2. Customer enters PIN
   Status: STK_SENT (still polling)
   
3. Callback received (ResultCode: 0)
   Status: Completed
   MpesaReceiptNumber: QGH7SK61SU
   
4. Receipt generated
   Shows M-Pesa code
```

### Cancelled Flow
```
1. STK Push sent
   Status: STK_SENT
   
2. Customer cancels
   Status: STK_SENT (still polling)
   
3. Callback received (ResultCode: 1032)
   Status: Failed
   ErrorMessage: "User cancelled transaction"
   
4. Error shown to user
```

### Wrong PIN Flow
```
1. STK Push sent
   Status: STK_SENT
   
2. Customer enters wrong PIN
   Status: STK_SENT (still polling)
   
3. Callback received (ResultCode: 2001)
   Status: Failed
   ErrorMessage: "Wrong PIN entered"
   
4. Error shown to user
```

---

## ⚠️ Common Issues

### Issue 1: Status Stays "STK_SENT"
**Cause:** Callback never received

**Solutions:**
1. Check ngrok is running
2. Verify callback URL has no `/admin/` prefix
3. Test callback endpoint manually
4. Check application logs

### Issue 2: "Unexpected token '<'" Error
**Cause:** Endpoint returning HTML instead of JSON

**Solutions:**
1. Verify endpoint exists: `/api/mpesa/callback`
2. Check route is correct (no `/admin/`)
3. Restart application
4. Test with Postman

### Issue 3: Callback Received but Sale Not Updated
**Cause:** CheckoutRequestID mismatch

**Solutions:**
1. Check MpesaTransaction table for CheckoutRequestID
2. Verify Sale.SaleId matches
3. Check logs for matching errors

---

## 🎯 When Ngrok URL Changes

### Quick Update Process
```
1. Get new ngrok URL
2. Update appsettings.Development.json:
   - CallbackUrl
   - ConfirmationUrl
   - ValidationUrl
   - PublicDomain
3. Restart application
4. Re-register C2B URLs
```

### Script to Update (PowerShell)
```powershell
# Get ngrok URL
$ngrokUrl = "https://NEW-URL.ngrok-free.app"

# Update config (manual for now)
# Then restart app
```

---

## 🌐 Production Deployment

### When You Deploy to Production

**Step 1: Update appsettings.json**
```json
"CallbackUrl": "https://yourdomain.com/api/mpesa/callback",
"ConfirmationUrl": "https://yourdomain.com/api/mpesa/c2b/confirmation",
"ValidationUrl": "https://yourdomain.com/api/mpesa/c2b/validation",
"PublicDomain": "https://yourdomain.com"
```

**Step 2: Register URLs with Safaricom**
```
POST https://yourdomain.com/api/MpesaTest/register-c2b
```

**Step 3: Test**
```
1. Test STK Push
2. Test QR Code
3. Verify callbacks received
4. Check receipts show M-Pesa codes
```

---

## ✅ Verification Checklist

- [ ] Ngrok running on correct port (5001)
- [ ] Callback URLs have NO `/admin/` prefix
- [ ] appsettings.Development.json updated
- [ ] Application restarted
- [ ] C2B URLs registered
- [ ] Test callback endpoint returns 405
- [ ] STK Push test successful
- [ ] Callback received in logs
- [ ] Sale status changes to "Completed"
- [ ] Receipt shows M-Pesa code

---

**Callback URLs are now correctly configured!** 🎉

**Current Ngrok URL:** `https://125023d8524c.ngrok-free.app`  
**Callback Endpoint:** `/api/mpesa/callback` (NO /admin/ prefix)
