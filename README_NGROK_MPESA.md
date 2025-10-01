# PixelSolution - Complete Setup Guide for Ngrok & M-Pesa

## 🚀 Quick Start (3 Steps)

### Step 1: Install Ngrok
1. Download ngrok: https://ngrok.com/download
2. Extract to `C:\ngrok\`
3. Sign up: https://dashboard.ngrok.com/signup
4. Get your auth token: https://dashboard.ngrok.com/get-started/your-authtoken
5. Run in PowerShell:
   ```powershell
   cd C:\ngrok
   .\ngrok config add-authtoken YOUR_AUTH_TOKEN_HERE
   ```

### Step 2: Start Your Application
1. Open Visual Studio
2. Open PixelSolution project
3. Press **F5** to run
4. Wait for app to start (https://localhost:5001)

### Step 3: Start Ngrok
**Option A: Double-click** `start-ngrok.bat` in project folder

**Option B: Run PowerShell script:**
```powershell
cd C:\Users\Denno\source\repos\PixelSolution
.\start-ngrok.ps1
```

**Option C: Manual command:**
```powershell
cd C:\ngrok
.\ngrok http https://localhost:5001
```

## 📋 Complete Step-by-Step Guide

### 1️⃣ Verify M-Pesa Credentials

**Current credentials in appsettings.Development.json:**
```json
"MpesaSettings": {
  "ConsumerKey": "4aEia8VMAGLQU28ZoorLQRZtMutc6A6GyGXMq9HYoNFyXNOY",
  "ConsumerSecret": "wMdKEDv2y2JZQ8ZdN1TAn4MgxbuILwrNsOu4ywi6QcVZJw4BrlEclAcW4XSduSlw",
  "Shortcode": "3560959",
  "Passkey": "fc087de2729c7ff67b2b2b3aacc2068039fc56284c676d56679ef86f70640d8d"
}
```

**⚠️ If getting "Invalid Access Token" error:**

1. **Login to Safaricom Portal:**
   - Go to: https://developer.safaricom.co.ke/login
   - Login with your credentials

2. **Find Your App:**
   - Go to "My Apps"
   - Find app with shortcode **3560959**
   - Verify status is **ACTIVE**

3. **Get Correct Credentials:**
   - Click on your app
   - Go to "Keys" or "Credentials" tab
   - Copy **Consumer Key** (exactly as shown)
   - Copy **Consumer Secret** (exactly as shown)

4. **Update appsettings.Development.json:**
   ```json
   "ConsumerKey": "PASTE_YOUR_CONSUMER_KEY_HERE",
   "ConsumerSecret": "PASTE_YOUR_CONSUMER_SECRET_HERE"
   ```

5. **Clear Old Tokens:**
   - Open SQL Server Management Studio
   - Connect to your database
   - Run: `DELETE FROM MpesaTokens;`

6. **Restart Application**

### 2️⃣ Setup Ngrok

**Install Ngrok:**
```powershell
# Download from https://ngrok.com/download
# Extract to C:\ngrok\

# Authenticate (get token from https://dashboard.ngrok.com/get-started/your-authtoken)
cd C:\ngrok
.\ngrok config add-authtoken YOUR_AUTH_TOKEN_HERE
```

**Start Ngrok:**
```powershell
cd C:\ngrok
.\ngrok http https://localhost:5001
```

**You'll see output like:**
```
Forwarding    https://abc123.ngrok-free.app -> https://localhost:5001
```

**Copy your ngrok URL!** (e.g., `https://abc123.ngrok-free.app`)

### 3️⃣ Update Callback URL

**Edit appsettings.Development.json:**
```json
"MpesaSettings": {
  "CallbackUrl": "https://YOUR-NGROK-URL.ngrok-free.app/api/mpesa/callback"
}
```

**Example:**
```json
"CallbackUrl": "https://abc123.ngrok-free.app/api/mpesa/callback"
```

**Important:** Replace `YOUR-NGROK-URL` with your actual ngrok URL!

### 4️⃣ Restart Application

1. Stop your application (Shift+F5 in Visual Studio)
2. Start it again (F5)
3. Keep ngrok running in the background

### 5️⃣ Access Your Application

**From your computer:**
```
https://YOUR-NGROK-URL.ngrok-free.app/Admin/Sales
```

**From your phone:**
```
https://YOUR-NGROK-URL.ngrok-free.app
```

**Example:**
```
https://abc123.ngrok-free.app/Admin/Sales
https://abc123.ngrok-free.app/Employee/Sales
```

### 6️⃣ Test M-Pesa Payment

1. Go to: `https://YOUR-NGROK-URL.ngrok-free.app/Admin/Sales`
2. Add items to cart
3. Click "Checkout"
4. Select **M-Pesa** payment method
5. Enter phone number: `758024400` (will auto-format to `254758024400`)
6. Click "Complete Payment"
7. Check your phone for STK prompt
8. Enter M-Pesa PIN
9. Wait for callback (system will auto-complete sale)

