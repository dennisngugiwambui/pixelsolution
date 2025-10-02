// Global variables
let cart = [];
let allProducts = [];
let allCategories = [];
let selectedPaymentMethod = null;
let currentTotal = 0;
let currentSaleId = null; // Store current sale ID for manual verification

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
        
        // Determine the correct endpoint based on current page (case-insensitive)
        const currentPath = window.location.pathname.toLowerCase();
        const endpoint = currentPath.includes('/admin/') ? '/Admin/GetCategories' : '/Employee/GetCategories';
        console.log('🔗 Using categories endpoint:', endpoint, '(from path:', window.location.pathname, ')');
        
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

        // Determine the correct endpoint based on current page (case-insensitive)
        const currentPath = window.location.pathname.toLowerCase();
        const endpoint = currentPath.includes('/admin/') ? '/Admin/GetProductsForSale' : '/Employee/GetProductsForSale';
        console.log('🔗 Using endpoint:', endpoint, '(from path:', window.location.pathname, ')');

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
        // Determine the correct endpoint based on current page (case-insensitive)
        const currentPath = window.location.pathname.toLowerCase();
        const endpoint = currentPath.includes('/admin/') ? '/Admin/GetTodaysSalesStats' : '/Employee/GetTodaysSalesStats';
        console.log('🔗 Using stats endpoint:', endpoint, '(from path:', window.location.pathname, ')');
        
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
        // Determine the correct endpoint based on current page (case-insensitive)
        const currentPath = window.location.pathname.toLowerCase();
        const endpoint = currentPath.includes('/admin/') ? '/Admin/GetNotifications' : '/Employee/GetNotifications';
        console.log('🔔 Using notifications endpoint:', endpoint, '(from path:', window.location.pathname, ')');
        
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
    
    // Hide payment forms - check if elements exist first
    const cashForm = document.getElementById('cashPaymentForm');
    const stkForm = document.getElementById('stkPaymentForm');
    const qrForm = document.getElementById('qrPaymentForm');
    const manualForm = document.getElementById('manualPaymentForm');
    const methodSelection = document.getElementById('paymentMethodSelection');
    const backBtn = document.getElementById('backToOptionsBtn');
    
    if (cashForm) cashForm.style.display = 'none';
    if (stkForm) stkForm.style.display = 'none';
    if (qrForm) qrForm.style.display = 'none';
    if (manualForm) manualForm.style.display = 'none';
    if (methodSelection) methodSelection.style.display = 'block';
    if (backBtn) backBtn.style.display = 'none';
    
    // Reset form inputs - check if elements exist
    const cashInput = document.getElementById('cashReceived');
    const phoneInput = document.getElementById('customerPhone');
    const qrPhoneInput = document.getElementById('qrCustomerPhone');
    const manualCodeInput = document.getElementById('manualMpesaCode');
    const changeDisplay = document.getElementById('changeDisplay');
    const completeBtn = document.getElementById('completePaymentBtn');
    
    if (cashInput) cashInput.value = '';
    if (phoneInput) phoneInput.value = '';
    if (qrPhoneInput) qrPhoneInput.value = '';
    if (manualCodeInput) manualCodeInput.value = '';
    if (changeDisplay) changeDisplay.style.display = 'none';
    if (completeBtn) completeBtn.disabled = true;
}

// Select payment method
function selectPaymentMethod(method) {
    console.log(`💳 Payment method selected: ${method}`);
    selectedPaymentMethod = method;
    
    // Update UI to show selected method
    const paymentOptions = document.querySelectorAll('.payment-option');
    paymentOptions.forEach(option => option.classList.remove('selected'));
    
    // Hide method selection and all forms
    document.getElementById('paymentMethodSelection').style.display = 'none';
    document.getElementById('cashPaymentForm').style.display = 'none';
    document.getElementById('stkPaymentForm').style.display = 'none';
    document.getElementById('qrPaymentForm').style.display = 'none';
    document.getElementById('manualPaymentForm').style.display = 'none';
    
    // Show back button
    document.getElementById('backToOptionsBtn').style.display = 'flex';
    
    // Show appropriate form
    if (method === 'cash') {
        document.getElementById('cashPaymentForm').style.display = 'block';
        setTimeout(() => document.getElementById('cashReceived').focus(), 100);
        const cashInput = document.getElementById('cashReceived');
        cashInput.addEventListener('input', calculateChange);
        
    } else if (method === 'stk') {
        document.getElementById('stkPaymentForm').style.display = 'block';
        setTimeout(() => document.getElementById('customerPhone').focus(), 100);
        const phoneInput = document.getElementById('customerPhone');
        phoneInput.addEventListener('input', updateCompletePaymentButton);
        
    } else if (method === 'qr') {
        document.getElementById('qrPaymentForm').style.display = 'block';
        setTimeout(() => document.getElementById('qrCustomerPhone').focus(), 100);
        const phoneInput = document.getElementById('qrCustomerPhone');
        phoneInput.addEventListener('input', updateCompletePaymentButton);
        
    } else if (method === 'manual') {
        document.getElementById('manualPaymentForm').style.display = 'block';
        setTimeout(() => document.getElementById('manualMpesaCode').focus(), 100);
        const codeInput = document.getElementById('manualMpesaCode');
        codeInput.addEventListener('input', updateCompletePaymentButton);
    }
    
    updateCompletePaymentButton();
}

