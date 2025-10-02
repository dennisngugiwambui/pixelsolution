# Manual M-Pesa Verification Implementation

## ✅ What's Implemented

### 1. Receipt Display with M-Pesa Code
- ✅ Receipt modal now shows M-Pesa transaction code
- ✅ Highlighted in green box for easy visibility
- ✅ Works for both STK Push and manual payments

### 2. Manual Verification UI
- ✅ Added "Already Paid? Verify M-Pesa Code" section in payment modal
- ✅ Input field for entering last 5 digits or full code
- ✅ "Verify & Complete Sale" button

---

## 🚀 Next Steps to Complete

### Step 1: Add Same UI to Admin/Sales.cshtml
Copy the manual verification section to Admin/Sales.cshtml (same location as Employee/Sales.cshtml)

### Step 2: Create Backend Endpoint

**File:** `Controllers/SalesController.cs` or `Controllers/EmployeeController.cs`

```csharp
[HttpPost("VerifyManualMpesaCode")]
public async Task<IActionResult> VerifyManualMpesaCode([FromBody] ManualMpesaVerificationRequest request)
{
    try
    {
        var code = request.MpesaCode.ToUpper().Trim();
        
        // Search for unused M-Pesa transaction
        // If last 5 digits provided, search by ending
        var query = _context.Set<UnusedMpesaTransaction>()
            .Where(t => t.IsUsed == false && t.TillNumber == "6509715");
        
        if (code.Length >= 5)
        {
            // Search by last 5 digits or full code
            query = query.Where(t => t.TransactionCode.EndsWith(code.Substring(code.Length - 5)) 
                                  || t.TransactionCode == code);
        }
        else
        {
            return Ok(new { success = false, message = "Please enter at least 5 characters" });
        }
        
        var transaction = await query.FirstOrDefaultAsync();
        
        if (transaction == null)
        {
            return Ok(new { 
                success = false, 
                message = "No unused M-Pesa transaction found with this code" 
            });
        }
        
        // Verify amount matches
        if (Math.Abs(transaction.Amount - request.SaleAmount) > 0.01m)
        {
            return Ok(new { 
                success = false, 
                message = $"Amount mismatch. Transaction amount: KSh {transaction.Amount}, Sale amount: KSh {request.SaleAmount}" 
            });
        }
        
        // Mark transaction as used
        transaction.IsUsed = true;
        transaction.UsedAt = DateTime.UtcNow;
        transaction.SaleId = request.SaleId;
        
        // Update sale
        var sale = await _context.Sales.FindAsync(request.SaleId);
        if (sale != null)
        {
            sale.Status = "Completed";
            sale.MpesaReceiptNumber = transaction.TransactionCode;
            sale.AmountPaid = transaction.Amount;
        }
        
        await _context.SaveChangesAsync();
        
        return Ok(new { 
            success = true, 
            message = "Payment verified successfully!",
            mpesaReceiptNumber = transaction.TransactionCode,
            amount = transaction.Amount
        });
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error verifying manual M-Pesa code");
        return Ok(new { success = false, message = "Error verifying code" });
    }
}

public class ManualMpesaVerificationRequest
{
    public string MpesaCode { get; set; }
    public int SaleId { get; set; }
    public decimal SaleAmount { get; set; }
}
```

### Step 3: Create UnusedMpesaTransaction Model

**File:** `Models/AdditionalModels.cs`

```csharp
public class UnusedMpesaTransaction
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    [StringLength(50)]
    public string TransactionCode { get; set; } = string.Empty;
    
    [Required]
    [StringLength(20)]
    public string TillNumber { get; set; } = string.Empty;
    
    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }
    
    [StringLength(15)]
    public string PhoneNumber { get; set; } = string.Empty;
    
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
    
    public bool IsUsed { get; set; } = false;
    
    public DateTime? UsedAt { get; set; }
    
    public int? SaleId { get; set; }
    
    [ForeignKey("SaleId")]
    public virtual Sale? Sale { get; set; }
}
```

### Step 4: Update C2B Callback to Record ALL Payments

**File:** `Controllers/MpesaCallbackController.cs`

