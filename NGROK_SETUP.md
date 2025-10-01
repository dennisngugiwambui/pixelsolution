# PixelSolution - Ngrok Setup Guide

## Prerequisites
1. Download ngrok from: https://ngrok.com/download
2. Extract ngrok.exe to a folder (e.g., C:\ngrok)
3. Sign up for free account at: https://dashboard.ngrok.com/signup

## Step 1: Install and Configure Ngrok

### Option A: Using PowerShell (Recommended)
```powershell
# Navigate to your ngrok folder
cd C:\ngrok

# Authenticate ngrok (get your token from https://dashboard.ngrok.com/get-started/your-authtoken)
.\ngrok config add-authtoken YOUR_AUTH_TOKEN_HERE
```

### Option B: Using Command Prompt
```cmd
cd C:\ngrok
ngrok config add-authtoken YOUR_AUTH_TOKEN_HERE
```

## Step 2: Start Your PixelSolution Application

1. Open Visual Studio
2. Run your PixelSolution project (F5 or Ctrl+F5)
3. Note the HTTPS port (should be 5001)
4. Keep the application running

## Step 3: Start Ngrok Tunnel

### Open a NEW PowerShell/Command Prompt window and run:

```powershell
# Navigate to ngrok folder
cd C:\ngrok

# Start ngrok tunnel for HTTPS port 5001
.\ngrok http https://localhost:5001
```

### You should see output like this:
```
ngrok

Session Status                online
Account                       Your Name (Plan: Free)
Version                       3.x.x
Region                        United States (us)
Latency                       -
Web Interface                 http://127.0.0.1:4040
Forwarding                    https://abc123.ngrok-free.app -> https://localhost:5001

Connections                   ttl     opn     rt1     rt5     p50     p90
                              0       0       0.00    0.00    0.00    0.00
```

## Step 4: Get Your Public URL

**Your public URL will be something like:**
```
https://abc123.ngrok-free.app
```

**Copy this URL!** You'll need it for:
1. Accessing your site from anywhere
2. M-Pesa callback configuration

## Step 5: Access Your Application

### From Your Computer:
```
https://abc123.ngrok-free.app/Admin/Sales
https://abc123.ngrok-free.app/Employee/Sales
```

### From Your Phone or Another Device:
```
https://abc123.ngrok-free.app
```

**Note:** Replace `abc123.ngrok-free.app` with YOUR actual ngrok URL

## Step 6: Update M-Pesa Callback URL

### Update appsettings.Development.json:
```json
"MpesaSettings": {
  "CallbackUrl": "https://YOUR-NGROK-URL.ngrok-free.app/api/mpesa/callback"
}
```

**Example:**
```json
"CallbackUrl": "https://abc123.ngrok-free.app/api/mpesa/callback"
```

### After updating:
1. Stop your application
2. Restart your application
3. Keep ngrok running

## Step 7: Test M-Pesa Integration

1. Go to: `https://YOUR-NGROK-URL.ngrok-free.app/Admin/Sales`
2. Add items to cart
3. Select M-Pesa payment
4. Enter phone number (254XXXXXXXXX)
5. Click "Complete Payment"
6. Check your phone for STK prompt
7. Enter M-Pesa PIN
8. System will receive callback via ngrok!

## Ngrok Commands Reference

### Start ngrok with HTTPS
```powershell
ngrok http https://localhost:5001
```

### Start ngrok with custom subdomain (Paid plan only)
```powershell
ngrok http https://localhost:5001 --subdomain=pixelsolution
```

### View ngrok web interface (see all requests)
Open browser: http://localhost:4040

### Stop ngrok
Press `Ctrl+C` in the ngrok terminal

## Troubleshooting

### Issue: "Invalid Host Header"
**Solution:** Add to appsettings.Development.json:
```json
"AllowedHosts": "*"
```

### Issue: Ngrok URL changes every time
**Solution:** 
- Free plan gives random URLs
- Upgrade to paid plan for static subdomain
- Or update callback URL each time you restart ngrok

### Issue: M-Pesa callback not received
**Check:**
1. Ngrok is running
2. Application is running
3. Callback URL in appsettings matches ngrok URL
4. Check ngrok web interface (http://localhost:4040) for incoming requests

### Issue: SSL Certificate Error
**Solution:** Ngrok handles SSL automatically, no action needed

## Important Notes

⚠️ **Security Warning:**
- Ngrok exposes your local app to the internet
- Only use for development/testing
- Don't expose production databases
- Stop ngrok when not testing

⚠️ **Free Plan Limitations:**
- Random URL each time (changes on restart)
- 40 connections/minute limit
- Session timeout after 2 hours

## Production Deployment

For production, you should:
1. Deploy to a real server (Azure, AWS, DigitalOcean, etc.)
2. Get a proper domain name
3. Configure SSL certificate
4. Update M-Pesa callback URL to production domain

## Quick Start Commands

```powershell
# Terminal 1: Start your app
cd C:\Users\Denno\source\repos\PixelSolution\PixelSolution
dotnet run

# Terminal 2: Start ngrok
cd C:\ngrok
.\ngrok http https://localhost:5001

# Copy the ngrok URL and access:
# https://YOUR-URL.ngrok-free.app/Admin/Sales
```

## M-Pesa Credentials Verification

**Current Credentials:**
- Consumer Key: 4aEia8VMAGLQU28ZoorLQRZtMutc6A6GyGXMq9HYoNFyXNOY
- Consumer Secret: wMdKEDv2y2JZQ8ZdN1TAn4MgxbuILwrNsOu4ywi6QcVZJw4BrlEclAcW4XSduSlw
- Shortcode: 3560959
- Passkey: fc087de2729c7ff67b2b2b3aacc2068039fc56284c676d56679ef86f70640d8d

**If getting "Invalid Access Token" error:**
1. Verify credentials on Safaricom Developer Portal
2. Make sure the app is active
3. Check if credentials match the shortcode
4. Try regenerating credentials on the portal

---

**Need Help?** Check ngrok docs: https://ngrok.com/docs
