// Global variables
let cart = [];
let allProducts = [];
let allCategories = [];
let selectedPaymentMethod = null;
let currentTotal = 0;

document.addEventListener('DOMContentLoaded', function() {
    console.log('🚀 Sales page loaded - Edit button functionality enabled');
    console.log('🔧 JavaScript file version: 2025-08-21-22:40');
    loadCategories();
    loadProducts();
    updateCartDisplay();
    setupSearchFunctionality();
    loadTodayStats();
    loadNotifications();
    setupKeyboardShortcuts();
    
    // Test edit button functionality
    window.testEditButton = function() {
        console.log('🧪 Testing edit button functionality');
        if (cart.length === 0) {
            console.log('❌ Cart is empty - add a product first');
            return false;
        }
        console.log('✅ Cart has items:', cart.length);
        const editButtons = document.querySelectorAll('.edit-price-btn');
        console.log('🔍 Edit buttons found:', editButtons.length);
        return editButtons.length > 0;
    };
});

// Load categories from server
async function loadCategories() {
    try {
        console.log('Loading categories from server...');
        
        // Determine the correct endpoint based on current page
        const currentPath = window.location.pathname;
        const endpoint = currentPath.includes('/Admin/') ? '/Admin/GetCategories' : '/Employee/GetCategories';
        console.log('🔗 Using categories endpoint:', endpoint);
        
        const response = await fetch(endpoint);
        
        if (!response.ok) {
            throw new Error(`HTTP error! status: ${response.status}`);
        }

        const data = await response.json();
        console.log('Categories API response:', data);

        if (data.success && data.categories) {
            allCategories = data.categories;
            
            const categoryFilter = document.getElementById('categoryFilter');
            if (categoryFilter) {
                // Clear existing options except "All Categories"
                categoryFilter.innerHTML = '<option value="">All Categories</option>';
                
                allCategories.forEach(category => {
                    const option = document.createElement('option');
                    option.value = category.categoryId;
                    option.textContent = category.name;
                    categoryFilter.appendChild(option);
                });

                console.log(`Loaded ${allCategories.length} categories`);
            }
        } else {
            console.error('Failed to load categories:', data.message || 'Unknown error');
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

        // Determine the correct endpoint based on current page
        const currentPath = window.location.pathname;
        const endpoint = currentPath.includes('/Admin/') ? '/Admin/GetProductsForSale' : '/Employee/GetProductsForSale';
        console.log('🔗 Using endpoint:', endpoint);

        const response = await fetch(endpoint);

        if (!response.ok) {
            throw new Error(`HTTP error! status: ${response.status}`);
        }

        const data = await response.json();
        console.log('📦 Products API response:', data);

        if (data.success && data.products) {
            allProducts = data.products;
            console.log(`✅ Loaded ${allProducts.length} products`);
            
            // Debug: Log product IDs and image URLs to verify they're correct
            console.log('🔍 Product details loaded:', allProducts.map(p => ({ 
                id: p.id, 
                name: p.name, 
                imageUrl: p.imageUrl,
                hasImage: !!(p.imageUrl && p.imageUrl.trim() !== '')
            })));
            
            // Debug image URLs specifically
            const productsWithImages = allProducts.filter(p => p.imageUrl && p.imageUrl.trim() !== '');
            const productsWithoutImages = allProducts.filter(p => !p.imageUrl || p.imageUrl.trim() === '');
            console.log(`🖼️ Products with images: ${productsWithImages.length}`);
            console.log(`📦 Products without images: ${productsWithoutImages.length}`);
            
            if (productsWithImages.length > 0) {
                console.log('🖼️ Sample image URLs:', productsWithImages.slice(0, 3).map(p => ({
                    name: p.name,
                    imageUrl: p.imageUrl,
                    isValidUrl: isValidImageUrl(p.imageUrl)
                })));
            }
            
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

// Validate image URL
function isValidImageUrl(url) {
    if (!url || typeof url !== 'string' || url.trim() === '') {
        return false;
    }
    
    try {
        const urlObj = new URL(url);
        const validProtocols = ['http:', 'https:', 'data:'];
        const validExtensions = ['.jpg', '.jpeg', '.png', '.gif', '.webp', '.svg', '.bmp'];
        
        if (!validProtocols.includes(urlObj.protocol)) {
            console.warn(`Invalid protocol for image URL: ${url}`);
            return false;
        }
        
        // Check if it's a data URL (base64 image)
        if (urlObj.protocol === 'data:') {
            return url.startsWith('data:image/');
        }
        
        // Check file extension
        const pathname = urlObj.pathname.toLowerCase();
        const hasValidExtension = validExtensions.some(ext => pathname.endsWith(ext));
        
        if (!hasValidExtension) {
            console.warn(`No valid image extension found for URL: ${url}`);
        }
        
        return true;
    } catch (error) {
        console.warn(`Invalid URL format: ${url}`, error.message);
        return false;
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
        productCard.dataset.productId = product.id;
        productCard.dataset.categoryId = product.categoryId || '';
        productCard.dataset.stockStatus = stockBadgeClass;

        if (product.stockQuantity > 0) {
            // CRITICAL FIX: Use proper event listener to prevent closure issues
            productCard.addEventListener('click', function() {
                console.log(`🖱️ Product card clicked: ID=${product.id}, Name=${product.name}`);
                addToCart(product.id);
            });
        } else {
            productCard.addEventListener('click', function() {
                showStockWarning(product.name);
            });
        }

        productCard.innerHTML = `
            <div class="stock-badge ${stockBadgeClass}">
                ${stockText}
            </div>

            ${product.imageUrl && product.imageUrl.trim() !== '' ?
                `<img src="${product.imageUrl}" alt="${product.name}" class="product-image" 
                      onerror="console.warn('Failed to load image: ${product.imageUrl}'); this.style.display='none'; this.nextElementSibling.style.display='flex';"
                      onload="console.log('Successfully loaded image for: ${product.name}');">
                 <div class="product-image" style="display: none;">
                     <i class="fas fa-box"></i>
                 </div>` :
                `<div class="product-image">
                     <i class="fas fa-box"></i>
                 </div>`
            }

            <div class="product-info">
                <h3 class="product-name">${product.name}</h3>
                <p class="product-sku">SKU: ${product.sku}</p>
                <div class="product-price">KSh ${parseFloat(product.price).toLocaleString('en-KE', { minimumFractionDigits: 2 })}</div>
                <div class="product-stock">Stock: ${product.stockQuantity}</div>
            </div>
        `;

        productsGrid.appendChild(productCard);
    });

    container.innerHTML = '';
    container.appendChild(productsGrid);
}

// Add product to cart
function addToCart(productId) {
    console.log(`🛒 Adding product to cart: ${productId}`);
    
    const product = allProducts.find(p => p.id === productId);
    if (!product) {
        console.error(`Product with ID ${productId} not found`);
        showToast('Product not found', 'error');
        return;
    }

    if (product.stockQuantity <= 0) {
        showToast('Product is out of stock', 'error');
        return;
    }

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
        // CRITICAL FIX: Ensure proper number parsing and prevent NaN
        const productPrice = parseFloat(product.price) || 0;
        console.log(`🛒 Adding new item: ${product.name}, Price: ${productPrice}`);
        
        cart.push({
            id: productId,
            name: product.name,
            sku: product.sku,
            price: productPrice,
            quantity: 1,
            total: productPrice,
            maxStock: product.stockQuantity,
            imageUrl: product.imageUrl
        });
        console.log(`Added ${product.name} to cart with price ${productPrice}`);
        showToast(`Added ${product.name} to cart`, 'success');
    }

    updateCartDisplay();
    
    // Force add edit buttons as fallback
    setTimeout(() => {
        forceAddEditButtons();
    }, 100);
}

// Update cart display
function updateCartDisplay() {
    console.log('Updating cart display, items:', cart.length);

    const cartItemsContainer = document.getElementById('cartItems');
    const cartItemCount = document.getElementById('cartItemCount');
    const cartSubtotalDisplay = document.getElementById('cartSubtotalDisplay');

    // CRITICAL FIX: Ensure proper number calculations and prevent NaN
    const subtotal = cart.reduce((sum, item) => {
        const itemTotal = parseFloat(item.total) || 0;
        console.log(`Cart item: ${item.name}, total: ${itemTotal}`);
        return sum + itemTotal;
    }, 0);
    
    // Tax calculation: 16% VAT (standard Kenya rate)
    const total = subtotal;
    const tax = total * 0.16;
    const netAmount = total - tax;
    currentTotal = total;

    console.log(`💰 Cart calculations: Subtotal=${subtotal}, Tax=${tax}, Total=${total}`);

    // Update counters in header
    cartItemCount.textContent = cart.length;
    cartSubtotalDisplay.textContent = `KSh ${total.toLocaleString('en-KE', { minimumFractionDigits: 2 })}`;

    // Update total displays in footer
    document.getElementById('subtotalAmount').textContent = `KSh ${netAmount.toLocaleString('en-KE', { minimumFractionDigits: 2 })}`;
    document.getElementById('taxAmount').textContent = `KSh ${tax.toLocaleString('en-KE', { minimumFractionDigits: 2 })}`;
    document.getElementById('totalAmount').textContent = `KSh ${total.toLocaleString('en-KE', { minimumFractionDigits: 2 })}`;

    // Update button states
    const clearBtn = document.getElementById('clearBtn');
    const holdBtn = document.getElementById('holdBtn');
    const checkoutBtn = document.getElementById('checkoutBtn');

    if (cart.length > 0) {
        clearBtn.disabled = false;
        holdBtn.disabled = false;
        checkoutBtn.disabled = false;
    } else {
        clearBtn.disabled = true;
        holdBtn.disabled = true;
        checkoutBtn.disabled = true;
    }

    // Render cart items
    if (cart.length === 0) {
        cartItemsContainer.innerHTML = `
            <div class="empty-cart">
                <i class="fas fa-shopping-cart"></i>
                <h3>Your cart is empty</h3>
                <p>Add products to start a sale</p>
            </div>
        `;
    } else {
        console.log('🛒 Rendering cart with edit buttons enabled');
        cartItemsContainer.innerHTML = cart.map(item => `
            <div class="cart-item">
                ${item.imageUrl && item.imageUrl.trim() !== '' ? 
                    `<img src="${item.imageUrl}" alt="${item.name}" class="cart-item-image" 
                          onerror="console.warn('Cart image failed: ${item.imageUrl}'); this.style.display='none'; this.nextElementSibling.style.display='flex';"
                          onload="console.log('Cart image loaded for: ${item.name}');">
                     <div class="cart-item-image" style="display: none;"><i class="fas fa-box"></i></div>` :
                    `<div class="cart-item-image"><i class="fas fa-box"></i></div>`
                }
                
                <div class="cart-item-details">
                    <div class="cart-item-name">${item.name}</div>
                    <div class="cart-item-sku">${item.sku}</div>
                    <div class="cart-item-price">KSh ${item.price.toLocaleString('en-KE', { minimumFractionDigits: 2 })} each</div>
                </div>
                
                <div class="cart-item-controls">
                    <div class="quantity-controls">
                        <button class="quantity-btn" onclick="updateQuantity(${item.id}, -1)" ${item.quantity <= 1 ? 'disabled' : ''}>-</button>
                        <span class="quantity-display">${item.quantity}</span>
                        <button class="quantity-btn" onclick="updateQuantity(${item.id}, 1)" ${item.quantity >= item.maxStock ? 'disabled' : ''}>+</button>
                    </div>
                    <button class="edit-price-btn" onclick="editItemPrice(${item.id})" title="Edit Price" style="width: 28px; height: 28px; border: none; background: #dbeafe; color: #2563eb; border-radius: 6px; display: flex; align-items: center; justify-content: center; cursor: pointer; font-size: 0.75rem; transition: all 0.2s; margin-left: 0.25rem;">
                        <i class="fas fa-edit"></i>
                    </button>
                    <button class="remove-btn" onclick="removeFromCart(${item.id})" style="margin-left: 0.25rem;">
                        <i class="fas fa-trash"></i>
                    </button>
                </div>
            </div>
        `).join('');
        
        // Force add edit buttons as fallback
        setTimeout(() => {
            forceAddEditButtons();
        }, 100);
    }
}

// Load today's stats
async function loadTodayStats() {
    console.log('📊 Loading today\'s stats...');
    await updateTodayStats();
}

// Update today's stats
async function updateTodayStats() {
    console.log('🔄 Updating today\'s stats...');
    try {
        // Determine the correct endpoint based on current page
        const currentPath = window.location.pathname;
        const endpoint = currentPath.includes('/Admin/') ? '/Admin/GetTodaysSalesStats' : '/Employee/GetTodaysSalesStats';
        console.log('🔗 Using stats endpoint:', endpoint);
        
        const response = await fetch(endpoint);
        console.log('📊 Stats response status:', response.status);
        
        if (!response.ok) {
            throw new Error(`HTTP error! status: ${response.status}`);
        }
        
        const data = await response.json();
        console.log('📊 Stats response data:', data);
        
        if (data.success && data.stats) {
            console.log('💰 Updating cards with stats:', data.stats);
            
            // Update the stats cards with proper number formatting
            const todaySalesElement = document.getElementById('todaySales');
            const todayTransactionsElement = document.getElementById('todayTransactions');
            const avgTransactionElement = document.getElementById('avgTransaction');
            
            if (todaySalesElement) {
                const totalSales = parseFloat(data.stats.totalSales) || 0;
                todaySalesElement.textContent = `KSh ${totalSales.toLocaleString('en-KE', { minimumFractionDigits: 2 })}`;
            }
            if (todayTransactionsElement) {
                const transactionCount = parseInt(data.stats.transactionCount) || 0;
                todayTransactionsElement.textContent = transactionCount;
            }
            if (avgTransactionElement) {
                const avgTransaction = parseFloat(data.stats.averageTransaction) || 0;
                avgTransactionElement.textContent = `KSh ${avgTransaction.toLocaleString('en-KE', { minimumFractionDigits: 2 })}`;
            }
            
            console.log('✅ Sales cards updated successfully');
        } else {
            console.error('❌ Failed to get stats:', data.message || 'Unknown error');
        }
    } catch (error) {
        console.error('❌ Error updating today\'s stats:', error);
    }
}

// Load notifications
async function loadNotifications() {
    try {
        // Determine the correct endpoint based on current page
        const currentPath = window.location.pathname;
        const endpoint = currentPath.includes('/Admin/') ? '/Admin/GetNotifications' : '/Employee/GetNotifications';
        console.log('🔔 Using notifications endpoint:', endpoint);
        
        const response = await fetch(endpoint);
        
        if (!response.ok) {
            console.warn(`Notifications endpoint returned ${response.status}, skipping notifications`);
            return;
        }
        
        const text = await response.text();
        console.log('🔔 Raw notifications response:', text);
        
        // Check if response is empty or invalid JSON
        if (!text || text.trim() === '') {
            console.log('🔔 Empty notifications response, skipping');
            return;
        }
        
        try {
            const data = JSON.parse(text);
            console.log('🔔 Notifications loaded:', data);
            
            if (data.success && data.notifications) {
                displayNotifications(data.notifications);
            }
        } catch (jsonError) {
            console.warn('🔔 Invalid JSON in notifications response:', jsonError.message);
            console.warn('🔔 Response text:', text);
        }
    } catch (error) {
        console.warn('🔔 Error loading notifications (non-critical):', error.message);
    }
}

// Display notifications (placeholder function)
function displayNotifications(notifications) {
    console.log(`🔔 Would display ${notifications.length} notifications`);
    // Implementation for notification display can be added here
}

// Setup search functionality
function setupSearchFunctionality() {
    const searchInput = document.getElementById('searchInput');
    const categoryFilter = document.getElementById('categoryFilter');

    if (searchInput) {
        searchInput.addEventListener('input', filterProducts);
    }

    if (categoryFilter) {
        categoryFilter.addEventListener('change', filterProducts);
    }
}

// Filter products based on search and category
function filterProducts() {
    const searchTerm = document.getElementById('searchInput')?.value.toLowerCase() || '';
    const selectedCategory = document.getElementById('categoryFilter')?.value || '';

    let filteredProducts = allProducts;

    // Filter by search term
    if (searchTerm) {
        filteredProducts = filteredProducts.filter(product =>
            product.name.toLowerCase().includes(searchTerm) ||
            product.sku.toLowerCase().includes(searchTerm)
        );
    }

    // Filter by category
    if (selectedCategory) {
        filteredProducts = filteredProducts.filter(product =>
            product.categoryId == selectedCategory
        );
    }

    displayProducts(filteredProducts);
}

// Display empty state
function displayEmptyState(message) {
    const container = document.getElementById('productsContainer');
    container.innerHTML = `
        <div style="text-align: center; padding: 4rem; color: #64748b;">
            <i class="fas fa-box-open" style="font-size: 3rem; margin-bottom: 1rem; color: #d1d5db;"></i>
            <h3>${message}</h3>
            <p>Try adjusting your search or filters.</p>
        </div>
    `;
}

// Show stock warning
function showStockWarning(productName) {
    showToast(`${productName} is out of stock`, 'error');
}

// Toast notification system
function showToast(message, type) {
    // Remove existing toasts
    const existingToasts = document.querySelectorAll('.toast');
    existingToasts.forEach(toast => toast.remove());

    const toast = document.createElement('div');
    toast.className = `toast toast-${type}`;
    toast.innerHTML = `
        <div class="toast-content">
            <i class="fas ${type === 'success' ? 'fa-check-circle' : 'fa-exclamation-circle'}"></i>
            <span>${message}</span>
        </div>
    `;

    document.body.appendChild(toast);

    // Show toast
    setTimeout(() => toast.classList.add('show'), 100);

    // Hide and remove toast
    setTimeout(() => {
        toast.classList.remove('show');
        setTimeout(() => toast.remove(), 300);
    }, 3000);
}

// Setup keyboard shortcuts
function setupKeyboardShortcuts() {
    document.addEventListener('keydown', function(e) {
        // Ctrl/Cmd + K for search focus
        if ((e.ctrlKey || e.metaKey) && e.key === 'k') {
            e.preventDefault();
            const searchInput = document.getElementById('searchInput');
            if (searchInput) {
                searchInput.focus();
                searchInput.select();
            }
        }
        
        // Escape to clear search
        if (e.key === 'Escape') {
            const searchInput = document.getElementById('searchInput');
            if (searchInput && searchInput === document.activeElement) {
                searchInput.value = '';
                filterProducts();
                searchInput.blur();
            }
        }
    });
}

// Update quantity
function updateQuantity(productId, change) {
    const item = cart.find(item => item.id === productId);
    if (!item) return;

    const newQuantity = item.quantity + change;
    
    if (newQuantity <= 0) {
        removeFromCart(productId);
        return;
    }

    if (newQuantity > item.maxStock) {
        showToast('Cannot exceed available stock', 'error');
        return;
    }

    item.quantity = newQuantity;
    item.total = item.quantity * item.price;
    
    updateCartDisplay();
    showToast(`Updated ${item.name} quantity to ${newQuantity}`, 'success');
}

// Remove from cart
function removeFromCart(productId) {
    const itemIndex = cart.findIndex(item => item.id === productId);
    if (itemIndex > -1) {
        const item = cart[itemIndex];
        cart.splice(itemIndex, 1);
        updateCartDisplay();
        showToast(`Removed ${item.name} from cart`, 'success');
    }
}

// Clear cart
function clearCart() {
    if (cart.length === 0) return;
    
    if (confirm('Are you sure you want to clear the cart?')) {
        cart = [];
        updateCartDisplay();
        showToast('Cart cleared', 'success');
    }
}

// Hold sale (placeholder)
function holdSale() {
    showToast('Hold sale functionality not implemented yet', 'info');
}

// Open payment modal
function openPaymentModal() {
    if (cart.length === 0) {
        showToast('Cart is empty', 'error');
        return;
    }

    const modal = document.getElementById('paymentModal');
    const modalTotalAmount = document.getElementById('modalTotalAmount');
    
    if (modalTotalAmount) {
        modalTotalAmount.textContent = `KSh ${currentTotal.toLocaleString('en-KE', { minimumFractionDigits: 2 })}`;
    }
    
    if (modal) {
        modal.style.display = 'flex';
        document.body.style.overflow = 'hidden';
    }
}

// Close payment modal
function closePaymentModal() {
    const modal = document.getElementById('paymentModal');
    if (modal) {
        modal.style.display = 'none';
        document.body.style.overflow = 'auto';
    }
    
    // Reset payment method selection
    selectedPaymentMethod = null;
    const paymentOptions = document.querySelectorAll('.payment-option');
    paymentOptions.forEach(option => option.classList.remove('selected'));
    
    // Hide payment forms
    document.getElementById('cashPaymentForm').style.display = 'none';
    document.getElementById('mpesaPaymentForm').style.display = 'none';
    document.getElementById('paymentMethodSelection').style.display = 'block';
    
    // Reset form inputs
    document.getElementById('cashReceived').value = '';
    document.getElementById('customerPhone').value = '';
    document.getElementById('changeDisplay').style.display = 'none';
    document.getElementById('completePaymentBtn').disabled = true;
}

// Select payment method
function selectPaymentMethod(method) {
    console.log(`💳 Payment method selected: ${method}`);
    selectedPaymentMethod = method;
    
    // Update UI to show selected method
    const paymentOptions = document.querySelectorAll('.payment-option');
    paymentOptions.forEach(option => option.classList.remove('selected'));
    
    // Hide method selection and show appropriate form
    document.getElementById('paymentMethodSelection').style.display = 'none';
    
    if (method === 'cash') {
        document.getElementById('cashPaymentForm').style.display = 'block';
        document.getElementById('mpesaPaymentForm').style.display = 'none';
        
        // Focus on cash input
        setTimeout(() => {
            document.getElementById('cashReceived').focus();
        }, 100);
        
        // Add event listener for cash amount changes
        const cashInput = document.getElementById('cashReceived');
        cashInput.addEventListener('input', calculateChange);
        
    } else if (method === 'mpesa') {
        document.getElementById('cashPaymentForm').style.display = 'none';
        document.getElementById('mpesaPaymentForm').style.display = 'block';
        
        // Focus on phone input
        setTimeout(() => {
            document.getElementById('customerPhone').focus();
        }, 100);
        
        // Add event listener for phone number validation
        const phoneInput = document.getElementById('customerPhone');
        phoneInput.addEventListener('input', updateCompletePaymentButton);
    }
    
    updateCompletePaymentButton();
}

// Set quick amount for cash payment
function setQuickAmount(amount) {
    const cashInput = document.getElementById('cashReceived');
    
    if (amount === 'exact') {
        cashInput.value = currentTotal.toFixed(2);
    } else {
        cashInput.value = amount;
    }
    
    calculateChange();
    updateCompletePaymentButton();
}

// Calculate change for cash payment
function calculateChange() {
    const cashReceived = parseFloat(document.getElementById('cashReceived').value) || 0;
    const changeDisplay = document.getElementById('changeDisplay');
    const changeAmount = document.getElementById('changeAmount');
    const changeLabel = document.getElementById('changeLabel');
    
    if (cashReceived > 0) {
        const change = cashReceived - currentTotal;
        
        if (change >= 0) {
            changeLabel.textContent = 'Change:';
            changeAmount.textContent = `KSh ${change.toLocaleString('en-KE', { minimumFractionDigits: 2 })}`;
            changeAmount.style.color = '#10b981';
        } else {
            changeLabel.textContent = 'Insufficient:';
            changeAmount.textContent = `KSh ${Math.abs(change).toLocaleString('en-KE', { minimumFractionDigits: 2 })}`;
            changeAmount.style.color = '#dc2626';
        }
        
        changeDisplay.style.display = 'block';
    } else {
        changeDisplay.style.display = 'none';
    }
    
    updateCompletePaymentButton();
}

// Validate M-Pesa form
function validateMpesaForm() {
    const phoneNumber = document.getElementById('customerPhone').value;
    const isValid = phoneNumber.length === 9 && /^[0-9]+$/.test(phoneNumber);
    return isValid;
}

// Update complete payment button state
function updateCompletePaymentButton() {
    const completeBtn = document.getElementById('completePaymentBtn');
    if (!completeBtn) return;
    
    let isValid = false;
    
    if (selectedPaymentMethod === 'cash') {
        const cashReceived = parseFloat(document.getElementById('cashReceived').value) || 0;
        isValid = cashReceived >= currentTotal;
    } else if (selectedPaymentMethod === 'mpesa') {
        const phoneNumber = document.getElementById('customerPhone').value;
        isValid = validatePhoneNumber(phoneNumber);
    }
    
    completeBtn.disabled = !isValid;
}

// Complete payment
async function completePayment() {
    if (!selectedPaymentMethod) {
        showToast('Please select a payment method', 'error');
        return;
    }
    
    if (cart.length === 0) {
        showToast('Cart is empty', 'error');
        return;
    }
    
    const completeBtn = document.getElementById('completePaymentBtn');
    completeBtn.disabled = true;
    completeBtn.innerHTML = '<i class="fas fa-spinner fa-spin"></i> Processing...';
    
    try {
        // Prepare sale data
        const saleData = {
            items: cart.map(item => ({
                productId: item.id,
                quantity: item.quantity,
                unitPrice: item.price,
                total: item.total
            })),
            paymentMethod: selectedPaymentMethod,
            totalAmount: currentTotal,
            customerPhone: selectedPaymentMethod === 'mpesa' ? formatPhoneNumberForAPI(document.getElementById('customerPhone').value) : null,
            cashReceived: selectedPaymentMethod === 'cash' ? parseFloat(document.getElementById('cashReceived').value) : null
        };
        
        console.log('💳 Processing sale:', saleData);
        
        // Get CSRF token
        const token = document.querySelector('input[name="__RequestVerificationToken"]').value;
        
        // Determine the correct endpoint based on current page
        const currentPath = window.location.pathname;
        const endpoint = currentPath.includes('/Admin/') ? '/Sales/ProcessSale' : '/Employee/ProcessSale';
        console.log('🔗 Using sales endpoint:', endpoint);
        
        const response = await fetch(endpoint, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': token
            },
            body: JSON.stringify(saleData)
        });
        
        const result = await response.json();
        console.log('💳 Sale processing result:', result);
        
        if (result.success) {
            if (selectedPaymentMethod === 'mpesa' && result.waitingForCallback) {
                // Show status messages for M-Pesa flow
                showToast(result.message || 'STK Push sent successfully!', 'success');
                
                if (result.statusMessages) {
                    showToast(result.statusMessages.current, 'info');
                    setTimeout(() => {
                        showToast(result.statusMessages.next, 'info');
                    }, 2000);
                }
                
                // Clear cart and close payment modal but don't generate receipt yet
                cart = [];
                updateCartDisplay();
                closePaymentModal();
                
                // Start polling for payment status with enhanced feedback
                pollPaymentStatusWithFeedback(result.saleId, result.totalAmount, result.checkoutRequestId);
                
            } else {
                showToast('Payment processed successfully!', 'success');
                
                // Generate and show receipt for non-M-Pesa payments
                generateReceipt(result.saleId, saleData);
                
                // Clear cart and close payment modal
                cart = [];
                updateCartDisplay();
                closePaymentModal();
                
                // Update stats
                await updateTodayStats();
            }
        } else {
            throw new Error(result.message || 'Payment processing failed');
        }
        
    } catch (error) {
        console.error('💥 Payment processing error:', error);
        showToast(`Payment failed: ${error.message}`, 'error');
    } finally {
        completeBtn.disabled = false;
        completeBtn.innerHTML = 'Complete Payment';
    }
}