// Back to payment options
function backToPaymentOptions() {
    // Hide all forms
    document.getElementById('cashPaymentForm').style.display = 'none';
    document.getElementById('stkPaymentForm').style.display = 'none';
    document.getElementById('qrPaymentForm').style.display = 'none';
    document.getElementById('manualPaymentForm').style.display = 'none';
    
    // Show payment method selection
    document.getElementById('paymentMethodSelection').style.display = 'block';
    
    // Hide back button
    document.getElementById('backToOptionsBtn').style.display = 'none';
    
    // Reset selected method
    selectedPaymentMethod = null;
    
    // Reset button
    const completeBtn = document.getElementById('completePaymentBtn');
    completeBtn.disabled = true;
    completeBtn.innerHTML = 'Complete Payment';
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
    const isValid = phoneNumber.length >= 7 && phoneNumber.length <= 9 && /^[0-9]+$/.test(phoneNumber);
    
    return isValid;
}

// This function is no longer used - QR code is generated via showQRCodeModal button

// Update complete payment button state
function updateCompletePaymentButton() {
    const completeBtn = document.getElementById('completePaymentBtn');
    if (!completeBtn) return;
    
    let isValid = false;
    let buttonText = 'Complete Payment';
    
    if (selectedPaymentMethod === 'cash') {
        const cashReceived = parseFloat(document.getElementById('cashReceived').value) || 0;
        isValid = cashReceived >= currentTotal;
        buttonText = 'Complete Cash Payment';
    } else if (selectedPaymentMethod === 'stk') {
        const phoneNumber = document.getElementById('customerPhone').value;
        isValid = validatePhoneNumber(phoneNumber);
        buttonText = 'Send STK Push';
    } else if (selectedPaymentMethod === 'qr') {
        const phoneNumber = document.getElementById('qrCustomerPhone').value;
        isValid = validatePhoneNumber(phoneNumber);
        buttonText = 'Generate QR Code';
    } else if (selectedPaymentMethod === 'manual') {
        const code = document.getElementById('manualMpesaCode').value.trim();
        isValid = code.length >= 5;
        buttonText = 'Verify & Complete';
    }
    
    completeBtn.disabled = !isValid;
    completeBtn.innerHTML = buttonText;
}