## 🔧 Troubleshooting

### Issue: "Invalid Access Token"

**Solution:**
1. Verify credentials on Safaricom Developer Portal
2. Make sure app is **ACTIVE**
3. Copy credentials exactly (no extra spaces)
4. Clear old tokens: `DELETE FROM MpesaTokens;`
5. Restart application

### Issue: Ngrok URL changes every time

**Solution:**
- Free plan gives random URLs
- Update callback URL each time you restart ngrok
- Or upgrade to paid plan for static subdomain

### Issue: M-Pesa callback not received

**Check:**
1. ✅ Ngrok is running
2. ✅ Application is running
3. ✅ Callback URL in appsettings matches ngrok URL
4. ✅ Check ngrok web interface: http://localhost:4040

### Issue: Amount validation error

**Solution:**
- Minimum amount: KSh 1
- No maximum limit (updated)
- Make sure amount is a valid number

### Issue: Phone number format error

**Solution:**
- Enter 9 digits: `758024400`
- System auto-adds `254` prefix
- Final format: `254758024400` (12 digits)

## 📊 Monitoring

### View Ngrok Requests
Open browser: http://localhost:4040

This shows:
- All incoming requests
- Request/response details
- M-Pesa callbacks
- Errors and status codes

### View Application Logs
Check Visual Studio Output window for:
- M-Pesa token generation
- STK Push requests
- Callback responses
- Error messages

## 🎯 Testing Checklist

Before testing M-Pesa:
- [ ] Application running on https://localhost:5001
- [ ] Ngrok running and showing forwarding URL
- [ ] Callback URL updated in appsettings.Development.json
- [ ] Application restarted after updating callback URL
- [ ] M-Pesa credentials verified on Safaricom portal
- [ ] Old tokens cleared from database
- [ ] Phone number ready (254XXXXXXXXX format)
- [ ] Test amount ready (e.g., KSh 10 for testing)

## 📱 Test Flow

```
1. Add items to cart
   ↓
2. Click "Checkout"
   ↓
3. Select "M-Pesa"
   ↓
4. Enter phone: 758024400
   ↓
5. Click "Complete Payment"
   ↓
6. System sends STK Push
   ↓
7. Check phone for prompt
   ↓
8. Enter M-Pesa PIN
   ↓
9. Safaricom sends callback to ngrok
   ↓
10. System completes sale
    ↓
11. Receipt generated
```

## 🔐 Security Notes

⚠️ **Important:**
- Ngrok exposes your local app to the internet
- Only use for development/testing
- Don't expose production databases
- Stop ngrok when not testing
- Never commit ngrok URLs to git

## 📞 Support

### Ngrok Issues
- Docs: https://ngrok.com/docs
- Support: https://ngrok.com/support

### M-Pesa Issues
- Portal: https://developer.safaricom.co.ke
- Email: apisupport@safaricom.co.ke
- Phone: +254 722 000 000

### Application Issues
- Check logs in Visual Studio Output
- Check ngrok web interface: http://localhost:4040
- Review error messages in browser console (F12)

## 🚀 Production Deployment

For production:
1. Deploy to real server (Azure, AWS, DigitalOcean)
2. Get proper domain name
3. Configure SSL certificate
4. Update M-Pesa callback URL to production domain
5. Change `IsSandbox` to `false`
6. Use production M-Pesa credentials
7. Update `BaseUrl` to `https://api.safaricom.co.ke`

## 📝 Quick Commands Reference

```powershell
# Start application
cd C:\Users\Denno\source\repos\PixelSolution\PixelSolution
dotnet run

# Start ngrok
cd C:\ngrok
.\ngrok http https://localhost:5001

# Clear M-Pesa tokens (SQL)
DELETE FROM MpesaTokens;

# Test token generation
GET https://localhost:5001/api/MpesaTest/test-token

# Clear tokens via API
POST https://localhost:5001/api/MpesaTest/clear-tokens
```

## ✅ Success Indicators

**You know it's working when:**
1. ✅ Ngrok shows forwarding URL
2. ✅ Application accessible via ngrok URL
3. ✅ STK prompt appears on phone
4. ✅ Callback received (check ngrok web interface)
5. ✅ Sale status changes from "Pending" to "Completed"
6. ✅ Receipt generated automatically
7. ✅ Stock deducted after payment confirmation

---

**Need more help?** Check these files:
- `NGROK_SETUP.md` - Detailed ngrok setup
- `MPESA_CREDENTIALS_CHECK.md` - M-Pesa credential verification
- `start-ngrok.bat` - Quick start script
- `start-ngrok.ps1` - PowerShell start script
