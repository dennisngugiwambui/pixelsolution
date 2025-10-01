# Register C2B URLs - CORRECT METHOD

## 🎯 Use Ngrok URL, NOT Localhost!

### ❌ WRONG:
```
POST https://localhost:5001/api/MpesaTest/register-c2b
```

### ✅ CORRECT:
```
POST https://125023d8524c.ngrok-free.app/api/MpesaTest/register-c2b
```

---

## 🚀 How to Register

### Method 1: Using Browser
```
1. Open browser
2. Go to: https://125023d8524c.ngrok-free.app/api/MpesaTest/register-c2b
3. Click "Send" or "POST"
```

### Method 2: Using Postman
```
1. Open Postman
2. Method: POST
3. URL: https://125023d8524c.ngrok-free.app/api/MpesaTest/register-c2b
4. Click "Send"
```

### Method 3: Using PowerShell
```powershell
Invoke-WebRequest -Uri "https://125023d8524c.ngrok-free.app/api/MpesaTest/register-c2b" -Method POST
```

---

## ✅ Expected Response

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

## 📋 What Gets Registered

**Confirmation URL:**
```
https://125023d8524c.ngrok-free.app/api/mpesa/c2b/confirmation
```

**Validation URL:**
```
https://125023d8524c.ngrok-free.app/api/mpesa/c2b/validation
```

**Till Number:** 6509715

---

## 🔍 Verify Registration

**Check what URLs are configured:**
```
GET https://125023d8524c.ngrok-free.app/api/MpesaTest/check-c2b-registration
```

---

**Use the ngrok URL for EVERYTHING, not localhost!** 🎯
