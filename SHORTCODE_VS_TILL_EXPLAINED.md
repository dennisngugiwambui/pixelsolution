# Shortcode vs Till Number - Complete Explanation

## 🎯 Understanding the Difference

### **Shortcode (3560959)**
- **Purpose:** Used for **STK Push** (Lipa Na M-Pesa Online)
- **What it does:** Sends payment prompt to customer's phone
- **Used in:** STK Push API requests
- **Passkey:** `fc087de2729c7ff67b2b2b3aacc2068039fc56284c676d56679ef86f70640d8d`
- **Type:** Paybill number

### **Till Number (6509715)**
- **Purpose:** Used for **Buy Goods** (C2B payments)
- **What it does:** Customers pay manually via M-Pesa menu
- **Used in:** QR codes, manual payments
- **Type:** Till number for Buy Goods

---

## 📊 When to Use Which

| Feature | Use Shortcode (3560959) | Use Till (6509715) |
|---------|------------------------|-------------------|
| **STK Push** | ✅ YES | ❌ NO |
| **QR Code** | ❌ NO | ✅ YES |
| **Manual Payment** | ❌ NO | ✅ YES |
| **API Password** | ✅ YES (with passkey) | ❌ NO |
| **Callback URL** | ✅ YES | ✅ YES (C2B) |

---

## ✅ Correct Configuration

```json
"MpesaSettings": {
  "Shortcode": "3560959",      // For STK Push
  "TillNumber": "6509715",      // For QR Code & Buy Goods
  "Passkey": "fc087de2729c7ff67b2b2b3aacc2068039fc56284c676d56679ef86f70640d8d"
}
```

---

## 🔄 How They Work Together

### **STK Push Flow (Uses Shortcode)**
```
1. Customer clicks "Pay with M-Pesa"
2. System calls STK Push API
3. Uses Shortcode: 3560959
4. Uses Passkey to generate password
5. Customer receives prompt on phone
6. Customer enters PIN
7. Payment processed
```

### **QR Code Flow (Uses Till Number)**
```
1. System generates QR code
2. QR contains Till: 6509715
3. Customer scans QR with M-Pesa app
4. M-Pesa app shows: "Pay Till 6509715"
5. Customer enters PIN
6. Payment processed
```

### **Manual Payment Flow (Uses Till Number)**
```
1. Customer opens M-Pesa menu
2. Selects "Lipa Na M-Pesa"
3. Selects "Buy Goods and Services"
4. Enters Till: 6509715
5. Enters amount
6. Enters PIN
7. Payment processed
```

---

## 🛠️ Implementation Details

### **STK Push (MpesaService.cs)**
```csharp
var stkRequest = new StkPushRequest
{
    BusinessShortCode = _settings.Shortcode, // 3560959
    Password = GeneratePassword(_settings.Shortcode, _settings.Passkey, timestamp),
    // ... other fields
};
```

### **QR Code (MpesaService.cs)**
```csharp
var qrRequest = new
{
    MerchantName = merchantName,
    Amount = amount,
    TrxCode = "BG", // Buy Goods
    CPI = _settings.TillNumber, // 6509715
    Size = size
};
```

### **C2B Registration**
```csharp
var registerRequest = new
{
    ShortCode = _settings.TillNumber, // 6509715 for Buy Goods
    ConfirmationURL = _settings.ConfirmationUrl,
    ValidationURL = _settings.ValidationUrl
};
```

---

## ⚠️ Common Mistakes

### ❌ **WRONG: Using Till for STK Push**
```json
"Shortcode": "6509715"  // WRONG! This is the till, not shortcode
```
**Result:** STK Push fails, wrong passkey, authentication error

### ❌ **WRONG: Using Shortcode for QR Code**
```csharp
CPI = _settings.Shortcode  // WRONG! QR should use till
```
**Result:** QR shows wrong number, customer can't pay

### ✅ **CORRECT: Separate Values**
```json
"Shortcode": "3560959",   // For STK Push
"TillNumber": "6509715"   // For QR Code
```

---

## 🧪 Testing

### **Test STK Push (Shortcode 3560959)**
```
1. Add item to cart
2. Select M-Pesa payment
3. Enter phone number
4. Click "Complete Payment"
5. Should receive STK prompt
6. Check logs: "BusinessShortCode: 3560959"
```

### **Test QR Code (Till 6509715)**
```
1. Add item to cart
2. Select M-Pesa payment
3. Enter phone number
4. QR code appears
5. Scan QR code
6. M-Pesa app shows: "Till 6509715"
7. Enter PIN and pay
```

---

## 📝 Configuration Checklist

- [ ] Shortcode set to: **3560959**
- [ ] TillNumber set to: **6509715**
- [ ] Passkey matches shortcode: **fc087de2729c7ff67b2b2b3aacc2068039fc56284c676d56679ef86f70640d8d**
- [ ] STK Push uses Shortcode
- [ ] QR Code uses TillNumber
- [ ] C2B registration uses TillNumber
- [ ] Callback URLs configured
- [ ] BaseUrl set to production: **https://api.safaricom.co.ke**

---

## 🎯 Summary

| Number | Name | Purpose | Used For |
|--------|------|---------|----------|
| **3560959** | Shortcode | STK Push | Paybill, STK Push API |
| **6509715** | Till Number | Buy Goods | QR Code, Manual Payment, C2B |

**Key Point:** 
- **STK Push = Shortcode (3560959)**
- **QR Code = Till (6509715)**
- **Both are valid, just for different purposes!**

---

**Configuration is now correct! Restart your app to apply changes.** 🎉
