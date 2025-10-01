# M-Pesa Credential Issue - URGENT FIX

## 🔴 Problem Identified

**Error from Safaricom:**
- `ResultCode: 1` - "The initiator information is invalid"
- `ResultCode: 1` - "This request is not permitted according to product..."

**Root Cause:**
Your STK Push is using **Shortcode 3560959** but the **Passkey** might not match this shortcode.

---

## 🔍 What's Happening

### Current Configuration:
```json
"Shortcode": "3560959",      // Paybill for STK Push
"TillNumber": "6509715",      // Till for QR/C2B
"Passkey": "fc087de2729c7ff67b2b2b3aacc2068039fc56284c676d56679ef86f70640d8d"
```

### The Issue:
The **Passkey** must match the **Shortcode** you're using for STK Push.

**Question:** Is this Passkey for Shortcode **3560959** or Till **6509715**?

---

## ✅ Solution Options

### Option 1: Get Correct Passkey for Shortcode 3560959

**You need:**
1. Go to Safaricom Daraja Portal
2. Select your **Paybill (3560959)**
3. Get the **Lipa Na M-Pesa Online Passkey**
4. Update `appsettings.json`

**Expected format:**
```json
"Shortcode": "3560959",
"Passkey": "YOUR_PAYBILL_PASSKEY_HERE"
```

---

### Option 2: Use Till Number for STK Push (If Till Supports STK)

**If your Till (6509715) supports STK Push:**
```json
"Shortcode": "6509715",      // Use till for STK
"TillNumber": "6509715",      // Same for QR/C2B
"Passkey": "fc087de2729c7ff67b2b2b3aacc2068039fc56284c676d56679ef86f70640d8d"
```

**Note:** Not all tills support STK Push. Check with Safaricom.

---

## 🔧 How to Get Correct Credentials

### Step 1: Login to Daraja Portal
```
https://developer.safaricom.co.ke/
```

### Step 2: Select Your App
```
Go to: My Apps → Select your app
```

### Step 3: Get Credentials

**For Paybill (3560959):**
```
1. Click on "Lipa Na M-Pesa Online"
2. Find "Business Short Code": 3560959
3. Find "Passkey": [Copy this]
4. This is your STK Push Passkey
```

**For Till (6509715):**
```
1. Click on "Customer to Business (C2B)"
2. Find "Short Code": 6509715
3. This is for QR codes and manual payments
4. May or may not have STK Push capability
```

---

## 🎯 Quick Test

### Test if Credentials are Correct

**Run this SQL to check what's being sent:**
```sql
SELECT TOP 5 
    MerchantRequestId,
    CheckoutRequestId,
    Status,
    ResultCode,
    ErrorMessage,
    CreatedAt
FROM MpesaTransactions
ORDER BY CreatedAt DESC;
```

**If you see:**
- `ResultCode: 0` = Success ✅
- `ResultCode: 1` = Invalid credentials ❌
- `ResultCode: 1032` = User cancelled ✅ (credentials work)
- `ResultCode: 2001` = Wrong PIN ✅ (credentials work)

---

## 📋 Credential Checklist

### For STK Push (Lipa Na M-Pesa Online):
- [ ] Business Short Code (Paybill)
- [ ] Passkey (from Daraja Portal)
- [ ] Consumer Key
- [ ] Consumer Secret
- [ ] Callback URL (public, accessible)

### For C2B (QR Code / Manual Till):
- [ ] Till Number
- [ ] Confirmation URL
- [ ] Validation URL
- [ ] Consumer Key
- [ ] Consumer Secret

---

## ⚠️ Common Mistakes

### Mistake 1: Using Till Passkey for Paybill
```json
// WRONG
"Shortcode": "3560959",  // Paybill
"Passkey": "till_passkey"  // Till passkey ❌
```

### Mistake 2: Using Paybill for Till Operations
```json
// WRONG
"TillNumber": "3560959"  // This is a paybill, not a till ❌
```

### Mistake 3: Mixing Sandbox and Production
```json
// WRONG
"Shortcode": "174379",  // Sandbox
"BaseUrl": "https://api.safaricom.co.ke"  // Production ❌
```

---

## 🚀 Immediate Action Required

### Step 1: Verify Your Credentials

**Answer these questions:**
1. Is **3560959** a Paybill or Till?
2. Is **6509715** a Paybill or Till?
3. Which one is registered for STK Push?
4. Do you have the correct Passkey for the STK Push shortcode?

### Step 2: Update Configuration

**Once you have the correct Passkey:**
```json
"MpesaSettings": {
  "ConsumerKey": "4aEia8VMAGLQU28ZoorLQRZtMutc6A6GyGXMq9HYoNFyXNOY",
  "ConsumerSecret": "wMdKEDv2y2JZQ8ZdN1TAn4MgxbuILwrNsOu4ywi6QcVZJw4BrlEclAcW4XSduSlw",
  "Shortcode": "3560959",  // For STK Push
  "TillNumber": "6509715",  // For QR/C2B
  "Passkey": "CORRECT_PASSKEY_FOR_3560959",  // ← UPDATE THIS
  "CallbackUrl": "https://125023d8524c.ngrok-free.app/api/mpesa/callback",
  "BaseUrl": "https://api.safaricom.co.ke",
  "IsSandbox": false
}
```

### Step 3: Test Again
```
1. Restart application
2. Test STK Push
3. Check MpesaTransactions table
4. Look for ResultCode: 0 (success)
```

---

## 🔍 Debug Information

### Check Application Logs for:
```
🔐 STK Push params - Shortcode: 3560959, Timestamp: ...
📱 Initiating STK Push for 254758024400, Amount: 1000
🔑 Access token obtained
```

### Check M-Pesa Response:
```
If you see:
- "initiator information is invalid" = Wrong Passkey
- "not permitted according to product" = Wrong Shortcode or not enabled
- "Success" = Credentials correct ✅
```

---

## 📞 Contact Safaricom

If you're still stuck:

**Safaricom Support:**
- Email: apisupport@safaricom.co.ke
- Phone: 0722 000 000

**Ask them:**
1. "Which shortcode should I use for STK Push?"
2. "What is the correct Passkey for STK Push?"
3. "Is my Till (6509715) enabled for STK Push?"

---

## ✅ Success Indicators

**When credentials are correct, you'll see:**
```
ResultCode: 0
ResultDesc: "Success"
MpesaReceiptNumber: "QGH7SK61SU"
```

**Not:**
```
ResultCode: 1
ErrorMessage: "The initiator information is invalid"
```

---

**Get the correct Passkey from Daraja Portal and update your config!** 🎯

The issue is NOT with your code - it's with the M-Pesa credentials configuration.
