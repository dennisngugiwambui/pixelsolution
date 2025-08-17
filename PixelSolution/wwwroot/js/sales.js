
        // Global variables
        let cart = [];
        let selectedPaymentMethod = null;
        let currentTotal = 0;
        let allProducts = [];
        let allCategories = [];
        let lastSaleData = null;

        // Initialize POS system
        document.addEventListener('DOMContentLoaded', function() {
            console.log('Initializing POS system...');
            loadProducts();
            loadCategories();
            updateCartDisplay();
            setupSearchFunctionality();
            loadTodayStats();
            setupKeyboardShortcuts();
            setupEventListeners();
        });

        // Load categories for filter
        async function loadCategories() {
            try {
                const response = await fetch('/api/categories');
                if (response.ok) {
                    allCategories = await response.json();

                    const categoryFilter = document.getElementById('categoryFilter');
                    categoryFilter.innerHTML = '<option value="">All Categories</option>';

                    allCategories.forEach(category => {
                        const option = document.createElement('option');
                        option.value = category.categoryId;
                        option.textContent = category.name;
                        categoryFilter.appendChild(option);
                    });

                    console.log(`Loaded ${allCategories.length} categories`);
                }
            } catch (error) {
                console.error('Error loading categories:', error);
            }
        }

        // Load products from server
        async function loadProducts() {
            try {
                console.log('Loading products from server...');
                showLoadingState();

                const response = await fetch('/Admin/GetProductsForSale');

                if (!response.ok) {
                    throw new Error(`HTTP error! status: ${response.status}`);
                }

                const data = await response.json();
                console.log('📦 Products API response:', data);

                if (data.success && data.products) {
                    allProducts = data.products;
                    console.log(`✅ Loaded ${allProducts.length} products`);
                    displayProducts(allProducts);
                } else {
                    console.error('❌ Failed to load products:', data.message || 'Unknown error');
                    displayEmptyState('Failed to load products. Please refresh the page.');
                }
            } catch (error) {
                console.error('💥 Error loading products:', error);
                displayEmptyState('Error loading products. Please check your connection.');
            }
        }

        // Show loading state
        function showLoadingState() {
            const container = document.getElementById('productsContainer');
            container.innerHTML = `
                <div style="text-align: center; padding: 4rem; color: #64748b;">
                    <i class="fas fa-spinner fa-spin" style="font-size: 2rem; margin-bottom: 1rem; color: #10b981;"></i>
                    <h3>Loading products...</h3>
                    <p>Please wait while we fetch the latest products.</p>
                </div>
            `;
        }

        // Display products in grid
        function displayProducts(products) {
            const container = document.getElementById('productsContainer');

            if (!products || products.length === 0) {
                displayEmptyState('No products available matching your filters.');
                return;
            }

            const productsGrid = document.createElement('div');
            productsGrid.className = 'products-grid';

            products.forEach(product => {
                const stockClass = product.stockQuantity <= 0 ? "out-of-stock" :
                                 product.stockQuantity <= 5 ? "low-stock" : "";

                const stockBadgeClass = product.stockQuantity <= 0 ? "out-of-stock" :
                                       product.stockQuantity <= 5 ? "low-stock" : "in-stock";

                const stockText = product.stockQuantity <= 0 ? "Out" :
                                 product.stockQuantity <= 5 ? "Low" : "In Stock";

                const productCard = document.createElement('div');
                productCard.className = `product-card ${stockClass}`;
                productCard.dataset.productId = product.id; // API returns 'id', not 'productId'
                productCard.dataset.categoryId = product.categoryId || '';
                productCard.dataset.stockStatus = stockBadgeClass;

                if (product.stockQuantity > 0) {
                    productCard.onclick = () => addToCart(product.id); // API returns 'id', not 'productId'
                } else {
                    productCard.onclick = () => showStockWarning(product.name);
                }

                productCard.innerHTML = `
                    <div class="stock-badge ${stockBadgeClass}">
                        ${stockText}
                    </div>

                    ${product.imageUrl ?
                        `<img src="${product.imageUrl}" alt="${product.name}" class="product-image" onerror="this.style.display='none'; this.nextElementSibling.style.display='flex';">
                         <div class="product-image" style="display: none;">
                             <i class="fas fa-cube"></i>
                         </div>` :
                        `<div class="product-image">
                             <i class="fas fa-cube"></i>
                         </div>`
                    }

                    <div class="product-name" title="${product.name}">${product.name}</div>
                    <div class="product-sku">SKU: ${product.sku}</div>
                    <div class="product-price">KSh ${parseFloat(product.price).toLocaleString('en-KE', { minimumFractionDigits: 2 })}</div>
                    <div class="product-stock">Stock: ${product.stockQuantity}</div>
                `;

                productsGrid.appendChild(productCard);
            });

            container.innerHTML = '';
            container.appendChild(productsGrid);
        }

        // Display empty state
        function displayEmptyState(message) {
            const container = document.getElementById('productsContainer');
            container.innerHTML = `
                <div class="no-products">
                    <i class="fas fa-cube"></i>
                    <h3>No products found</h3>
                    <p>${message}</p>
                    <button onclick="loadProducts()" class="btn" style="margin-top: 1rem; background: #10b981; color: white; padding: 0.75rem 1.5rem; border-radius: 8px; border: none; cursor: pointer;">
                        <i class="fas fa-refresh"></i> Retry
                    </button>
                </div>
            `;
        }

        // Show stock warning
        function showStockWarning(productName) {
            const warning = document.getElementById('stockWarning');
            const warningText = document.getElementById('stockWarningText');
            warningText.textContent = `${productName} is out of stock. Please restock to continue selling.`;
            warning.style.display = 'block';
            setTimeout(() => {
                warning.style.display = 'none';
            }, 3000);
        }

        // Add product to cart
        function addToCart(productId) {
            console.log(`DEBUG: Adding product ${productId} to cart`);
            console.log(`DEBUG: Available products:`, allProducts.map(p => ({ id: p.id, name: p.name })));
            console.log(`DEBUG: Looking for product with ID: ${productId} (type: ${typeof productId})`);

            const product = allProducts.find(p => {
                console.log(`DEBUG: Comparing ${p.id} (type: ${typeof p.id}) with ${productId} (type: ${typeof productId})`);
                return p.id == productId; // Use == to handle type coercion
            });
            
            if (!product) {
                console.error('DEBUG: Product not found with ID:', productId);
                console.error('DEBUG: Available product IDs:', allProducts.map(p => p.id));
                showToast('Product not found', 'error');
                return;
            }
            
            console.log(`DEBUG: Found product:`, product);

            if (product.stockQuantity <= 0) {
                showStockWarning(product.name);
                return;
            }

            // Check if product already exists in cart
            const existingItem = cart.find(item => item.id === productId);

            if (existingItem) {
                if (existingItem.quantity < product.stockQuantity) {
                    existingItem.quantity++;
                    existingItem.total = existingItem.quantity * existingItem.price;
                    console.log(`Updated quantity for ${product.name}: ${existingItem.quantity}`);
                    showToast(`Updated ${product.name} quantity`, 'success');
                } else {
                    showToast('Cannot add more. Insufficient stock', 'error');
                    return;
                }
            } else {
                cart.push({
                    id: productId,
                    name: product.name,
                    sku: product.sku,
                    price: parseFloat(product.price), // API returns 'price', not 'sellingPrice'
                    quantity: 1,
                    total: parseFloat(product.price), // API returns 'price', not 'sellingPrice'
                    maxStock: product.stockQuantity,
                    imageUrl: product.imageUrl
                });
                console.log(`Added ${product.name} to cart`);
                showToast(`Added ${product.name} to cart`, 'success');
            }

            updateCartDisplay();
        }

        // Update cart display
        function updateCartDisplay() {
            console.log('Updating cart display, items:', cart.length);

            const cartItemsContainer = document.getElementById('cartItems');
            const cartItemCount = document.getElementById('cartItemCount');
            const cartSubtotalDisplay = document.getElementById('cartSubtotalDisplay');

            // Calculate totals - Tax is 15% OF the total amount, not added on top
            const subtotal = cart.reduce((sum, item) => sum + item.total, 0);
            // If total selling price is the subtotal, then tax = 15% of that amount
            const total = subtotal; // The subtotal IS the total selling price
            const tax = total * 0.15; // 15% of the selling price
            const netAmount = total - tax; // Net amount after tax deduction
            currentTotal = total;

            // Update counters in header
            cartItemCount.textContent = cart.length;
            cartSubtotalDisplay.textContent = `KSh ${total.toLocaleString('en-KE', { minimumFractionDigits: 2 })}`;

            // Update total displays in footer
            document.getElementById('subtotalAmount').textContent = `KSh ${netAmount.toLocaleString('en-KE', { minimumFractionDigits: 2 })}`;
            document.getElementById('taxAmount').textContent = `KSh ${tax.toLocaleString('en-KE', { minimumFractionDigits: 2 })}`;
            document.getElementById('totalAmount').textContent = `KSh ${total.toLocaleString('en-KE', { minimumFractionDigits: 2 })}`;

            // Update button states
            const hasItems = cart.length > 0;
            document.getElementById('clearBtn').disabled = !hasItems;
            document.getElementById('holdBtn').disabled = !hasItems;
            document.getElementById('checkoutBtn').disabled = !hasItems;

            // Update cart items display
            if (cart.length === 0) {
                cartItemsContainer.innerHTML = `
                    <div class="empty-cart">
                        <i class="fas fa-shopping-cart"></i>
                        <h3>Your cart is empty</h3>
                        <p>Add products to start a sale</p>
                    </div>
                `;
                return;
            }

            // Build cart items HTML
            let cartHTML = '';
            cart.forEach(item => {
                cartHTML += `
                    <div class="cart-item" data-product-id="${item.id}">
                        ${item.imageUrl ?
                            `<img src="${item.imageUrl}" alt="${item.name}" class="cart-item-image" onerror="this.style.display='none'; this.nextElementSibling.style.display='flex';">
                             <div class="cart-item-image" style="display: none;">
                                 <i class="fas fa-cube"></i>
                             </div>` :
                            `<div class="cart-item-image">
                                 <i class="fas fa-cube"></i>
                             </div>`
                        }

                        <div class="cart-item-details">
                            <div class="cart-item-name" title="${item.name}">${item.name}</div>
                            <div class="cart-item-sku">SKU: ${item.sku}</div>
                            <div class="cart-item-price">KSh ${item.price.toLocaleString('en-KE', { minimumFractionDigits: 2 })} each</div>
                        </div>

                        <div class="quantity-controls">
                            <button class="quantity-btn" onclick="updateQuantity('${item.id}', -1)" type="button">
                                <i class="fas fa-minus"></i>
                            </button>
                            <div class="quantity-display">${item.quantity}</div>
                            <button class="quantity-btn" onclick="updateQuantity('${item.id}', 1)" type="button">
                                <i class="fas fa-plus"></i>
                            </button>
                        </div>

                        <div style="text-align: right; margin-left: 0.5rem;">
                            <div style="font-weight: 700; color: #10b981; font-size: 0.9rem;">
                                KSh ${item.total.toLocaleString('en-KE', { minimumFractionDigits: 2 })}
                            </div>
                            <button class="remove-btn" onclick="removeFromCart('${item.id}')" type="button" title="Remove item">
                                <i class="fas fa-trash"></i>
                            </button>
                        </div>
                    </div>
                `;
            });

            cartItemsContainer.innerHTML = cartHTML;

            // Auto-scroll to bottom when new items are added
            cartItemsContainer.scrollTop = cartItemsContainer.scrollHeight;
        }

        // Update quantity in cart
        function updateQuantity(productId, change) {
            console.log(`🔢 Updating quantity for product ${productId}, change: ${change}`);

            const itemIndex = cart.findIndex(item => item.id == productId);
            if (itemIndex === -1) return;

            const item = cart[itemIndex];
            const newQuantity = item.quantity + change;

            if (newQuantity <= 0) {
                removeFromCart(productId);
                return;
            }

            if (newQuantity > item.maxStock) {
                showToast(`Cannot exceed available stock (${item.maxStock})`, 'error');
                return;
            }

            item.quantity = newQuantity;
            item.total = item.quantity * item.price;

            updateCartDisplay();
        }

        // Remove item from cart
        function removeFromCart(productId) {
            console.log(`🗑️ Removing product ${productId} from cart`);

            const itemIndex = cart.findIndex(item => item.id == productId);
            if (itemIndex !== -1) {
                const itemName = cart[itemIndex].name;
                cart.splice(itemIndex, 1);
                showToast(`Removed ${itemName} from cart`, 'info');
                updateCartDisplay();
            }
        }

        // Clear entire cart
        function clearCart() {
            if (cart.length === 0) return;

            if (confirm('Are you sure you want to clear the entire cart?')) {
                console.log('🧹 Clearing entire cart');
                cart = [];
                updateCartDisplay();
                showToast('Cart cleared', 'info');
            }
        }

        // Hold sale (save to localStorage)
        function holdSale() {
            if (cart.length === 0) return;

            if (confirm('Hold this sale for later?')) {
                const heldSaleId = 'heldSale_' + Date.now();
                localStorage.setItem(heldSaleId, JSON.stringify(cart));
                cart = [];
                updateCartDisplay();
                showToast('Sale held successfully', 'info');
                console.log('💾 Sale held with ID:', heldSaleId);
            }
        }

        // Payment modal functions
        function openPaymentModal() {
            if (cart.length === 0) {
                showToast('Cart is empty. Add some products first.', 'error');
                return;
            }

            console.log('💳 Opening payment modal, total:', currentTotal);
            document.getElementById('modalTotalAmount').textContent = `KSh ${currentTotal.toLocaleString('en-KE', { minimumFractionDigits: 2 })}`;
            document.getElementById('paymentModal').classList.add('show');
            resetPaymentForm();
        }

        function closePaymentModal() {
            console.log('Closing payment modal');
            document.getElementById('paymentModal').classList.remove('show');
            resetPaymentForm();
        }

        function selectPaymentMethod(method) {
            console.log('Selected payment method:', method);
            selectedPaymentMethod = method;

            // Update selected state
            document.querySelectorAll('.payment-option').forEach(option => {
                option.classList.remove('selected');
            });
            event.currentTarget.classList.add('selected');

            // Hide method selection
            document.getElementById('paymentMethodSelection').style.display = 'none';

            // Show appropriate form
            if (method === 'cash') {
                document.getElementById('cashPaymentForm').style.display = 'block';
                document.getElementById('cashReceived').focus();
                // Set exact amount by default
                document.getElementById('cashReceived').value = currentTotal.toFixed(2);
                calculateChange();
            } else if (method === 'mpesa') {
                document.getElementById('mpesaPaymentForm').style.display = 'block';
                document.getElementById('customerPhone').focus();
            }

            updateCompleteButton();
        }

        function resetPaymentForm() {
            selectedPaymentMethod = null;

            // Show method selection
            document.getElementById('paymentMethodSelection').style.display = 'block';

            // Hide forms
            document.getElementById('cashPaymentForm').style.display = 'none';
            document.getElementById('mpesaPaymentForm').style.display = 'none';

            // Reset form values
            document.getElementById('cashReceived').value = '';
            document.getElementById('customerPhone').value = '';
            document.getElementById('changeDisplay').style.display = 'none';

            // Remove selected state
            document.querySelectorAll('.payment-option').forEach(option => {
                option.classList.remove('selected');
            });

            updateCompleteButton();
        }

        function setQuickAmount(amount) {
            const cashInput = document.getElementById('cashReceived');

            if (amount === 'exact') {
                cashInput.value = currentTotal.toFixed(2);
            } else {
                cashInput.value = amount.toFixed(2);
            }

            calculateChange();
        }

        function calculateChange() {
            const cashReceived = parseFloat(document.getElementById('cashReceived').value) || 0;
            const change = cashReceived - currentTotal;

            const changeDisplay = document.getElementById('changeDisplay');
            const changeAmount = document.getElementById('changeAmount');
            const changeLabel = document.getElementById('changeLabel');

            if (cashReceived > 0) {
                changeDisplay.style.display = 'block';

                if (change >= 0) {
                    changeDisplay.className = 'change-display positive';
                    changeLabel.textContent = 'Change:';
                    changeAmount.textContent = `KSh ${change.toLocaleString('en-KE', { minimumFractionDigits: 2 })}`;
                } else {
                    changeDisplay.className = 'change-display negative';
                    changeLabel.textContent = 'Short by:';
                    changeAmount.textContent = `KSh ${Math.abs(change).toLocaleString('en-KE', { minimumFractionDigits: 2 })}`;
                }
            } else {
                changeDisplay.style.display = 'none';
            }

            updateCompleteButton();
        }

        function updateCompleteButton() {
            const completeBtn = document.getElementById('completePaymentBtn');
            let canComplete = false;

            if (selectedPaymentMethod === 'cash') {
                const cashReceived = parseFloat(document.getElementById('cashReceived').value) || 0;
                canComplete = cashReceived >= currentTotal;
            } else if (selectedPaymentMethod === 'mpesa') {
                const phone = document.getElementById('customerPhone').value;
                canComplete = phone && phone.length === 9 && /^[0-9]+$/.test(phone);
            }

            completeBtn.disabled = !canComplete;
        }

        // Complete payment - FIXED VERSION
        async function completePayment() {
            console.log('Processing payment...');

            if (!selectedPaymentMethod) {
                showToast('Please select a payment method', 'error');
                return;
            }

            if (cart.length === 0) {
                showToast('Cart is empty', 'error');
                return;
            }

            const completeBtn = document.getElementById('completePaymentBtn');
            const originalText = completeBtn.innerHTML;

            // Show loading state
            completeBtn.disabled = true;
            completeBtn.innerHTML = '<div class="loading"></div> Processing...';

            try {
                // Use the already calculated currentTotal to ensure consistency with display
                // This matches exactly what's shown to the user in updateCartDisplay()
                const totalAmount = currentTotal;
                const subtotal = cart.reduce((sum, item) => sum + item.total, 0);
                const tax = totalAmount * 0.15; // 15% of the selling price
                const netAmount = totalAmount - tax; // Net amount after tax deduction

                let saleData = {
                    items: cart.map(item => ({
                        productId: item.id,
                        quantity: item.quantity,
                        unitPrice: item.price,
                        totalPrice: item.total
                    })),
                    totalAmount: totalAmount,
                    paymentMethod: selectedPaymentMethod === 'cash' ? 'Cash' : 'M-Pesa',
                    customerName: '',
                    customerPhone: '',
                    customerEmail: ''
                };

                console.log('DEBUG: Sending sale data:', saleData);
                console.log('DEBUG: Cart items with product IDs:', cart.map(item => ({ 
                    productId: item.id, 
                    productIdType: typeof item.id,
                    name: item.name 
                })));

                if (selectedPaymentMethod === 'cash') {
                    const cashReceived = parseFloat(document.getElementById('cashReceived').value) || 0;
                    const change = cashReceived - totalAmount;

                    if (change < 0) {
                        showToast(`Insufficient amount. Need KSh ${Math.abs(change).toFixed(2)} more.`, 'error');
                        completeBtn.disabled = false;
                        completeBtn.innerHTML = originalText;
                        return;
                    }

                    saleData.amountPaid = cashReceived;
                    saleData.changeGiven = Math.max(0, change);
                } else if (selectedPaymentMethod === 'mpesa') {
                    const phone = document.getElementById('customerPhone').value;
                    saleData.customerPhone = '+254' + phone;
                    saleData.amountPaid = totalAmount;
                    saleData.changeGiven = 0;
                }

                console.log('Sending sale data:', saleData);

                // Get CSRF token
                const tokenInput = document.querySelector('#hiddenTokenForm input[name="__RequestVerificationToken"]');
                const token = tokenInput ? tokenInput.value : '';
                
                console.log('CSRF token found:', token ? 'Yes' : 'No');
                console.log('Calling endpoint: /Sales/ProcessSale');

                const response = await fetch('/Sales/ProcessSale', {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                        'RequestVerificationToken': token
                    },
                    body: JSON.stringify(saleData)
                });

                console.log('Response status:', response.status, response.statusText);
                
                // Check if response is ok
                if (!response.ok) {
                    const errorText = await response.text();
                    console.error('HTTP Error:', response.status, response.statusText, errorText);
                    showToast(`Server error (${response.status}): ${response.statusText}`, 'error');
                    throw new Error(`Server error: ${response.status} - ${response.statusText}`);
                }

                const contentType = response.headers.get('content-type');
                if (!contentType || !contentType.includes('application/json')) {
                    const responseText = await response.text();
                    console.error('Non-JSON response received:', responseText);
                    showToast('Server returned invalid response format', 'error');
                    throw new Error('Server returned non-JSON response');
                }

                const result = await response.json();
                console.log('Sale response received:', result);

                if (result.success) {
                    showToast('Sale completed successfully!', 'success');

                    // Store sale data for receipt
                    lastSaleData = {
                        saleId: result.saleId,
                        saleNumber: result.saleNumber,
                        totalAmount: result.totalAmount,
                        changeGiven: result.changeGiven,
                        cashierName: result.cashierName, // Use actual cashier name from backend
                        items: [...cart],
                        paymentMethod: selectedPaymentMethod,
                        amountPaid: saleData.amountPaid,
                        customerPhone: saleData.customerPhone
                    };

                    // Clear cart and close modal
                    cart = [];
                    updateCartDisplay();
                    closePaymentModal();

                    // Update today's stats
                    updateTodayStats();

                    // Generate and show receipt
                    generateAndShowReceipt(lastSaleData);

                    // Reload products to update stock
                    setTimeout(() => {
                        loadProducts();
                    }, 1000);

                    console.log('Sale completed successfully:', result.saleNumber);
                } else {
                    console.error('Sale failed:', result.message);
                    showToast(result.message || 'Sale processing failed', 'error');
                }
            } catch (error) {
                console.error('Error processing sale:', error);
                showToast(`Payment processing error: ${error.message}`, 'error');
            } finally {
                // Reset button
                completeBtn.disabled = false;
                completeBtn.innerHTML = originalText;
            }
        }

        // Generate and show receipt
        function generateAndShowReceipt(saleData) {
            const receiptContent = document.getElementById('receiptContent');

            const receiptHTML = `
                <div class="receipt-header">
                    <h2 style="margin: 0; color: #1f2937; font-size: 1.5rem;">PIXEL SOLUTION</h2>
                    <p style="margin: 0.25rem 0; color: #6b7280;">Chuka, Ndangani</p>
                    <p style="margin: 0; color: #6b7280;">Tel: +254758024400</p>
                </div>
                <div class="receipt-content">
                    <div style="text-align: center; margin-bottom: 1rem; border-bottom: 1px dashed #000; padding-bottom: 0.5rem;">
                        <div><strong>Receipt: ${saleData.saleNumber}</strong></div>
                        <div>Date: ${new Date().toLocaleString()}</div>
                        <div>Served by: ${saleData.cashierName}</div>
                    </div>

                    <div style="margin-bottom: 1rem;">
                        ${saleData.items.map(item => `
                            <div class="receipt-item">
                                <div>
                                    <div style="font-weight: bold;">${item.name}</div>
                                    <div style="font-size: 0.8rem; color: #666;">SKU: ${item.sku}</div>
                                </div>
                                <div style="text-align: right;">
                                    <div>${item.quantity} x KSh ${item.price.toFixed(2)}</div>
                                    <div style="font-weight: bold;">KSh ${item.total.toFixed(2)}</div>
                                </div>
                            </div>
                        `).join('')}
                    </div>

                    <div style="border-top: 1px dashed #000; padding-top: 0.5rem;">
                        <div class="receipt-item">
                            <span>Subtotal:</span>
                            <span>KSh ${(saleData.totalAmount / 1.16).toFixed(2)}</span>
                        </div>
                        <div class="receipt-item">
                            <span>Tax (16%):</span>
                            <span>KSh ${(saleData.totalAmount * 0.16 / 1.16).toFixed(2)}</span>
                        </div>
                        <div class="receipt-item receipt-total">
                            <span><strong>TOTAL:</strong></span>
                            <span><strong>KSh ${saleData.totalAmount.toFixed(2)}</strong></span>
                        </div>
                        <div class="receipt-item">
                            <span>Amount Paid:</span>
                            <span>KSh ${saleData.amountPaid.toFixed(2)}</span>
                        </div>
                        ${saleData.changeGiven > 0 ? `
                        <div class="receipt-item">
                            <span>Change:</span>
                            <span>KSh ${saleData.changeGiven.toFixed(2)}</span>
                        </div>
                        ` : ''}
                    </div>

                    <div style="text-align: center; margin-top: 1rem; padding-top: 0.5rem; border-top: 1px dashed #000; font-size: 0.8rem;">
                        <div>Payment Method: ${saleData.paymentMethod}</div>
                        ${saleData.customerPhone ? `<div>Phone: ${saleData.customerPhone}</div>` : ''}
                        <div style="margin-top: 0.5rem; font-style: italic;">Thank you for your business!</div>
                    </div>
                </div>
            `;

            receiptContent.innerHTML = receiptHTML;
            document.getElementById('receiptModal').classList.add('show');
        }

        function closeReceiptModal() {
            document.getElementById('receiptModal').classList.remove('show');
        }

        function printReceipt() {
            const receiptContent = document.getElementById('receiptContent').innerHTML;
            const printWindow = window.open('', '_blank');
            printWindow.document.write(`
                <!DOCTYPE html>
                <html>
                <head>
                    <title>Receipt - ${lastSaleData?.saleNumber || 'Sale'}</title>
                    <style>
                        body { font-family: 'Courier New', monospace; margin: 0; padding: 20px; }
                        .receipt-preview { max-width: 400px; margin: 0 auto; }
                        .receipt-header { text-align: center; margin-bottom: 1rem; }
                        .receipt-content { font-size: 14px; line-height: 1.4; }
                        .receipt-item { display: flex; justify-content: space-between; margin-bottom: 0.5rem; }
                        .receipt-total { border-top: 2px solid #000; padding-top: 0.5rem; font-weight: bold; }
        @media print { body { margin: 0; } }
                    </style>
                </head>
                <body>
                    <div class="receipt-preview">${receiptContent}</div>
                    <script>window.onload = function() { window.print(); window.close(); }</script>
                    </body>
                    </html>
            `);
            printWindow.document.close();
}

        async function downloadReceiptPDF() {
            if (!lastSaleData) {
                showToast('No receipt data available', 'error');
                return;
            }

            try {
                showToast('Generating PDF receipt...', 'info');

                // Calculate subtotal and tax
                const subtotal = lastSaleData.totalAmount / 1.16;
                const tax = lastSaleData.totalAmount - subtotal;

                // Prepare receipt data for server
                const receiptData = {
                    saleNumber: lastSaleData.saleNumber,
                    saleDate: new Date(lastSaleData.saleDate || new Date()),
                    cashierName: lastSaleData.cashierName || 'Unknown',
                    customerName: lastSaleData.customerName || '',
                    customerPhone: lastSaleData.customerPhone || '',
                    paymentMethod: lastSaleData.paymentMethod || 'Cash',
                    totalAmount: lastSaleData.totalAmount,
                    amountPaid: lastSaleData.amountPaid,
                    changeGiven: lastSaleData.changeGiven || 0,
                    subtotal: subtotal,
                    tax: tax,
                    items: lastSaleData.items.map(item => ({
                        name: item.name,
                        quantity: item.quantity,
                        unitPrice: item.unitPrice || (item.total / item.quantity),
                        total: item.total
                    }))
                };

                // Get CSRF token
                const token = document.querySelector('input[name="__RequestVerificationToken"]').value;

                // Call server endpoint to generate PDF
                const response = await fetch('/Admin/GenerateReceiptPDF', {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                        'RequestVerificationToken': token
                    },
                    body: JSON.stringify(receiptData)
                });

                if (response.ok) {
                    // Get the PDF blob
                    const blob = await response.blob();
                    
                    // Create download link
                    const url = URL.createObjectURL(blob);
                    const a = document.createElement('a');
                    a.href = url;
                    a.download = `Receipt_${lastSaleData.saleNumber}_${new Date().toISOString().split('T')[0]}.pdf`;
                    document.body.appendChild(a);
                    a.click();
                    document.body.removeChild(a);
                    URL.revokeObjectURL(url);

                    showToast('Receipt PDF downloaded successfully!', 'success');
                } else {
                    const errorData = await response.json();
                    showToast(errorData.message || 'Failed to generate PDF receipt', 'error');
                }
            } catch (error) {
                console.error('Error downloading receipt PDF:', error);
                showToast('Error generating PDF receipt', 'error');
            }
        }

        // Setup search functionality
        function setupSearchFunctionality() {
            const searchInput = document.getElementById('productSearch');
            const categoryFilter = document.getElementById('categoryFilter');
            const stockFilter = document.getElementById('stockFilter');

            if (searchInput) {
                searchInput.addEventListener('input', debounce(filterProducts, 300));
            }
            if (categoryFilter) {
                categoryFilter.addEventListener('change', filterProducts);
            }
            if (stockFilter) {
                stockFilter.addEventListener('change', filterProducts);
            }
        }

        // Filter products
        function filterProducts() {
            const searchTerm = document.getElementById('productSearch').value.toLowerCase();
            const categoryFilter = document.getElementById('categoryFilter').value;
            const stockFilter = document.getElementById('stockFilter').value;

            let filteredProducts = allProducts.filter(product => {
                // Search filter
                const matchesSearch = !searchTerm ||
                    product.name.toLowerCase().includes(searchTerm) ||
                    product.sku.toLowerCase().includes(searchTerm) ||
                    (product.categoryName && product.categoryName.toLowerCase().includes(searchTerm));

                // Category filter
                const matchesCategory = !categoryFilter || product.categoryId == categoryFilter;

                // Stock filter
                let matchesStock = true;
                if (stockFilter === 'in-stock') {
                    matchesStock = product.stockQuantity > 5;
                } else if (stockFilter === 'low-stock') {
                    matchesStock = product.stockQuantity <= 5 && product.stockQuantity > 0;
                }

                return matchesSearch && matchesCategory && matchesStock;
            });

            displayProducts(filteredProducts);
        }

        // Setup keyboard shortcuts
        function setupKeyboardShortcuts() {
            document.addEventListener('keydown', function(e) {
                // F2 for checkout
                if (e.key === 'F2' && cart.length > 0) {
                    e.preventDefault();
                    openPaymentModal();
                }

                // F3 for clear cart
                if (e.key === 'F3' && cart.length > 0) {
                    e.preventDefault();
                    clearCart();
                }

                // Enter key in payment modal
                if (e.key === 'Enter' && document.getElementById('paymentModal').classList.contains('show')) {
                    e.preventDefault();
                    const completeBtn = document.getElementById('completePaymentBtn');
                    if (!completeBtn.disabled) {
                        completePayment();
                    }
                }
            });
        }

        // Setup event listeners
        function setupEventListeners() {
            // Cash input change listener
            const cashInput = document.getElementById('cashReceived');
            if (cashInput) {
                cashInput.addEventListener('input', () => {
                    calculateChange();
                    updateCompleteButton();
                });
            }

            // Phone input change listener
            const phoneInput = document.getElementById('customerPhone');
            if (phoneInput) {
                phoneInput.addEventListener('input', updateCompleteButton);
            }

            // Modal close on outside click
            document.getElementById('paymentModal').addEventListener('click', function(e) {
                if (e.target === this) {
                    closePaymentModal();
                }
            });

            document.getElementById('receiptModal').addEventListener('click', function(e) {
                if (e.target === this) {
                    closeReceiptModal();
                }
            });
        }

        // Load today's stats on page load
        async function loadTodayStats() {
            try {
                const response = await fetch('/Admin/GetTodaysSalesStats', {
                    method: 'GET',
                    headers: {
                        'Content-Type': 'application/json',
                        'Cache-Control': 'no-cache'
                    }
                });
                
                if (response.ok) {
                    const data = await response.json();

                    if (data.success) {
                        document.getElementById('todaySales').textContent = `KSh ${data.stats.totalSales.toLocaleString('en-KE', { minimumFractionDigits: 2 })}`;
                        document.getElementById('todayTransactions').textContent = data.stats.transactionCount.toString();
                        document.getElementById('avgTransaction').textContent = `KSh ${data.stats.averageTransaction.toLocaleString('en-KE', { minimumFractionDigits: 2 })}`;
                        console.log('✅ Initial stats loaded:', data.stats);
                    }
                }
            } catch (error) {
                console.error('❌ Could not load today\'s stats:', error);
            }
        }

        // Update today's stats after sale
        async function updateTodayStats() {
            try {
                console.log('🔄 Updating today\'s stats...');
                const response = await fetch('/Admin/GetTodaysSalesStats', {
                    method: 'GET',
                    headers: {
                        'Content-Type': 'application/json'
                    }
                });

                console.log('📊 Stats response status:', response.status);
                
                if (response.ok) {
                    const result = await response.json();
                    console.log('📊 Stats response data:', result);
                    
                    if (result.success) {
                        const stats = result.stats;
                        console.log('💰 Updating cards with stats:', stats);
                        
                        document.getElementById('todaySales').textContent = `KSh ${stats.totalSales.toLocaleString('en-KE', { minimumFractionDigits: 2 })}`;
                        document.getElementById('todayTransactions').textContent = stats.transactionCount.toString();
                        document.getElementById('avgTransaction').textContent = `KSh ${stats.averageTransaction.toLocaleString('en-KE', { minimumFractionDigits: 2 })}`;
                        
                        console.log('✅ Sales cards updated successfully');
                    } else {
                        console.error('❌ Stats API returned success=false:', result.message);
                    }
                } else {
                    const errorText = await response.text();
                    console.error('❌ Stats API error:', response.status, errorText);
                }
            } catch (error) {
                console.error('❌ Error updating today\'s stats:', error);
            }
        }

        // Toast notification system
        function showToast(message, type) {
            // Remove existing toasts
            const existingToasts = document.querySelectorAll('.toast');
            existingToasts.forEach(toast => toast.remove());

            const toast = document.createElement('div');
            toast.className = `toast ${type}`;
            toast.textContent = message;
            toast.style.cssText = `
                position: fixed;
                top: 20px;
                right: 20px;
                z-index: 9999;
                padding: 1rem 1.5rem;
                border-radius: 12px;
                color: white;
                font-weight: 600;
                opacity: 0;
                transform: translateX(100%);
                transition: all 0.3s ease;
                max-width: 300px;
                box-shadow: 0 10px 30px rgba(0,0,0,0.3);
            `;

            switch(type) {
                case 'success':
                    toast.style.background = 'linear-gradient(135deg, #10b981, #059669)';
                    break;
                case 'error':
                    toast.style.background = 'linear-gradient(135deg, #ef4444, #dc2626)';
                    break;
                case 'warning':
                    toast.style.background = 'linear-gradient(135deg, #f59e0b, #d97706)';
                    break;
                case 'info':
                    toast.style.background = 'linear-gradient(135deg, #3b82f6, #2563eb)';
                    break;
            }

            document.body.appendChild(toast);

            // Show toast
            setTimeout(() => {
                toast.style.opacity = '1';
                toast.style.transform = 'translateX(0)';
            }, 100);

            // Hide toast after 3 seconds
            setTimeout(() => {
                toast.style.opacity = '0';
                toast.style.transform = 'translateX(100%)';
                setTimeout(() => {
                    toast.remove();
                }, 300);
            }, 3000);
        }

        // Utility function for debouncing
        function debounce(func, wait) {
            let timeout;
            return function executedFunction(...args) {
                const later = () => {
                    clearTimeout(timeout);
                    func(...args);
                };
                clearTimeout(timeout);
                timeout = setTimeout(later, wait);
            };
        }

        // Initialize cart display and load initial stats on page load
        updateCartDisplay();
        
        // Force update stats immediately and repeatedly for debugging
        console.log('🚀 Forcing stats update on page load...');
        updateTodayStats();
        
        // Also update stats every 2 seconds for debugging
        setInterval(() => {
            console.log('⏰ Auto-updating stats...');
            updateTodayStats();
        }, 2000);