// Enhanced poll payment status for M-Pesa payments with real-time feedback
async function pollPaymentStatusWithFeedback(saleId, totalAmount, checkoutRequestId) {
    let attempts = 0;
    const maxAttempts = 30; // Poll for 5 minutes (30 attempts * 10 seconds)
    
    // Status messages for different stages
    const statusMessages = {
        'STK_SENT': 'STK Push sent to your phone',
        'WAITING_PIN': 'Waiting for you to enter your M-Pesa PIN...',
        'PROCESSING': 'Processing your payment...',
        'Pending': 'Payment in progress...',
        'Completed': 'Payment successful!',
        'Failed': 'Payment failed'
    };
    
    let lastStatus = 'STK_SENT';
    let pinReminderShown = false;
    
    const checkStatus = async () => {
        try {
            const response = await fetch(`/Sales/CheckPaymentStatus?saleId=${saleId}`);
            const result = await response.json();
            
            console.log('💳 Payment status check:', result);
            
            if (result.success) {
                // Handle status transitions with appropriate messages
                if (result.status !== lastStatus) {
                    lastStatus = result.status;
                    
                    switch (result.status) {
                        case 'Completed':
                            showToast('🎉 Payment confirmed! Generating receipt...', 'success');
                            
                            // Generate receipt now that payment is confirmed
                            const saleData = { 
                                totalAmount: totalAmount,
                                paymentMethod: 'M-Pesa',
                                mpesaReceiptNumber: result.mpesaReceiptNumber
                            };
                            generateReceipt(saleId, saleData);
                            await updateTodayStats();
                            return;
                            
                        case 'Failed':
                            showToast(`❌ Payment failed: ${result.message}`, 'error');
                            showToast('Please try again or use a different payment method.', 'info');
                            return;
                            
                        case 'Pending':
                            if (!pinReminderShown) {
                                showToast('📱 Please check your phone and enter your M-Pesa PIN', 'info');
                                pinReminderShown = true;
                            }
                            break;
                    }
                }
            } else {
                showToast(`Error checking payment: ${result.message}`, 'error');
                return;
            }
            
            // Continue polling if status is still pending
            attempts++;
            if (attempts < maxAttempts) {
                // Show progress updates
                if (attempts === 5) {
                    showToast('⏳ Still waiting for payment confirmation...', 'info');
                } else if (attempts === 15) {
                    showToast('⏳ Please complete the payment on your phone (2 min elapsed)', 'warning');
                } else if (attempts === 25) {
                    showToast('⏳ Payment taking longer than expected (4 min elapsed)', 'warning');
                }
                
                setTimeout(checkStatus, 10000); // Check every 10 seconds
            } else {
                showToast('⏰ Payment confirmation timeout. Please check your M-Pesa messages or try again.', 'error');
                showToast('If payment was successful, the receipt will be available in transaction history.', 'info');
            }
        } catch (error) {
            console.error('Error checking payment status:', error);
            attempts++;
            if (attempts < maxAttempts) {
                setTimeout(checkStatus, 10000);
            } else {
                showToast('❌ Unable to check payment status. Please verify transaction manually.', 'error');
            }
        }
    };
    
    // Start checking after 3 seconds (give time for STK to reach phone)
    setTimeout(checkStatus, 3000);
}

