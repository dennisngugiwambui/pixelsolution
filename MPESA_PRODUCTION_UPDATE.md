# M-Pesa Production Update - Complete Guide

## 🎯 What Changed

### 1. **Production URLs** ✅
- Changed from **Sandbox** (`https://sandbox.safaricom.co.ke`) to **Production** (`https://api.safaricom.co.ke`)
- Updated in both `appsettings.json` and `appsettings.Development.json`

### 2. **Enhanced Token Validation** ✅
- Token is now validated before use
- If invalid, system automatically generates a new token
- Proper token structure validation (length > 20 characters)
- Invalid tokens are deactivated in database

### 3. **QR Code Generation** ✅
- New API endpoint to generate M-Pesa QR codes
- Customers can scan QR code to pay
- Dynamic QR codes with amount and merchant info

### 4. **C2B URL Registration** ✅
- Register validation and confirmation URLs with Safaricom
- Handle C2B payment notifications
- Validation endpoint for pre-payment checks
- Confirmation endpoint for payment completion

---

## 📋 Configuration Changes

### Updated `appsettings.json` and `appsettings.Development.json`:

```json
"MpesaSettings": {
  "ConsumerKey": "4aEia8VMAGLQU28ZoorLQRZtMutc6A6GyGXMq9HYoNFyXNOY",
  "ConsumerSecret": "wMdKEDv2y2JZQ8ZdN1TAn4MgxbuILwrNsOu4ywi6QcVZJw4BrlEclAcW4XSduSlw",
  "Shortcode": "3560959",
  "Passkey": "fc087de2729c7ff67b2b2b3aacc2068039fc56284c676d56679ef86f70640d8d",
  "CallbackUrl": "https://your-domain.com/api/mpesa/callback",
  "ConfirmationUrl": "https://your-domain.com/api/mpesa/c2b/confirmation",
  "ValidationUrl": "https://your-domain.com/api/mpesa/c2b/validation",
  "BaseUrl": "https://api.safaricom.co.ke",  // ✅ PRODUCTION URL
  "IsSandbox": false  // ✅ PRODUCTION MODE
}
```

**Important:** Replace `https://your-domain.com` with your actual ngrok URL or production domain!

---

## 🔧 New Features Implemented

### 1. Enhanced Token Management

**Location:** `Services/MpesaService.cs` - `GetAccessTokenAsync()`

**What it does:**
1. Checks database for valid token
2. Validates token structure (must be > 20 characters)
3. If invalid, deactivates old token
4. Generates fresh token from Safaricom
5. Saves new token to database

**Benefits:**
- Prevents "Invalid Access Token" errors
- Automatic token refresh
- Reduced API calls (uses cached tokens)

### 2. QR Code Generation

**API Endpoint:** `POST /api/MpesaTest/test-qr`

**Request Body:**
```json
{
  "MerchantName": "PixelSolution",
  "RefNo": "INV-001",
  "Amount": 100,
  "TrxCode": "BG",
  "Size": "300"
}
```

**Response:**
```json
{
  "success": true,
  "message": "QR Code generated successfully",
  "data": {
    "QRCode": "base64_encoded_qr_code_image",
    "ResponseCode": "00",
    "ResponseDescription": "Success"
  }
}
```

**How to use:**
1. Generate QR code with amount
2. Display QR code to customer
3. Customer scans with M-Pesa app
4. Payment processed automatically

### 3. C2B URL Registration

**API Endpoint:** `POST /api/MpesaTest/register-c2b`

**What it does:**
- Registers your validation and confirmation URLs with Safaricom
- Enables C2B payment notifications
- Required for Buy Goods transactions

**Callback Endpoints Created:**
1. **Validation:** `POST /api/mpesa/c2b/validation`
   - Called before payment is processed
   - You can validate account numbers, etc.
   - Return `ResultCode: 0` to accept, `1` to reject

2. **Confirmation:** `POST /api/mpesa/c2b/confirmation`
   - Called after payment is completed
   - Save payment details to database
   - Trigger business logic

---

## 🚀 How to Use New Features

### Step 1: Update Callback URLs

**For ngrok (development):**
```json
"CallbackUrl": "https://your-ngrok-url.ngrok-free.app/api/mpesa/callback",
"ConfirmationUrl": "https://your-ngrok-url.ngrok-free.app/api/mpesa/c2b/confirmation",
"ValidationUrl": "https://your-ngrok-url.ngrok-free.app/api/mpesa/c2b/validation"
```

**For production:**
```json
"CallbackUrl": "https://yourdomain.com/api/mpesa/callback",
"ConfirmationUrl": "https://yourdomain.com/api/mpesa/c2b/confirmation",
"ValidationUrl": "https://yourdomain.com/api/mpesa/c2b/validation"
```

### Step 2: Register C2B URLs

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
    "OriginatorCoversationID": "...",
    "ResponseCode": "0",
    "ResponseDescription": "Success"
  }
}
```

**⚠️ Important:** You only need to do this once per shortcode!

### Step 3: Generate QR Code (Optional)

**Using Postman:**
```
POST https://localhost:5001/api/MpesaTest/test-qr
Content-Type: application/json