// Validate and complete payment (for Enter key support)
function validateAndCompletePayment() {
    console.log('Validating payment for method:', selectedPaymentMethod);
    
    if (!selectedPaymentMethod) {
        showToast('⚠️ Please select a payment method', 'warning');
        return;
    }
    
    let isValid = false;
    
    if (selectedPaymentMethod === 'cash') {
        const cashReceived = parseFloat(document.getElementById('cashReceived').value) || 0;
        isValid = cashReceived >= currentTotal;
        if (!isValid) {
            showToast('⚠️ Amount received must be at least KSh ' + currentTotal.toFixed(2), 'warning');
            return;
        }
    } else if (selectedPaymentMethod === 'stk') {
        const phoneNumber = document.getElementById('customerPhone').value;
        isValid = phoneNumber && phoneNumber.length === 9;
        if (!isValid) {
            showToast('⚠️ Please enter a valid 9-digit phone number', 'warning');
            return;
        }
    }
    
    if (isValid) {
        completePayment();
    }
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
    
    // MANUAL VERIFICATION - Don't create sale, verify code first
    if (selectedPaymentMethod === 'manual') {
        await verifyManualMpesaCode();
        return;
    }
    
    // QR CODE - Don't create sale, wait for payment
    if (selectedPaymentMethod === 'qr') {
        showToast('Please wait for customer to scan QR and pay, then use Manual Verify to complete', 'info');
        return;
    }
    
    const completeBtn = document.getElementById('completePaymentBtn');
    completeBtn.disabled = true;
    completeBtn.innerHTML = '<i class="fas fa-spinner fa-spin"></i> Processing...';
    
    try {
        // Only STK and Cash create sales immediately
        // All M-Pesa methods (stk, qr, manual) should be labeled as "M-Pesa"
        const paymentMethodFormatted = selectedPaymentMethod === 'cash' ? 'Cash' : 'M-Pesa';
        
        const rawPhone = selectedPaymentMethod === 'stk' ? document.getElementById('customerPhone').value : '';
        const formattedPhone = selectedPaymentMethod === 'stk' ? formatPhoneNumberForAPI(rawPhone) : null;
        
        console.log('📱 Phone number debug:', {
            raw: rawPhone,
            formatted: formattedPhone,
            length: formattedPhone?.length
        });
        
        // Get cash received value for cash payments
        const cashReceivedValue = selectedPaymentMethod === 'cash' 
            ? parseFloat(document.getElementById('cashReceived').value) 
            : null;
        
        const saleData = {
            Items: cart.map(item => ({
                productId: item.id,
                quantity: item.quantity,
                unitPrice: item.price,
                totalPrice: item.total  // Backend expects TotalPrice not total
            })),
            paymentMethod: paymentMethodFormatted,
            totalAmount: currentTotal,
            customerPhone: formattedPhone,
            cashReceived: cashReceivedValue
        };
        
        console.log('💰 Cash payment data:', {
            method: selectedPaymentMethod,
            total: currentTotal,
            cashReceived: cashReceivedValue,
            change: cashReceivedValue ? (cashReceivedValue - currentTotal) : 0
        });
        
        console.log('💳 Processing sale:', saleData);
        
        // Get CSRF token
        const token = document.querySelector('input[name="__RequestVerificationToken"]').value;
        
        // Determine the correct endpoint based on current page (case-insensitive)
        const currentPath = window.location.pathname.toLowerCase();
        const endpoint = currentPath.includes('/admin/') ? '/Sales/ProcessSale' : '/Employee/ProcessSale';
        console.log('🔗 Using sales endpoint:', endpoint, '(from path:', window.location.pathname, ')');
        
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
            // Store sale ID for manual verification
            currentSaleId = result.saleId;
            
            // STK Push - wait for callback, don't print receipt yet
            if (selectedPaymentMethod === 'stk' && result.waitingForCallback) {
                // Show status messages for M-Pesa flow
                showToast(result.message || 'STK Push sent successfully!', 'success');
                
                if (result.statusMessages) {
                    showToast(result.statusMessages.current, 'info');
                    setTimeout(() => {
                        showToast(result.statusMessages.next, 'info');
                    }, 2000);
                }
                
                // Clear cart and close payment modal but DON'T generate receipt yet
                cart = [];
                updateCartDisplay();
                closePaymentModal();
                
                // Start polling for payment status with enhanced feedback
                pollPaymentStatusWithFeedback(result.saleId, result.totalAmount, result.checkoutRequestId);
                
            } else if (selectedPaymentMethod === 'cash') {
                // Cash - immediate receipt
                showToast('Payment processed successfully!', 'success');
                
                // Generate and show receipt for cash payments
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

// Show payment processing modal with spinner
function showPaymentProcessingModal(message = 'Processing payment...') {
    const modal = document.createElement('div');
    modal.id = 'paymentProcessingModal';
    modal.style.cssText = `
        position: fixed;
        top: 0;
        left: 0;
        width: 100%;
        height: 100%;
        background: rgba(0, 0, 0, 0.7);
        display: flex;
        align-items: center;
        justify-content: center;
        z-index: 10000;
    `;
    
    modal.innerHTML = `
        <div style="background: white; padding: 2rem; border-radius: 12px; text-align: center; min-width: 300px;">
            <div class="spinner" style="margin: 0 auto 1rem; width: 50px; height: 50px; border: 4px solid #f3f3f3; border-top: 4px solid #10b981; border-radius: 50%; animation: spin 1s linear infinite;"></div>
            <h3 style="margin: 0 0 0.5rem 0; color: #374151;">${message}</h3>
            <p id="paymentStatusText" style="margin: 0; color: #6b7280; font-size: 0.875rem;">Waiting for payment confirmation...</p>
        </div>
    `;
    
    document.body.appendChild(modal);
    
    // Add spinner animation
    if (!document.getElementById('spinnerStyle')) {
        const style = document.createElement('style');
        style.id = 'spinnerStyle';
        style.textContent = '@keyframes spin { 0% { transform: rotate(0deg); } 100% { transform: rotate(360deg); } }';
        document.head.appendChild(style);
    }
}

// Update payment processing modal message
function updatePaymentProcessingMessage(message) {
    const statusText = document.getElementById('paymentStatusText');
    if (statusText) {
        statusText.textContent = message;
    }
}

// Close payment processing modal
function closePaymentProcessingModal() {
    const modal = document.getElementById('paymentProcessingModal');
    if (modal) {
        modal.remove();
    }
}

// Enhanced payment modal with visual states
function showEnhancedPaymentModal(state, options) {
    // Remove existing modal if any
    const existingModal = document.getElementById('paymentProcessingModal');
    if (existingModal) {
        existingModal.remove();
    }
    
    const modal = document.createElement('div');
    modal.id = 'paymentProcessingModal';
    modal.style.cssText = `
        position: fixed;
        top: 0;
        left: 0;
        right: 0;
        bottom: 0;
        width: 100vw;
        height: 100vh;
        background: rgba(0, 0, 0, 0.8);
        display: flex;
        align-items: center;
        justify-content: center;
        z-index: 99999;
        animation: fadeIn 0.3s ease-in;
        margin: 0;
        padding: 0;
    `;
    
    // State-specific styling
    const stateColors = {
        'STK_SENT': '#3b82f6',
        'WAITING_PIN': '#3b82f6',
        'PROCESSING': '#f59e0b',
        'SUCCESS': '#10b981',
        'FAILED': '#ef4444',
        'WAITING': '#6b7280',
        'WAITING_LONG': '#f59e0b',
        'TIMEOUT_WARNING': '#f97316',
        'TIMEOUT': '#ef4444'
    };
    
    const color = options.color || stateColors[state] || '#3b82f6';
    const icon = options.icon || 'fa-spinner fa-spin';
    
    modal.innerHTML = `
        <div style="background: white; padding: 3rem 2.5rem; border-radius: 20px; text-align: center; width: 90%; max-width: 480px; box-shadow: 0 25px 50px -12px rgba(0, 0, 0, 0.5); animation: slideUp 0.3s ease-out; margin: auto;">
            <div style="margin: 0 auto 1.75rem; width: 100px; height: 100px; background: ${color}15; border-radius: 50%; display: flex; align-items: center; justify-content: center; position: relative;">
                <div style="position: absolute; width: 100%; height: 100%; border-radius: 50%; border: 3px solid ${color}30; animation: pulse 2s ease-in-out infinite;"></div>
                <i class="fas ${icon}" style="font-size: 3rem; color: ${color}; z-index: 1;"></i>
            </div>
            <h3 style="margin: 0 0 1rem 0; color: #1f2937; font-size: 1.75rem; font-weight: 700; letter-spacing: -0.025em;">${options.title}</h3>
            <p style="margin: 0 0 0.75rem 0; color: #374151; font-size: 1.1rem; font-weight: 500; line-height: 1.5;">${options.message}</p>
            <p id="paymentStatusText" style="margin: 0; color: #6b7280; font-size: 0.95rem; line-height: 1.6;">${options.detail || ''}</p>
            ${options.saleId ? `<p style="margin-top: 1.5rem; padding-top: 1.5rem; border-top: 1px solid #e5e7eb; color: #9ca3af; font-size: 0.8rem; font-weight: 500;">Sale ID: ${options.saleId}</p>` : ''}
        </div>
    `;
    
    document.body.appendChild(modal);
    
    // Add animations if not already present
    if (!document.getElementById('paymentModalAnimations')) {
        const style = document.createElement('style');
        style.id = 'paymentModalAnimations';
        style.textContent = `
            @keyframes fadeIn {
                from { opacity: 0; }
                to { opacity: 1; }
            }
            @keyframes slideUp {
                from { 
                    transform: translateY(30px) scale(0.95); 
                    opacity: 0; 
                }
                to { 
                    transform: translateY(0) scale(1); 
                    opacity: 1; 
                }
            }
            @keyframes spin {
                0% { transform: rotate(0deg); }
                100% { transform: rotate(360deg); }
            }
            @keyframes pulse {
                0%, 100% { 
                    transform: scale(1); 
                    opacity: 0.3; 
                }
                50% { 
                    transform: scale(1.1); 
                    opacity: 0.6; 
                }
            }
        `;
        document.head.appendChild(style);
    }
}

// Enhanced poll payment status for M-Pesa payments with real-time feedback
async function pollPaymentStatusWithFeedback(saleId, totalAmount, checkoutRequestId) {
    let attempts = 0;
    const maxAttempts = 120; // Poll for 10 minutes (120 attempts with variable intervals)
    let cancelled = false;
    
    console.log('🔄 Starting M-Pesa payment polling for Sale ID:', saleId);
    
    // Show enhanced processing modal with visual feedback
    showEnhancedPaymentModal('STK_SENT', {
        title: '📱 M-Pesa Payment',
        message: 'STK Push sent to your phone',
        detail: 'Please check your phone and enter your M-Pesa PIN',
        saleId: saleId,
        checkoutRequestId: checkoutRequestId
    });
    
    // Add cancel button
    const modalContent = document.querySelector('#paymentProcessingModal .modal-content');
    if (modalContent && !document.getElementById('cancelPaymentBtn')) {
        const cancelBtn = document.createElement('button');
        cancelBtn.id = 'cancelPaymentBtn';
        cancelBtn.className = 'btn btn-secondary';
        cancelBtn.style.cssText = 'margin-top: 1rem; width: 100%;';
        cancelBtn.innerHTML = '<i class="fas fa-times"></i> Cancel Waiting';
        cancelBtn.onclick = () => {
            cancelled = true;
            closePaymentProcessingModal();
            showToast('⚠️ Stopped waiting for payment. Check M-Pesa messages to verify.', 'info');
        };
        modalContent.appendChild(cancelBtn);
    }
    
    let lastStatus = 'STK_SENT';
    let pinReminderShown = false;
    let processingShown = false;
    
    const checkStatus = async () => {
        // Check if cancelled
        if (cancelled) {
            return;
        }
        
        try {
            // Determine the correct endpoint based on current page (case-insensitive)
            const currentPath = window.location.pathname.toLowerCase();
            const endpoint = currentPath.includes('/admin/') ? '/Sales/CheckPaymentStatus' : '/Employee/CheckPaymentStatus';
            
            const response = await fetch(`${endpoint}?saleId=${saleId}`);
            const result = await response.json();
            
            console.log('💳 Payment status check:', result);
            
            if (result.success) {
                console.log(`💳 Payment status: ${result.status} (Attempt ${attempts}/${maxAttempts})`);
                
                // Handle status transitions with appropriate messages
                const statusChanged = result.status !== lastStatus;
                if (statusChanged) {
                    lastStatus = result.status;
                    console.log(`🔄 Status changed to: ${result.status}`);
                }
                
                // Process status (check on every poll, not just on change)
                switch (result.status) {
                        case 'Completed':
                            showEnhancedPaymentModal('SUCCESS', {
                                title: '✅ Payment Successful!',
                                message: 'Your M-Pesa payment has been confirmed',
                                detail: result.mpesaReceiptNumber ? `Receipt: ${result.mpesaReceiptNumber}` : 'Generating receipt...',
                                icon: 'fa-check-circle',
                                color: '#10b981'
                            });
                            
                            setTimeout(() => {
                                closePaymentProcessingModal();
                                showToast('🎉 Payment confirmed! Generating receipt...', 'success');
                                
                                // Generate receipt now that payment is confirmed
                                const saleData = { 
                                    totalAmount: totalAmount,
                                    paymentMethod: 'M-Pesa',
                                    mpesaReceiptNumber: result.mpesaReceiptNumber
                                };
                                generateReceipt(saleId, saleData);
                                updateTodayStats();
                            }, 2000);
                            return;
                            
                        case 'Failed':
                            // Check if it's actually failed or still processing
                            const errorMsg = result.message || '';
                            if (errorMsg.includes('still under processing') || errorMsg.includes('still processing')) {
                                // Not actually failed, still processing
                                console.log('⏳ Transaction still processing, continuing to poll...');
                                if (!processingShown) {
                                    showEnhancedPaymentModal('PROCESSING', {
                                        title: '⏳ Processing Payment',
                                        message: 'Your payment is being processed',
                                        detail: 'Please wait while we confirm your payment...',
                                        icon: 'fa-spinner fa-spin',
                                        color: '#f59e0b'
                                    });
                                    processingShown = true;
                                }
                                break; // Continue polling
                            }
                            
                            // Actually failed
                            showEnhancedPaymentModal('FAILED', {
                                title: '❌ Payment Failed',
                                message: result.message || 'Payment could not be completed',
                                detail: 'Please try again or use a different payment method',
                                icon: 'fa-times-circle',
                                color: '#ef4444'
                            });
                            
                            setTimeout(() => {
                                closePaymentProcessingModal();
                                showToast(`❌ Payment failed: ${result.message}`, 'error');
                            }, 2000);
                            return;
                            
                        case 'Pending':
                        case 'STK_SENT':
                            // Show "Waiting for PIN" after first check
                            if (attempts >= 1 && !pinReminderShown) {
                                showEnhancedPaymentModal('WAITING_PIN', {
                                    title: '📱 Waiting for PIN',
                                    message: 'Please enter your M-Pesa PIN',
                                    detail: 'Check your phone for the STK push notification',
                                    icon: 'fa-mobile-alt',
                                    color: '#3b82f6'
                                });
                                pinReminderShown = true;
                            } else if (!processingShown && attempts > 5) {
                                // After 15 seconds (5 attempts * 3s), show processing
                                showEnhancedPaymentModal('PROCESSING', {
                                    title: '⏳ Processing Payment',
                                    message: 'Your payment is being processed',
                                    detail: 'This may take a few moments...',
                                    icon: 'fa-spinner fa-spin',
                                    color: '#f59e0b'
                                });
                                processingShown = true;
                            }
                            break;
                }
            } else {
                console.error('❌ Payment status check failed:', result.message);
                closePaymentProcessingModal();
                showToast(`Error checking payment: ${result.message}`, 'error');
                return;
            }
            
            // Continue polling if status is still pending
            attempts++;
            if (attempts < maxAttempts) {
                // Dynamic polling interval - faster at start, slower later
                let pollInterval;
                if (attempts < 10) {
                    pollInterval = 3000; // First 30 seconds: check every 3 seconds
                } else if (attempts < 30) {
                    pollInterval = 5000; // Next 1.5 minutes: check every 5 seconds
                } else {
                    pollInterval = 10000; // After that: check every 10 seconds
                }
                
                // Calculate actual time elapsed based on variable intervals
                let timeElapsed = 0;
                if (attempts <= 10) {
                    timeElapsed = Math.floor((attempts * 3) / 60);
                } else if (attempts <= 30) {
                    timeElapsed = Math.floor((30 + (attempts - 10) * 5) / 60);
                } else {
                    timeElapsed = Math.floor((30 + 100 + (attempts - 30) * 10) / 60);
                }
                
                // Show progress updates in modal
                if (attempts === 10) {
                    showEnhancedPaymentModal('WAITING', {
                        title: '⏳ Still Waiting',
                        message: 'Waiting for payment confirmation',
                        detail: `Time elapsed: ${timeElapsed} minute(s)`,
                        icon: 'fa-clock',
                        color: '#6b7280'
                    });
                } else if (attempts === 30) {
                    showEnhancedPaymentModal('WAITING_LONG', {
                        title: '⏰ Please Complete Payment',
                        message: 'Still waiting for confirmation',
                        detail: `Time elapsed: ${timeElapsed} minutes. Please check your phone.`,
                        icon: 'fa-exclamation-triangle',
                        color: '#f59e0b'
                    });
                } else if (attempts === 60) {
                    showEnhancedPaymentModal('TIMEOUT_WARNING', {
                        title: '⚠️ Taking Longer Than Expected',
                        message: 'Payment confirmation delayed',
                        detail: `Time elapsed: ${timeElapsed} minutes. Verify your M-Pesa messages.`,
                        icon: 'fa-exclamation-circle',
                        color: '#f97316'
                    });
                }
                
                setTimeout(checkStatus, pollInterval);
            } else {
                showEnhancedPaymentModal('TIMEOUT', {
                    title: '⏰ Timeout',
                    message: 'Payment confirmation timeout',
                    detail: 'Please check your M-Pesa messages to verify the transaction',
                    icon: 'fa-clock',
                    color: '#ef4444'
                });
                
                setTimeout(() => {
                    closePaymentProcessingModal();
                    showToast('⏰ Payment confirmation timeout. Check M-Pesa messages.', 'error');
                    showToast('If payment succeeded, the receipt is in transaction history.', 'info');
                }, 3000);
            }
        } catch (error) {
            console.error('Error checking payment status:', error);
            attempts++;
            if (attempts < maxAttempts) {
                const pollInterval = attempts < 10 ? 3000 : (attempts < 30 ? 5000 : 10000);
                setTimeout(checkStatus, pollInterval);
            } else {
                closePaymentProcessingModal();
                showToast('❌ Unable to check payment status. Please verify transaction manually.', 'error');
            }
        }
    };
    
    // Start checking immediately (M-Pesa can respond very fast)
    setTimeout(checkStatus, 2000);
}

// Keep the original function for backward compatibility
async function pollPaymentStatus(saleId, totalAmount) {
    return pollPaymentStatusWithFeedback(saleId, totalAmount, null);
}

// Generate receipt
async function generateReceipt(saleId, saleData) {
    const receiptContent = document.getElementById('receiptContent');
    const now = new Date();
    
    // CRITICAL: Fetch complete sale data from backend - DON'T use cart as fallback
    let saleItems = [];
    let subtotal = saleData.totalAmount || currentTotal || 0;
    let amountPaid = saleData.totalAmount || 0;
    let changeGiven = 0;
    let mpesaReceipt = saleData.mpesaReceiptNumber || '';
    let paymentMethod = saleData.paymentMethod || 'Cash';
    
    try {
        // Determine the correct endpoint
        const currentPath = window.location.pathname.toLowerCase();
        const endpoint = currentPath.includes('/admin/') ? '/Sales/GetReceiptData' : '/Employee/GetReceiptData';
        
        console.log('📊 Fetching receipt data from:', endpoint, 'for sale ID:', saleId);
        
        const response = await fetch(`${endpoint}?saleId=${saleId}`);
        console.log('📊 Response status:', response.status);
        
        if (response.ok) {
            const data = await response.json();
            console.log('📊 Receipt data received:', data);
            
            if (data.success && data.items && data.items.length > 0) {
                saleItems = data.items;
                subtotal = data.totalAmount || subtotal;
                amountPaid = data.amountPaid || subtotal;
                changeGiven = data.changeGiven || 0;
                mpesaReceipt = data.mpesaReceiptNumber || mpesaReceipt;
                paymentMethod = data.paymentMethod || paymentMethod;
                console.log('✅ Using backend data - Items:', saleItems.length, 'Total:', subtotal, 'Paid:', amountPaid, 'Change:', changeGiven);
                console.log('📦 First item:', saleItems[0]);
            } else {
                console.warn('⚠️ Backend data invalid or empty');
                // Last resort: use cart if still available
                if (cart && cart.length > 0) {
                    console.log('📦 Using cart as emergency fallback');
                    saleItems = cart;
                }
            }
        } else {
            console.error('❌ Failed to fetch receipt data:', response.status, response.statusText);
        }
    } catch (error) {
        console.error('❌ Error fetching receipt data:', error);
    }
    
    // If we still have no items, show error
    if (saleItems.length === 0) {
        console.error('❌ No sale items available for receipt generation');
        showToast('⚠️ Receipt data incomplete. Please check sale details.', 'warning');
    }
    
    const tax = subtotal * 0.16;
    const netAmount = subtotal - tax;

const receiptHtml = `
<div class="receipt-header" style="text-align: center; padding: 1.5rem 1rem; background: white; border-bottom: 1px solid #e5e7eb;">
<h3 style="margin: 0; font-size: 1.1rem; font-weight: bold; color: #1f2937;">PIXEL SOLUTION COMPANY LTD</h3>
<p style="margin: 0.5rem 0 0.25rem; font-size: 0.85rem; color: #6b7280;">Sales Receipt</p>
<p style="margin: 0.25rem 0; font-size: 0.85rem; color: #374151;">Receipt #: ${saleId}</p>
<p style="margin: 0.25rem 0; font-size: 0.85rem; color: #374151;">${now.toLocaleDateString('en-KE')}, ${now.toLocaleTimeString('en-KE')}</p>
</div>

<div style="padding: 1.5rem 1rem;">
<div style="margin-bottom: 1.5rem;">
${saleItems.map(item => {
    // CRITICAL FIX: Calculate totalPrice if it's 0 but unitPrice exists
    const unitPrice = item.unitPrice || item.price || 0;
    let itemTotal = item.totalPrice || item.total || 0;
    
    // If totalPrice is 0 but we have unitPrice, calculate it
    if (itemTotal === 0 && unitPrice > 0 && item.quantity > 0) {
        itemTotal = unitPrice * item.quantity;
    }
    
    return `
<div style="display: flex; justify-content: space-between; align-items: center; padding: 0.5rem 0; border-bottom: 1px solid #f1f5f9;">
<div>
<div style="font-weight: 600; color: #1f2937; font-size: 0.9rem;">${item.productName || item.name}</div>
<div style="font-size: 0.8rem; color: #6b7280;">${item.quantity} x KSh ${unitPrice.toFixed(2)}</div>
</div>
<div style="font-weight: 600; color: #1f2937;">KSh ${itemTotal.toFixed(2)}</div>
</div>
`;
}).join('')}
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
<span style="color: #1f2937; font-weight: 600;">${paymentMethod}</span>
</div>
${(paymentMethod.toLowerCase().includes('pesa') || mpesaReceipt) ? `
<div style="display: flex; justify-content: space-between; margin-bottom: 0.5rem;">
<span style="color: #6b7280;">Amount Paid:</span>
<span style="color: #1f2937; font-weight: 600;">KSh ${subtotal.toFixed(2)}</span>
</div>
<div style="display: flex; justify-content: space-between; margin-bottom: 0.5rem;">
<span style="color: #6b7280;">Change Given:</span>
<span style="color: #1f2937; font-weight: 600;">KSh 0.00</span>
</div>
` : `
<div style="display: flex; justify-content: space-between; margin-bottom: 0.5rem;">
<span style="color: #6b7280;">Amount Given:</span>
<span style="color: #1f2937; font-weight: 600;">KSh ${amountPaid.toFixed(2)}</span>
</div>
<div style="display: flex; justify-content: space-between; margin-bottom: 0.5rem;">
<span style="color: #6b7280;">Change Given:</span>
<span style="color: #1f2937; font-weight: 600;">KSh ${changeGiven.toFixed(2)}</span>
</div>
`}
${mpesaReceipt ? `
<div style="display: flex; justify-content: space-between; margin-top: 0.5rem; padding: 0.75rem; background: #f0fdf4; border-radius: 0.375rem; border: 1px solid #86efac;">
<span style="color: #166534; font-weight: 600;">M-Pesa Code:</span>
<span style="color: #15803d; font-weight: 700; font-size: 1.1rem;">${mpesaReceipt}</span>
</div>
` : ''}
</div>

<div style="text-align: center; padding-top: 1rem; border-top: 1px solid #e5e7eb;">
<p style="margin: 0 0 0.5rem; font-size: 0.9rem; color: #6b7280;">Thank you for your business!</p>
<p style="margin: 0; font-size: 0.85rem; color: #9ca3af;">Served by: Staff</p>
</div>
</div>
`;

    console.log('📦 Final receipt data - Items:', saleItems.length, 'Subtotal:', subtotal, 'Paid:', amountPaid, 'Change:', changeGiven);
    
    receiptContent.innerHTML = receiptHtml;

    // Show receipt modal - prevent duplicate modals
    const receiptModal = document.getElementById('receiptModal');
    if (receiptModal && receiptModal.style.display !== 'flex') {
        receiptModal.style.display = 'flex';
    }
    
    // Auto-print receipt if thermal printer is connected
    setTimeout(() => {
        printReceipt();
    }, 500);
}

// Print receipt
function printReceipt() {
    const receiptContent = document.getElementById('receiptContent').innerHTML;
    
    // Create a hidden iframe for printing
    let printFrame = document.getElementById('printFrame');
    if (!printFrame) {
        printFrame = document.createElement('iframe');
        printFrame.id = 'printFrame';
        printFrame.style.display = 'none';
        document.body.appendChild(printFrame);
    }
    
    const printDocument = printFrame.contentWindow.document;
    printDocument.open();
    printDocument.write(`
        <!DOCTYPE html>
        <html>
        <head>
            <title>Receipt</title>
            <style>
                @media print {
                    @page { size: 80mm auto; margin: 0; }
                    body { margin: 0; padding: 0; width: 80mm; }
                }
                body { 
                    font-family: 'Courier New', monospace; 
                    font-size: 11px; 
                    margin: 0; 
                    padding: 10px;
                    width: 80mm;
                }
                .receipt-header { text-align: center; margin-bottom: 15px; }
                h3 { margin: 0; font-size: 14px; }
                p { margin: 3px 0; font-size: 11px; }
            </style>
        </head>
        <body>
            ${receiptContent}
        </body>
        </html>
    `);
    
    printDocument.close();
    
    // Wait for content to load then print
    setTimeout(() => {
        try {
            printFrame.contentWindow.focus();
            printFrame.contentWindow.print();
        } catch (error) {
            console.error('Print error:', error);
            showToast('Please connect a thermal printer', 'error');
        }
    }, 250);
}

// Download receipt as PDF
async function downloadReceiptPDF() {
    try {
        const receiptContent = document.getElementById('receiptContent').innerHTML;
        
        // Get CSRF token
        const token = document.querySelector('input[name="__RequestVerificationToken"]').value;
        
        // Determine the correct endpoint based on current page (case-insensitive)
        const currentPath = window.location.pathname.toLowerCase();
        const endpoint = currentPath.includes('/admin/') ? '/Sales/GenerateReceiptPDF' : '/Employee/GenerateReceiptPDF';
        
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

// Auto-generate QR code when 9 digits entered
async function autoGenerateQR(input) {
    const phoneNumber = input.value.trim();
    
    // Only allow digits
    input.value = phoneNumber.replace(/\D/g, '');
    
    if (input.value.length === 9) {
        const qrDisplay = document.getElementById('qrCodeDisplay');
        const qrStatus = document.getElementById('qrPaymentStatus');
        
        // Show loading
        qrDisplay.innerHTML = '<i class="fas fa-spinner fa-spin" style="font-size: 3rem; color: #3b82f6; margin-bottom: 1rem;"></i><p style="color: #6b7280;">Generating QR code...</p>';
        
        try {
            const formattedPhone = formatPhoneNumberForAPI(input.value);
            
            // Generate QR code - NO SALE CREATED
            const qrResponse = await fetch('/api/MpesaTest/generate-qr', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    phoneNumber: formattedPhone,
                    amount: currentTotal
                })
            });
            
            const qrResult = await qrResponse.json();
            
            if (qrResult.success && qrResult.data && qrResult.data.QRCode) {
                qrDisplay.innerHTML = `<img src="data:image/png;base64,${qrResult.data.QRCode}" alt="M-Pesa QR Code" style="width: 100%; max-width: 300px; border-radius: 12px; box-shadow: 0 4px 6px rgba(0,0,0,0.1);">`;
                qrStatus.style.display = 'block';
                document.getElementById('qrStatusText').textContent = 'Waiting for customer to scan and pay...';
                
                // Enable complete button to finalize after payment
                document.getElementById('completePaymentBtn').disabled = false;
                document.getElementById('completePaymentBtn').innerHTML = 'Waiting for Payment...';
            } else {
                qrDisplay.innerHTML = '<i class="fas fa-exclamation-triangle" style="font-size: 3rem; color: #ef4444; margin-bottom: 1rem;"></i><p style="color: #ef4444;">Failed to generate QR code</p>';
                showToast('Failed to generate QR code', 'error');
            }
        } catch (error) {
            console.error('Error generating QR code:', error);
            qrDisplay.innerHTML = '<i class="fas fa-exclamation-triangle" style="font-size: 3rem; color: #ef4444; margin-bottom: 1rem;"></i><p style="color: #ef4444;">Error generating QR code</p>';
            showToast('Error generating QR code', 'error');
        }
    }
}

// Close QR Code Modal
function closeQRCodeModal() {
    document.getElementById('qrCodeModal').style.display = 'none';
    document.getElementById('qrCodeDisplay').innerHTML = '<i class="fas fa-spinner fa-spin" style="font-size: 3rem; color: #0ea5e9;"></i><p style="margin-top: 1rem; color: #6b7280;">Generating QR Code...</p>';
}

// Manual M-Pesa Code Verification
async function verifyManualMpesaCode() {
    const codeInput = document.getElementById('manualMpesaCode');
    const code = codeInput.value.trim().toUpperCase();
    
    if (code.length < 5) {
        showToast('Please enter at least 5 characters of the M-Pesa code', 'error');
        return;
    }
    
    if (cart.length === 0) {
        showToast('Cart is empty. Please add items first.', 'error');
        return;
    }
    
    const completeBtn = document.getElementById('completePaymentBtn');
    completeBtn.disabled = true;
    completeBtn.innerHTML = '<i class="fas fa-spinner fa-spin"></i> Verifying...';
    
    try {
        // STEP 1: VERIFY the M-Pesa code FIRST (don't create sale yet)
        const endpoint = window.location.pathname.includes('/Admin/') ? '/Sales/VerifyManualMpesaCode' : '/Employee/VerifyManualMpesaCode';
        
        const verifyResponse = await fetch(endpoint, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({
                mpesaCode: code,
                saleAmount: currentTotal,
                saleId: 0 // No sale created yet
            })
        });
        
        const result = await verifyResponse.json();
        
        if (result.success) {
            showToast('✅ Payment verified! Creating sale...', 'success');
            
            // STEP 2: NOW create the sale with verified payment
            const saleData = {
                items: cart.map(item => ({
                    productId: item.id,
                    quantity: item.quantity,
                    unitPrice: item.price,
                    total: item.total
                })),
                paymentMethod: 'M-Pesa',
                totalAmount: currentTotal,
                customerPhone: null,
                cashReceived: null,
                mpesaReceiptNumber: result.mpesaReceiptNumber
            };
            
            const saleEndpoint = window.location.pathname.includes('/Admin/') ? '/Sales/ProcessSale' : '/Employee/ProcessSale';
            const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
            
            const saleResponse = await fetch(saleEndpoint, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': token
                },
                body: JSON.stringify(saleData)
            });
            
            const saleResult = await saleResponse.json();
            
            if (saleResult.success) {
                showToast('✅ Sale completed successfully!', 'success');
                
                // Generate receipt
                generateReceipt(saleResult.saleId, saleData);
                
                // Clear cart and close modal
                cart = [];
                currentSaleId = null;
                updateCartDisplay();
                closePaymentModal();
                await updateTodayStats();
                
                // Clear input
                codeInput.value = '';
            } else {
                showToast(`❌ ${saleResult.message}`, 'error');
                completeBtn.disabled = false;
                completeBtn.innerHTML = 'Verify & Complete';
            }
        } else {
            showToast(`❌ ${result.message}`, 'error');
            completeBtn.disabled = false;
            completeBtn.innerHTML = 'Verify & Complete';
        }
    } catch (error) {
        console.error('Error verifying M-Pesa code:', error);
        showToast('Error verifying code. Please try again.', 'error');
        completeBtn.disabled = false;
        completeBtn.innerHTML = 'Verify & Complete';
    }
}