// Keep the original function for backward compatibility
async function pollPaymentStatus(saleId, totalAmount) {
    return pollPaymentStatusWithFeedback(saleId, totalAmount, null);
}

// Generate receipt
function generateReceipt(saleId, saleData) {
const receiptContent = document.getElementById('receiptContent');
const now = new Date();

// Calculate totals from current cart and saleData
const subtotal = saleData.totalAmount || currentTotal || 0;
const tax = subtotal * 0.16;
const netAmount = subtotal - tax;

const receiptHtml = `
<div class="receipt-header" style="text-align: center; padding: 1.5rem 1rem; background: #f8fafc; border-bottom: 2px solid #e5e7eb;">
<h3 style="margin: 0; font-size: 1.25rem; font-weight: bold; color: #1f2937;">PIXEL SOLUTION COMPANY LTD</h3>
<p style="margin: 0.5rem 0 0.25rem; font-size: 0.9rem; color: #6b7280;">Sales Receipt</p>
<p style="margin: 0.25rem 0; font-size: 0.85rem; color: #374151;">Receipt #: ${saleId}</p>
<p style="margin: 0.25rem 0; font-size: 0.85rem; color: #374151;">${now.toLocaleString('en-KE')}</p>
</div>

<div style="padding: 1.5rem 1rem;">
<div style="margin-bottom: 1.5rem;">
${cart.map(item => `
<div style="display: flex; justify-content: space-between; align-items: center; padding: 0.5rem 0; border-bottom: 1px solid #f1f5f9;">
<div>
<div style="font-weight: 600; color: #1f2937; font-size: 0.9rem;">${item.name}</div>
<div style="font-size: 0.8rem; color: #6b7280;">${item.quantity} x KSh ${(item.price || 0).toFixed(2)}</div>
</div>
<div style="font-weight: 600; color: #1f2937;">KSh ${(item.total || 0).toFixed(2)}</div>
</div>
`).join('')}
</div>

<div style="border-top: 2px solid #e5e7eb; padding-top: 1rem; margin-bottom: 1rem;">
<div style="display: flex; justify-content: space-between; margin-bottom: 0.5rem;">
<span style="color: #6b7280;">Subtotal:</span>
<span style="color: #1f2937; font-weight: 600;">KSh ${netAmount.toFixed(2)}</span>
</div>
<div style="display: flex; justify-content: space-between; margin-bottom: 0.75rem;">
<span style="color: #6b7280;">VAT (16%):</span>
<span style="color: #1f2937; font-weight: 600;">KSh ${tax.toFixed(2)}</span>
</div>
<div style="display: flex; justify-content: space-between; padding-top: 0.75rem; border-top: 2px solid #1f2937; font-size: 1.1rem; font-weight: bold;">
<span style="color: #1f2937;">Total:</span>
<span style="color: #10b981;">KSh ${subtotal.toFixed(2)}</span>
</div>
</div>

<div style="border-top: 1px solid #e5e7eb; padding-top: 1rem; margin-bottom: 1rem;">
<div style="display: flex; justify-content: space-between; margin-bottom: 0.5rem;">
<span style="color: #6b7280;">Payment Method:</span>
<span style="color: #1f2937; font-weight: 600;">${selectedPaymentMethod === 'cash' ? 'Cash' : 'M-Pesa'}</span>
</div>
${selectedPaymentMethod === 'cash' ? `
<div style="display: flex; justify-content: space-between; margin-bottom: 0.5rem;">
<span style="color: #6b7280;">Cash Received:</span>
<span style="color: #1f2937; font-weight: 600;">KSh ${parseFloat(document.getElementById('cashReceived').value || 0).toFixed(2)}</span>
</div>
<div style="display: flex; justify-content: space-between;">
<span style="color: #6b7280;">Change:</span>
<span style="color: #1f2937; font-weight: 600;">KSh ${(parseFloat(document.getElementById('cashReceived').value || 0) - subtotal).toFixed(2)}</span>
</div>
` : ''}
</div>

<div style="text-align: center; padding-top: 1rem; border-top: 1px solid #e5e7eb;">
<p style="margin: 0 0 0.5rem; font-size: 0.9rem; color: #6b7280;">Thank you for your business!</p>
<p style="margin: 0; font-size: 0.85rem; color: #9ca3af;">Served by: Staff</p>
</div>
</div>
`;

receiptContent.innerHTML = receiptHtml;

    // Show receipt modal - prevent duplicate modals
    const receiptModal = document.getElementById('receiptModal');
    if (receiptModal && receiptModal.style.display !== 'flex') {
        receiptModal.style.display = 'flex';
    }
}

