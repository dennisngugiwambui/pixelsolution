// Global variables
let cart = [];
let allProducts = [];
let allCategories = [];
let selectedPaymentMethod = null;
let currentTotal = 0;

document.addEventListener('DOMContentLoaded', function() {
    console.log('Sales page loaded');
    loadCategories();
    loadProducts();
    updateCartDisplay();
    setupSearchFunctionality();
    loadTodayStats();
    loadNotifications();
    setupKeyboardShortcuts();
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
            
            // Debug: Log product IDs to verify they're correct
            console.log('🔍 Product IDs loaded:', allProducts.map(p => ({ id: p.id, name: p.name })));
            
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

            ${product.imageUrl ?
                `<img src="${product.imageUrl}" alt="${product.name}" class="product-image" onerror="this.style.display='none'; this.nextElementSibling.style.display='flex';">
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
        cartItemsContainer.innerHTML = cart.map(item => `
            <div class="cart-item">
                ${item.imageUrl ? 
                    `<img src="${item.imageUrl}" alt="${item.name}" class="cart-item-image" onerror="this.style.display='none'; this.nextElementSibling.style.display='flex';">
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
                    <button class="remove-btn" onclick="removeFromCart(${item.id})">
                        <i class="fas fa-trash"></i>
                    </button>
                </div>
            </div>
        `).join('');
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
}