{
  "MerchantName": "PixelSolution",
  "RefNo": "SALE-001",
  "Amount": 100,
  "Size": "300"
}
```

**Response includes base64 QR code image** - display it to customers!

### Step 4: Test Token Validation

**Clear old tokens:**
```sql
DELETE FROM MpesaTokens;
```

**Test token generation:**
```
GET https://localhost:5001/api/MpesaTest/test-token
```

**You should see:**
```json
{
  "success": true,
  "message": "Token generated successfully",
  "tokenLength": 52,
  "tokenPreview": "eyJ0eXAiOiJKV1QiLCJh...",
  "timestamp": "2025-09-30T12:00:00Z"
}
```

---

## 🔍 Token Validation Flow

```
1. Check database for valid token
   ↓
2. Is token found?
   ↓ YES                    ↓ NO
3. Validate structure    7. Generate new token
   ↓                        ↓
4. Is valid?             8. Save to database
   ↓ YES    ↓ NO           ↓
5. Use it  6. Deactivate  9. Return new token
              ↓
           7. Generate new
```

---

## 📱 QR Code Payment Flow

```
1. Generate QR code with amount
   ↓
2. Display QR to customer
   ↓
3. Customer scans with M-Pesa app
   ↓
4. Customer enters PIN
   ↓
5. Payment processed
   ↓
6. C2B Validation called (optional)
   ↓
7. C2B Confirmation called
   ↓
8. Save payment to database
```

---

## 🧪 Testing Endpoints

### 1. Test Token Generation
```
GET /api/MpesaTest/test-token
```

### 2. Clear Tokens
```
POST /api/MpesaTest/clear-tokens
```

### 3. Generate QR Code
```
POST /api/MpesaTest/test-qr
Body: { "Amount": 100, "MerchantName": "PixelSolution" }
```

### 4. Register C2B URLs
```
POST /api/MpesaTest/register-c2b
```

### 5. Test STK Push
```
POST /api/MpesaTest/test-laravel-stk
Body: { "PhoneNumber": "254758024400", "Amount": 10 }
```

---

## ⚠️ Important Notes

### 1. Credentials Must Be Correct
- **Consumer Key** and **Consumer Secret** must match your shortcode **3560959**
- Login to https://developer.safaricom.co.ke to verify
- Copy credentials exactly (no extra spaces)

### 2. Production vs Sandbox
- **Production URL:** `https://api.safaricom.co.ke`
- **Sandbox URL:** `https://sandbox.safaricom.co.ke`
- Make sure you're using production credentials with production URL

### 3. Callback URLs
- Must be HTTPS (not HTTP)
- Must be publicly accessible
- Use ngrok for local development
- Update URLs when ngrok restarts (free plan)

### 4. C2B Registration
- Only needs to be done once per shortcode
- If you change URLs, re-register
- Contact Safaricom if validation is required (optional feature)

---

## 🐛 Troubleshooting

### Issue: "Invalid Access Token" (404.001.03)

**Solution:**
1. Verify credentials on Safaricom portal
2. Make sure app is ACTIVE
3. Clear tokens: `DELETE FROM MpesaTokens;`
4. Restart application
5. Check logs for token generation details

### Issue: QR Code Generation Fails

**Solution:**
1. Check token is valid
2. Verify shortcode is correct
3. Ensure amount is valid (> 0)
4. Check logs for API response

### Issue: C2B URLs Not Receiving Callbacks

**Solution:**
1. Verify URLs are publicly accessible
2. Check ngrok is running
3. Test endpoints manually
4. Check Safaricom has registered your URLs

### Issue: Token Keeps Expiring

**Solution:**
- Tokens expire after 1 hour
- System automatically generates new ones
- Check database for token expiry times
- Ensure 5-minute buffer is working

---

## 📊 Monitoring

### Check Token Status
```sql
SELECT * FROM MpesaTokens 
WHERE IsActive = 1 
ORDER BY CreatedAt DESC;
```

### Check Recent Transactions
```sql
SELECT * FROM MpesaTransactions 
ORDER BY CreatedAt DESC 
LIMIT 10;
```

### View Application Logs
- Check Visual Studio Output window
- Look for emoji indicators:
  - 🔍 = Checking/Searching
  - ✅ = Success
  - ❌ = Error
  - 📱 = STK Push
  - 💾 = Database operation
  - 🔑 = Token operation

---

## ✅ Production Checklist

Before going live:
- [ ] Correct Consumer Key and Secret
- [ ] Production URL (`https://api.safaricom.co.ke`)
- [ ] IsSandbox set to `false`
- [ ] Callback URLs point to production domain
- [ ] C2B URLs registered with Safaricom
- [ ] Tokens cleared from database
- [ ] Application restarted
- [ ] Test STK Push with real phone
- [ ] Test QR code generation
- [ ] Monitor logs for errors

---

## 📞 Support

### Safaricom API Support
- Email: apisupport@safaricom.co.ke
- Phone: +254 722 000 000
- Portal: https://developer.safaricom.co.ke

### What to Provide When Contacting Support
- Shortcode: **3560959**
- Error message (if any)
- Request ID from error response
- Timestamp of the issue

---

**System is now configured for PRODUCTION use with enhanced token validation, QR code generation, and C2B payment support!** 🎉