// Print receipt
function printReceipt() {
    const receiptContent = document.getElementById('receiptContent').innerHTML;
    const printWindow = window.open('', '_blank');
    
    printWindow.document.write(`
        <!DOCTYPE html>
        <html>
        <head>
            <title>Receipt</title>
            <style>
                body { font-family: 'Courier New', monospace; font-size: 12px; margin: 20px; }
                .receipt-header { text-align: center; margin-bottom: 20px; }
                .receipt-item { display: flex; justify-content: space-between; margin-bottom: 5px; }
                .receipt-total { margin-top: 20px; }
            </style>
        </head>
        <body>
            ${receiptContent}
        </body>
        </html>
    `);
    
    printWindow.document.close();
    printWindow.print();
}

// Download receipt as PDF
async function downloadReceiptPDF() {
    try {
        const receiptContent = document.getElementById('receiptContent').innerHTML;
        
        // Get CSRF token
        const token = document.querySelector('input[name="__RequestVerificationToken"]').value;
        
        // Determine the correct endpoint based on current page
        const currentPath = window.location.pathname;
        const endpoint = currentPath.includes('/Admin/') ? '/Sales/GenerateReceiptPDF' : '/Employee/GenerateReceiptPDF';
        
        const response = await fetch(endpoint, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': token
            },
            body: JSON.stringify({
                receiptHtml: receiptContent,
                fileName: `Receipt_${new Date().toISOString().slice(0, 10)}_${Date.now()}.pdf`
            })
        });
        
        if (response.ok) {
            const blob = await response.blob();
            const url = window.URL.createObjectURL(blob);
            const a = document.createElement('a');
            a.href = url;
            a.download = `Receipt_${new Date().toISOString().slice(0, 10)}_${Date.now()}.pdf`;
            document.body.appendChild(a);
            a.click();
            window.URL.revokeObjectURL(url);
            document.body.removeChild(a);
            
            showToast('Receipt PDF downloaded successfully!', 'success');
        } else {
            throw new Error('Failed to generate PDF');
        }
    } catch (error) {
        console.error('PDF generation error:', error);
        showToast('Failed to download PDF receipt', 'error');
    }
}

