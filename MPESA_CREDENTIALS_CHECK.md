# M-Pesa Credentials Verification Guide

## Current Error
```
"errorCode": "404.001.03"
"errorMessage": "Invalid Access Token"
```

This error means the **Consumer Key** or **Consumer Secret** is incorrect or the app is not active on Safaricom Developer Portal.

## Your Current Credentials

**From appsettings.Development.json:**
- **Consumer Key**: `4aEia8VMAGLQU28ZoorLQRZtMutc6A6GyGXMq9HYoNFyXNOY`
- **Consumer Secret**: `wMdKEDv2y2JZQ8ZdN1TAn4MgxbuILwrNsOu4ywi6QcVZJw4BrlEclAcW4XSduSlw`
- **Shortcode**: `3560959`
- **Passkey**: `fc087de2729c7ff67b2b2b3aacc2068039fc56284c676d56679ef86f70640d8d`

## How to Verify Credentials on Safaricom Portal

### Step 1: Login to Safaricom Developer Portal
1. Go to: https://developer.safaricom.co.ke/login
2. Login with your credentials
3. Go to "My Apps"

### Step 2: Check Your App
1. Find your app with Shortcode **3560959**
2. Click on the app name
3. Verify the app status is **ACTIVE**

### Step 3: Get Correct Credentials
1. Click on "Keys" or "Credentials" tab
2. Copy the **Consumer Key** (should be exactly as shown)
3. Copy the **Consumer Secret** (should be exactly as shown)
4. Make sure you're copying from the **SANDBOX** section if testing

### Step 4: Verify API Products
Make sure your app has these API products enabled:
- ✅ **M-PESA Express (STK Push)** or **Lipa Na M-Pesa Online**
- ✅ **M-Pesa Sandbox** (for testing)

### Step 5: Check Passkey
1. The Passkey is specific to your shortcode
2. For **Sandbox**, the default passkey is usually:
   ```
   bfb279f9aa9bdbcf158e97dd71a467cd2e0c893059b10f78e6b72ada1ed2c919
   ```
3. For **Production**, you'll get a unique passkey from Safaricom

## Common Issues

### Issue 1: Wrong Environment
- **Sandbox URL**: `https://sandbox.safaricom.co.ke`
- **Production URL**: `https://api.safaricom.co.ke`

Make sure your `BaseUrl` matches your credentials environment.

### Issue 2: Expired Credentials
- Credentials can expire or be revoked
- Generate new credentials on the portal
- Update your appsettings.json

### Issue 3: App Not Active
- Your app must be in **ACTIVE** status
- If it's in **DRAFT** or **SUSPENDED**, it won't work
- Contact Safaricom support if suspended

### Issue 4: Wrong Shortcode
- Consumer Key/Secret must match the shortcode
- If you have multiple apps, make sure you're using credentials from the correct app

## How to Update Credentials

### 1. Update appsettings.Development.json
```json
"MpesaSettings": {
  "ConsumerKey": "YOUR_NEW_CONSUMER_KEY_HERE",
  "ConsumerSecret": "YOUR_NEW_CONSUMER_SECRET_HERE",
  "Shortcode": "3560959",
  "Passkey": "YOUR_PASSKEY_HERE",
  "CallbackUrl": "https://your-ngrok-url.ngrok-free.app/api/mpesa/callback",
  "BaseUrl": "https://sandbox.safaricom.co.ke",
  "IsSandbox": true
}
```

### 2. Clear Old Tokens
Run this SQL in your database:
```sql
DELETE FROM MpesaTokens;
```

Or call the API:
```
POST https://localhost:5001/api/MpesaTest/clear-tokens
```

### 3. Restart Application
- Stop your application
- Start it again
- Try M-Pesa payment

## Testing Credentials

### Method 1: Using Postman
```
GET https://sandbox.safaricom.co.ke/oauth/v1/generate?grant_type=client_credentials
Authorization: Basic BASE64(ConsumerKey:ConsumerSecret)
```

**Expected Response:**
```json
{
  "access_token": "xxxxxxxxxxxxx",
  "expires_in": "3599"
}
```

### Method 2: Using Your App
1. Go to: `https://localhost:5001/api/MpesaTest/test-token`
2. Check the response
3. If successful, token is valid
4. If error, credentials are wrong

## What to Do Next

### Option A: Get New Credentials from Portal
1. Login to https://developer.safaricom.co.ke
2. Go to your app
3. Regenerate credentials
4. Copy new Consumer Key and Secret
5. Update appsettings.Development.json
6. Clear tokens (DELETE FROM MpesaTokens)
7. Restart app

### Option B: Contact Safaricom Support
If you can't access the portal or credentials don't work:
- Email: apisupport@safaricom.co.ke
- Phone: +254 722 000 000
- Provide your shortcode: **3560959**

## Sandbox vs Production

### Sandbox (Testing)
- **URL**: https://sandbox.safaricom.co.ke
- **Shortcode**: Usually 174379 or your test shortcode
- **Phone**: Use test numbers from Safaricom
- **Passkey**: Default sandbox passkey

### Production (Live)
- **URL**: https://api.safaricom.co.ke
- **Shortcode**: Your actual till/paybill number
- **Phone**: Real customer phone numbers
- **Passkey**: Unique passkey from Safaricom

## Quick Checklist

Before testing M-Pesa:
- [ ] App is ACTIVE on Safaricom portal
- [ ] Consumer Key is correct (52 characters)
- [ ] Consumer Secret is correct (72 characters)
- [ ] Shortcode matches your app (3560959)
- [ ] Passkey is correct for your shortcode
- [ ] BaseUrl matches environment (sandbox/production)
- [ ] Old tokens cleared from database
- [ ] Application restarted
- [ ] Ngrok running (for callbacks)
- [ ] Callback URL updated in appsettings

---

**Still having issues?**
1. Check application logs for detailed error messages
2. Verify credentials character by character
3. Try generating new credentials on the portal
4. Contact Safaricom API support
