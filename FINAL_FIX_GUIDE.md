# FINAL FIX - M-Pesa STK Push Issue

## 🔴 The Problem

**Error:** "The request is not permitted according to product assignment"

**Meaning:** Your Shortcode **3560959** is **NOT enabled for STK Push** on Safaricom's side.

---

## ✅ Solution Applied

Changed STK Push to use **Till 6509715** instead of Shortcode 3560959:

```json
// BEFORE (WRONG):
"Shortcode": "3560959",  // Not enabled for STK Push
"TillNumber": "6509715",

// AFTER (CORRECT):
"Shortcode": "6509715",  // Use till for both STK and QR
"TillNumber": "6509715",
```

---

## 🚀 Next Steps

### Step 1: Restart Application
```
1. Stop (Shift+F5)
2. Start (F5)
```

### Step 2: Register C2B URLs (Use Ngrok!)
```
POST https://125023d8524c.ngrok-free.app/api/MpesaTest/register-c2b
```

**NOT localhost!**

### Step 3: Test STK Push
```
1. Go to: https://125023d8524c.ngrok-free.app/Employee/Sales
2. Add item (KSH 1)
3. Select M-Pesa
4. Enter phone: 758024400
5. Click "Complete Payment"
6. Spinner appears
7. Check phone for STK prompt
8. Enter PIN
9. Should succeed!
```

### Step 4: Test QR Code
```
1. Generate QR code
2. Scan with M-Pesa app
3. Pay KSH 1
4. Wait 10 seconds
5. Check database for transaction ID
6. Should be linked automatically
```

---

## 📊 What Changed

### Configuration:
- ✅ Shortcode changed from 3560959 → 6509715
- ✅ Both STK Push and QR use same till (6509715)
- ✅ Passkey already correct for 6509715
- ✅ Spinner added to sales.js
- ✅ C2B callback handler ready

### URLs (All use ngrok):
- ✅ CallbackUrl: `https://125023d8524c.ngrok-free.app/api/mpesa/callback`
- ✅ ConfirmationUrl: `https://125023d8524c.ngrok-free.app/api/mpesa/c2b/confirmation`
- ✅ ValidationUrl: `https://125023d8524c.ngrok-free.app/api/mpesa/c2b/validation`

---

## ⚠️ Important Notes

### 1. Till 6509715 Must Support STK Push
If you still get the same error, it means **Till 6509715 is also not enabled for STK Push**.

**Solution:** Contact Safaricom to enable STK Push for Till 6509715:
- Email: apisupport@safaricom.co.ke
- Say: "Please enable Lipa Na M-Pesa Online (STK Push) for Till 6509715"

### 2. Use Ngrok URL Everywhere
```
❌ https://localhost:5001/...
✅ https://125023d8524c.ngrok-free.app/...
```

### 3. Register C2B URLs First
Before testing QR payments, always register:
```
POST https://125023d8524c.ngrok-free.app/api/MpesaTest/register-c2b
```

---

## 🧪 Testing Checklist

- [ ] Application restarted
- [ ] Config shows Shortcode: 6509715
- [ ] C2B URLs registered (via ngrok)
- [ ] STK Push test (KSH 1)
- [ ] Spinner appears
- [ ] STK prompt received
- [ ] Payment succeeds
- [ ] Receipt generated
- [ ] QR code test
- [ ] QR payment links automatically

---

## 📞 If Still Failing

### Check Application Logs:
```
Look for:
📱 Initiating STK Push
🔐 STK Push params - Shortcode: 6509715
```

### Check M-Pesa Response:
```
If you see:
"The request is not permitted..." = Till 6509715 not enabled for STK
"Success" = Working! ✅
```

### Contact Safaricom:
```
Email: apisupport@safaricom.co.ke
Subject: Enable STK Push for Till 6509715

Message:
"Hello,

Please enable Lipa Na M-Pesa Online (STK Push) for my Till Number 6509715.

Consumer Key: 4aEia8VMAGLQU28ZoorLQRZtMutc6A6GyGXMq9HYoNFyXNOY

Thank you."
```

---

## ✅ Expected Results

### Success Indicators:
```
1. STK Push sent successfully
2. Customer receives prompt on phone
3. Enters PIN
4. Spinner shows: "Payment successful! ✅"
5. Receipt generated with M-Pesa code
6. Database shows: Status = "Completed"
```

### Failure Indicators:
```
1. Error: "The request is not permitted..."
   → Till 6509715 not enabled for STK Push
   → Contact Safaricom

2. Error: "Invalid CallBackURL"
   → Using localhost instead of ngrok
   → Use ngrok URL

3. Status stays "Pending" forever
   → Callback not received
   → Register C2B URLs
```

---

## 🎯 Summary

**Changed:** Shortcode from 3560959 → 6509715  
**Reason:** 3560959 not enabled for STK Push  
**Next:** Restart app and test  
**If fails:** Contact Safaricom to enable STK for 6509715

**Everything else is ready - just need correct Safaricom configuration!** 🎉