// Close receipt modal
function closeReceiptModal() {
    const modal = document.getElementById('receiptModal');
    if (modal) {
        modal.style.display = 'none';
    }
}

// Edit item price
function editItemPrice(productId) {
    console.log('🔧 Edit button clicked for product ID:', productId);
    const item = cart.find(item => item.id === productId);
    if (!item) {
        console.error('❌ Item not found in cart:', productId);
        return;
    }
    
    console.log('📝 Opening price edit dialog for:', item.name, 'Current price:', item.price);
    const newPrice = prompt(`Edit price for ${item.name}\nCurrent price: KSh ${item.price.toFixed(2)}\nEnter new price:`, item.price.toFixed(2));
    
    if (newPrice === null) {
        console.log('❌ User cancelled price edit');
        return; // User cancelled
    }
    
    const parsedPrice = parseFloat(newPrice);
    console.log('💰 New price entered:', newPrice, 'Parsed:', parsedPrice);
    
    if (isNaN(parsedPrice) || parsedPrice < 0) {
        console.error('❌ Invalid price entered:', newPrice);
        showToast('Please enter a valid price', 'error');
        return;
    }
    
    if (parsedPrice === item.price) {
        console.log('ℹ️ Price unchanged');
        showToast('Price unchanged', 'info');
        return;
    }
    
    // Update the item price and total
    const oldPrice = item.price;
    item.price = parsedPrice;
    item.total = item.quantity * parsedPrice;
    
    console.log('✅ Price updated from', oldPrice, 'to', parsedPrice);
    updateCartDisplay();
    
    const priceChange = parsedPrice > oldPrice ? 'increased' : 'decreased';
    showToast(`Price ${priceChange} for ${item.name}`, 'success');
}