Add this at the end of C2B confirmation handler:

```csharp
// Record ALL payments to till 6509715, even if no matching sale
var unusedTransaction = new UnusedMpesaTransaction
{
    TransactionCode = transId,
    TillNumber = "6509715",
    Amount = transAmount,
    PhoneNumber = msisdn,
    ReceivedAt = DateTime.UtcNow,
    IsUsed = pendingSale != null,
    SaleId = pendingSale?.SaleId
};

_context.UnusedMpesaTransactions.Add(unusedTransaction);
await _context.SaveChangesAsync();
```

### Step 5: Create Migration

```powershell
dotnet ef migrations add AddUnusedMpesaTransactions
dotnet ef database update
```

### Step 6: Add JavaScript Function

**File:** `wwwroot/js/sales.js`

```javascript
async function verifyManualMpesaCode() {
    const codeInput = document.getElementById('manualMpesaCode');
    const code = codeInput.value.trim().toUpperCase();
    
    if (code.length < 5) {
        showToast('Please enter at least 5 characters of the M-Pesa code', 'error');
        return;
    }
    
    try {
        const endpoint = window.location.pathname.includes('/Admin/') 
            ? '/Sales/VerifyManualMpesaCode' 
            : '/Employee/VerifyManualMpesaCode';
        
        const response = await fetch(endpoint, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                mpesaCode: code,
                saleId: currentSaleId, // Need to store this when sale is created
                saleAmount: currentTotal
            })
        });
        
        const result = await response.json();
        
        if (result.success) {
            showToast('✅ Payment verified successfully!', 'success');
            
            // Generate receipt with M-Pesa code
            const saleData = {
                totalAmount: result.amount,
                paymentMethod: 'M-Pesa',
                mpesaReceiptNumber: result.mpesaReceiptNumber
            };
            
            generateReceipt(currentSaleId, saleData);
            
            // Clear cart and close modal
            cart = [];
            updateCartDisplay();
            closePaymentModal();
            updateTodayStats();
        } else {
            showToast(`❌ ${result.message}`, 'error');
        }
    } catch (error) {
        console.error('Error verifying M-Pesa code:', error);
        showToast('Error verifying code. Please try again.', 'error');
    }
}
```

---

## 🎯 How It Works

### Flow 1: Customer Pays Manually
```
1. Customer opens M-Pesa app
2. Selects "Buy Goods"
3. Enters Till: 6509715
4. Enters amount
5. Enters PIN
6. Payment sent
7. C2B callback received
8. Transaction recorded in UnusedMpesaTransactions (IsUsed = false)
9. Cashier enters last 5 digits in POS
10. System finds matching unused transaction
11. Verifies amount matches
12. Marks transaction as used
13. Completes sale
14. Generates receipt with M-Pesa code
```

### Flow 2: STK Push
```
1. Cashier selects M-Pesa payment
2. Enters customer phone
3. STK Push sent
4. Customer enters PIN
5. Callback received
6. Sale completed automatically
7. Receipt generated with M-Pesa code
```

---

## ✅ Success Indicators

**When manual verification works:**
1. Customer pays manually to Till 6509715
2. Cashier enters last 5 digits (e.g., "68ZST")
3. System finds transaction
4. Verifies amount matches
5. Completes sale
6. Shows receipt with full M-Pesa code

**When amount doesn't match:**
```
Error: "Amount mismatch. Transaction amount: KSh 150, Sale amount: KSh 200"
```

**When code not found:**
```
Error: "No unused M-Pesa transaction found with this code"
```

**When code already used:**
```
Error: "This M-Pesa code has already been used"
```

---

## 📋 Testing Checklist

- [ ] Customer pays manually to Till 6509715
- [ ] C2B callback received and recorded
- [ ] Transaction appears in UnusedMpesaTransactions table
- [ ] Cashier enters last 5 digits
- [ ] System finds transaction
- [ ] Amount verified
- [ ] Sale completed
- [ ] Receipt shows M-Pesa code
- [ ] Transaction marked as used
- [ ] Cannot use same code twice

---

**This ensures ALL payments to Till 6509715 are tracked and can be verified!** 🎉