// Force add edit buttons to existing cart items (fallback function)
function forceAddEditButtons() {
    console.log('🔧 Force adding edit buttons to cart items');
    const cartItems = document.querySelectorAll('.cart-item-controls');
    cartItems.forEach((controls, index) => {
        // Check if edit button already exists
        if (controls.querySelector('.edit-price-btn')) {
            console.log('✅ Edit button already exists for item', index);
            return;
        }
        
        const quantityControls = controls.querySelector('.quantity-controls');
        const removeBtn = controls.querySelector('.remove-btn');
        
        if (quantityControls && removeBtn && cart[index]) {
            const editBtn = document.createElement('button');
            editBtn.className = 'edit-price-btn';
            editBtn.title = 'Edit Price';
            editBtn.style.cssText = 'width: 28px; height: 28px; border: none; background: #dbeafe; color: #2563eb; border-radius: 6px; display: flex; align-items: center; justify-content: center; cursor: pointer; font-size: 0.75rem; transition: all 0.2s; margin-left: 0.25rem;';
            editBtn.innerHTML = '<i class="fas fa-edit"></i>';
            editBtn.onclick = () => editItemPrice(cart[index].id);
            
            controls.insertBefore(editBtn, removeBtn);
            console.log('✅ Edit button added for item', index);
        }
    });
}

// Phone number formatting functions
function formatPhoneNumber(input) {
    // Remove all non-digit characters
    let value = input.value.replace(/\D/g, '');
    
    // Handle different input formats
    if (value.startsWith('254')) {
        // If starts with 254, remove it and keep the rest
        value = value.substring(3);
    } else if (value.startsWith('0')) {
        // If starts with 0, remove it
        value = value.substring(1);
    }
    
    // Limit to 9 digits maximum
    if (value.length > 9) {
        value = value.substring(0, 9);
    }
    
    // Update the input value
    input.value = value;
    
    // Validate and update payment button
    updateCompletePaymentButton();
}

function formatPhoneNumberForAPI(phoneNumber) {
    if (!phoneNumber) return null;
    
    // Remove all non-digit characters
    let cleanNumber = phoneNumber.replace(/\D/g, '');
    
    // Handle different formats
    if (cleanNumber.startsWith('254')) {
        // Already has country code
        return cleanNumber;
    } else if (cleanNumber.startsWith('0')) {
        // Remove leading 0 and add 254
        return '254' + cleanNumber.substring(1);
    } else if (cleanNumber.length === 9 && cleanNumber.startsWith('7')) {
        // 9 digits starting with 7
        return '254' + cleanNumber;
    } else if (cleanNumber.length === 8 && !cleanNumber.startsWith('7')) {
        // 8 digits, add 7 and 254
        return '2547' + cleanNumber;
    } else if (cleanNumber.length === 7) {
        // 7 digits, add 7 and 254
        return '2547' + cleanNumber;
    }
    
    // Default: add 254 prefix
    return '254' + cleanNumber;
}

function validatePhoneNumber(phoneNumber) {
    if (!phoneNumber) return false;
    
    const formatted = formatPhoneNumberForAPI(phoneNumber);
    
    // Valid Kenyan mobile numbers should be 12 digits starting with 254
    return formatted && formatted.length === 12 && formatted.startsWith('254');
}
