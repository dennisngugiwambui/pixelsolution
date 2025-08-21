// Dashboard functionality
document.addEventListener('DOMContentLoaded', function () {
    initializeDashboard();
});

function initializeDashboard() {
    setupSidebarToggle();
    setupNavigation();
    // setupCharts(); // Disabled to prevent conflicts with Dashboard.cshtml charts
    setupSearch();
    setupNotifications();
    setupResponsiveHandling();
    // loadDashboardData(); // Disabled to prevent conflicts with Dashboard.cshtml data loading
}

// Sidebar Toggle Functionality
function setupSidebarToggle() {
    const sidebarToggle = document.getElementById('sidebarToggle');
    const sidebar = document.getElementById('sidebar');
    const mainContent = document.getElementById('mainContent');

    if (sidebarToggle && sidebar && mainContent) {
        sidebarToggle.addEventListener('click', toggleSidebar);

        // Create mobile overlay if it doesn't exist
        if (!document.querySelector('.mobile-overlay')) {
            const overlay = document.createElement('div');
            overlay.className = 'mobile-overlay';
            overlay.addEventListener('click', closeSidebar);
            document.body.appendChild(overlay);
        }

        // Close sidebar on mobile when clicking outside
        document.addEventListener('click', (e) => {
            if (window.innerWidth <= 768) {
                if (!sidebar.contains(e.target) && !sidebarToggle.contains(e.target)) {
                    closeSidebar();
                }
            }
        });
    }
}

function toggleSidebar() {
    const sidebar = document.getElementById('sidebar');
    const mainContent = document.getElementById('mainContent');
    const overlay = document.querySelector('.mobile-overlay');

    if (sidebar && mainContent) {
        sidebar.classList.toggle('collapsed');
        mainContent.classList.toggle('expanded');

        // Handle mobile overlay
        if (window.innerWidth <= 768 && overlay) {
            const isCollapsed = sidebar.classList.contains('collapsed');
            if (isCollapsed) {
                overlay.classList.remove('active');
            } else {
                overlay.classList.add('active');
            }
        }

        // Save sidebar state to localStorage (only for desktop)
        if (window.innerWidth > 768) {
            const isCollapsed = sidebar.classList.contains('collapsed');
            localStorage.setItem('sidebarCollapsed', isCollapsed);
        }
    }
}

function closeSidebar() {
    const sidebar = document.getElementById('sidebar');
    const mainContent = document.getElementById('mainContent');
    const overlay = document.querySelector('.mobile-overlay');

    if (sidebar && mainContent) {
        sidebar.classList.add('collapsed');
        mainContent.classList.add('expanded');

        // Hide mobile overlay
        if (overlay) {
            overlay.classList.remove('active');
        }
    }
}

// Navigation Management
function setupNavigation() {
    document.querySelectorAll('.nav-link').forEach(link => {
        link.addEventListener('click', handleNavigation);
    });

    // Restore sidebar state
    const sidebarCollapsed = localStorage.getItem('sidebarCollapsed');
    if (sidebarCollapsed === 'true') {
        const sidebar = document.getElementById('sidebar');
        const mainContent = document.getElementById('mainContent');
        if (sidebar && mainContent) {
            sidebar.classList.add('collapsed');
            mainContent.classList.add('expanded');
        }
    }
}

function handleNavigation(e) {
    e.preventDefault();

    // Remove active class from all links
    document.querySelectorAll('.nav-link').forEach(l => l.classList.remove('active'));

    // Add active class to clicked link
    e.target.closest('.nav-link').classList.add('active');

    // Get the navigation target
    const target = e.target.closest('.nav-link').getAttribute('href');

    // Handle different navigation targets
    if (target && target !== '#') {
        // Show loading state
        showLoadingState();

        // Navigate to the target
        setTimeout(() => {
            window.location.href = target;
        }, 300);
    }
}

// Chart Setup and Management
function setupCharts() {
    initializeSalesChart();
    initializeProductsChart();
}

function initializeSalesChart() {
    const salesCtx = document.getElementById('salesChart');
    if (!salesCtx) return;

    const ctx = salesCtx.getContext('2d');
    window.salesChart = new Chart(ctx, {
        type: 'line',
        data: {
            labels: ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul'],
            datasets: [{
                label: 'Sales',
                data: [30000, 45000, 35000, 50000, 42000, 60000, 55000],
                borderColor: '#3b82f6',
                backgroundColor: 'rgba(59, 130, 246, 0.1)',
                borderWidth: 3,
                fill: true,
                tension: 0.4,
                pointBackgroundColor: '#3b82f6',
                pointBorderColor: '#ffffff',
                pointBorderWidth: 2,
                pointRadius: 6
            }]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            interaction: {
                intersect: false,
                mode: 'index'
            },
            plugins: {
                legend: {
                    display: false
                },
                tooltip: {
                    backgroundColor: 'rgba(0, 0, 0, 0.8)',
                    titleColor: '#ffffff',
                    bodyColor: '#ffffff',
                    borderColor: '#3b82f6',
                    borderWidth: 1,
                    cornerRadius: 8,
                    displayColors: false,
                    callbacks: {
                        title: function (context) {
                            return 'Month: ' + context[0].label;
                        },
                        label: function (context) {
                            return 'Sales: KSh ' + context.parsed.y.toLocaleString();
                        }
                    }
                }
            },
            scales: {
                y: {
                    beginAtZero: true,
                    grid: {
                        color: 'rgba(0, 0, 0, 0.05)',
                        borderDash: [5, 5]
                    },
                    ticks: {
                        callback: function (value) {
                            return 'KSh ' + (value / 1000) + 'k';
                        }
                    }
                },
                x: {
                    grid: {
                        display: false
                    }
                }
            },
            animations: {
                tension: {
                    duration: 1000,
                    easing: 'easeInOutQuart',
                    from: 1,
                    to: 0.4,
                    loop: false
                }
            }
        }
    });
}

function initializeProductsChart() {
    const productsCtx = document.getElementById('productsChart');
    if (!productsCtx) return;

    const ctx = productsCtx.getContext('2d');
    window.productsChart = new Chart(ctx, {
        type: 'doughnut',
        data: {
            labels: ['Electronics', 'Fashion', 'Home & Garden', 'Books', 'Sports'],
            datasets: [{
                data: [78, 62, 51, 29, 15],
                backgroundColor: [
                    '#3b82f6',
                    '#10b981',
                    '#f59e0b',
                    '#ef4444',
                    '#8b5cf6'
                ],
                borderWidth: 0,
                hoverBorderWidth: 3,
                hoverBorderColor: '#ffffff'
            }]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            cutout: '60%',
            plugins: {
                legend: {
                    position: 'bottom',
                    labels: {
                        padding: 20,
                        usePointStyle: true,
                        font: {
                            size: 12
                        }
                    }
                },
                tooltip: {
                    backgroundColor: 'rgba(0, 0, 0, 0.8)',
                    titleColor: '#ffffff',
                    bodyColor: '#ffffff',
                    borderColor: '#3b82f6',
                    borderWidth: 1,
                    cornerRadius: 8,
                    displayColors: true,
                    callbacks: {
                        label: function (context) {
                            const label = context.label || '';
                            const value = context.parsed;
                            const total = context.dataset.data.reduce((a, b) => a + b, 0);
                            const percentage = ((value / total) * 100).toFixed(1);
                            return `${label}: ${value}% (${percentage}%)`;
                        }
                    }
                }
            },
            animation: {
                animateRotate: true,
                duration: 2000
            }
        }
    });
}

// Search Functionality
function setupSearch() {
    const searchInput = document.querySelector('.search-input');
    if (searchInput) {
        searchInput.addEventListener('input', handleSearch);
        searchInput.addEventListener('keypress', function (e) {
            if (e.key === 'Enter') {
                performSearch(this.value);
            }
        });
    }
}

function handleSearch(e) {
    const query = e.target.value.toLowerCase();

    if (query.length > 2) {
        // Show search suggestions
        showSearchSuggestions(query);
    } else {
        hideSearchSuggestions();
    }
}

function showSearchSuggestions(query) {
    // Mock search suggestions
    const suggestions = [
        'Sales Report',
        'Add New Product',
        'Manage Users',
        'View Inventory',
        'Purchase Requests'
    ].filter(item => item.toLowerCase().includes(query));

    // Implementation for showing suggestions dropdown
    console.log('Search suggestions:', suggestions);
}

function hideSearchSuggestions() {
    // Hide search suggestions dropdown
    console.log('Hide search suggestions');
}

function performSearch(query) {
    showLoadingState();
    console.log('Performing search for:', query);
    // Implement actual search functionality
}

// Notification Management
function setupNotifications() {
    document.querySelectorAll('.action-btn').forEach(btn => {
        btn.addEventListener('click', handleNotificationClick);
    });

    // Check for new notifications periodically
    setInterval(checkNotifications, 30000); // Check every 30 seconds
}

function handleNotificationClick(e) {
    const btn = e.currentTarget;
    const icon = btn.querySelector('i');

    if (icon.classList.contains('fa-bell')) {
        showNotificationsPanel();
    } else if (icon.classList.contains('fa-envelope')) {
        showMessagesPanel();
    } else if (icon.classList.contains('fa-user-circle')) {
        showUserMenu();
    }
}

function showNotificationsPanel() {
    // Implementation for notifications panel
    console.log('Show notifications panel');
}

function showMessagesPanel() {
    // Implementation for messages panel
    console.log('Show messages panel');
}

function showUserMenu() {
    // Implementation for user menu
    console.log('Show user menu');
}

function checkNotifications() {
    // Check for new notifications from server
    fetch('/Employee/GetNotifications')
        .then(response => {
            if (!response.ok) {
                throw new Error('Network response was not ok');
            }
            return response.json();
        })
        .then(data => {
            updateNotificationBadges(data);
        })
        .catch(error => {
            // Silently handle notification errors to avoid console spam
            // console.error('Error checking notifications:', error);
        });
}

function updateNotificationBadges(data) {
    const notificationBadge = document.querySelector('.action-btn .fa-bell + .notification-badge');
    const messageBadge = document.querySelector('.action-btn .fa-envelope + .notification-badge');

    if (notificationBadge && data.notifications) {
        notificationBadge.textContent = data.notifications;
        notificationBadge.style.display = data.notifications > 0 ? 'flex' : 'none';
    }

    if (messageBadge && data.messages) {
        messageBadge.textContent = data.messages;
        messageBadge.style.display = data.messages > 0 ? 'flex' : 'none';
    }
}

// Responsive Handling
function setupResponsiveHandling() {
    window.addEventListener('resize', handleWindowResize);
    handleWindowResize(); // Initial check
}

function handleWindowResize() {
    const sidebar = document.getElementById('sidebar');
    const mainContent = document.getElementById('mainContent');

    if (window.innerWidth <= 768) {
        if (sidebar && mainContent) {
            sidebar.classList.add('collapsed');
            mainContent.classList.add('expanded');
        }
    } else {
        // Restore sidebar state on desktop
        const sidebarCollapsed = localStorage.getItem('sidebarCollapsed');
        if (sidebar && mainContent && sidebarCollapsed !== 'true') {
            sidebar.classList.remove('collapsed');
            mainContent.classList.remove('expanded');
        }
    }

    // Resize charts if they exist (Chart.js uses resize() method)
    if (window.salesChart && typeof window.salesChart.resize === 'function') {
        window.salesChart.resize();
    }
    if (window.productsChart && typeof window.productsChart.resize === 'function') {
        window.productsChart.resize();
    }
}

// Dashboard Data Loading
function loadDashboardData() {
    showLoadingState();

    Promise.all([
        fetchDashboardStats(),
        fetchRecentSales(),
        fetchChartData()
    ]).then(([stats, sales, chartData]) => {
        updateDashboardStats(stats);
        updateRecentSales(sales);
        updateCharts(chartData);
        hideLoadingState();
    }).catch(error => {
        console.error('Error loading dashboard data:', error);
        hideLoadingState();
        showError('Failed to load dashboard data');
    });
}

function fetchDashboardStats() {
    return fetch('/api/dashboard/stats')
        .then(response => response.json())
        .catch(() => ({
            totalSales: 55000,
            totalOrders: 500,
            productsSold: 9,
            newCustomers: 12
        }));
}

function fetchRecentSales() {
    return fetch('/api/dashboard/recent-sales')
        .then(response => response.json())
        .catch(() => []);
}

function fetchChartData() {
    return fetch('/api/dashboard/chart-data')
        .then(response => response.json())
        .catch(() => ({}));
}

function updateDashboardStats(stats) {
    const statCards = document.querySelectorAll('.stat-card');

    statCards.forEach((card, index) => {
        const valueElement = card.querySelector('.stat-value');
        if (valueElement) {
            switch (index) {
                case 0:
                    animateNumber(valueElement, stats.totalSales, 'currency');
                    break;
                case 1:
                    animateNumber(valueElement, stats.totalOrders);
                    break;
                case 2:
                    animateNumber(valueElement, stats.productsSold);
                    break;
                case 3:
                    animateNumber(valueElement, stats.newCustomers);
                    break;
            }
        }
    });
}

function updateRecentSales(sales) {
    const tableBody = document.querySelector('.data-table tbody');
    if (tableBody && sales.length > 0) {
        tableBody.innerHTML = sales.map(sale => `
            <tr>
                <td>${sale.saleNumber}</td>
                <td>${sale.customerName}</td>
                <td>${sale.productName}</td>
                <td>KSh ${sale.amount.toLocaleString()}</td>
                <td><span class="status-badge status-${sale.status.toLowerCase()}">${sale.status}</span></td>
                <td>${new Date(sale.date).toLocaleDateString()}</td>
                <td>
                    <button class="action-btn" onclick="viewSale('${sale.id}')">
                        <i class="fas fa-eye"></i>
                    </button>
                    <button class="action-btn" onclick="printSale('${sale.id}')">
                        <i class="fas fa-print"></i>
                    </button>
                </td>
            </tr>
        `).join('');
    }
}

function updateCharts(chartData) {
    if (window.salesChart && chartData.salesData) {
        window.salesChart.data.datasets[0].data = chartData.salesData;
        window.salesChart.update();
    }

    if (window.productsChart && chartData.productData) {
        window.productsChart.data.datasets[0].data = chartData.productData;
        window.productsChart.update();
    }
}

// Utility Functions
function animateNumber(element, targetValue, type = 'number') {
    const startValue = 0;
    const duration = 2000;
    const startTime = performance.now();

    function animate(currentTime) {
        const elapsed = currentTime - startTime;
        const progress = Math.min(elapsed / duration, 1);

        const currentValue = startValue + (targetValue - startValue) * easeOutQuart(progress);

        if (type === 'currency') {
            element.textContent = 'KSh ' + Math.floor(currentValue).toLocaleString();
        } else {
            element.textContent = Math.floor(currentValue).toLocaleString();
        }

        if (progress < 1) {
            requestAnimationFrame(animate);
        }
    }

    requestAnimationFrame(animate);
}

function easeOutQuart(t) {
    return 1 - Math.pow(1 - t, 4);
}

function showLoadingState() {
    const loadingOverlay = document.createElement('div');
    loadingOverlay.id = 'loadingOverlay';
    loadingOverlay.innerHTML = `
        <div style="
            position: fixed;
            top: 0;
            left: 0;
            width: 100%;
            height: 100%;
            background: rgba(255, 255, 255, 0.8);
            display: flex;
            align-items: center;
            justify-content: center;
            z-index: 9999;
        ">
            <div style="
                background: white;
                padding: 2rem;
                border-radius: 12px;
                box-shadow: 0 4px 12px rgba(0, 0, 0, 0.15);
                display: flex;
                align-items: center;
                gap: 1rem;
            ">
                <div style="
                    width: 20px;
                    height: 20px;
                    border: 2px solid #e5e7eb;
                    border-top: 2px solid #3b82f6;
                    border-radius: 50%;
                    animation: spin 1s linear infinite;
                "></div>
                <span>Loading...</span>
            </div>
        </div>
    `;
    document.body.appendChild(loadingOverlay);
}

function hideLoadingState() {
    const loadingOverlay = document.getElementById('loadingOverlay');
    if (loadingOverlay) {
        loadingOverlay.remove();
    }
}

function showError(message) {
    // Implementation for showing error messages
    console.error(message);
}

// Action Functions for Table Buttons
function viewSale(saleId) {
    window.location.href = `/Sales/Details/${saleId}`;
}

function printSale(saleId) {
    window.open(`/Sales/Receipt/${saleId}`, '_blank');
}

// Export functions for external use
window.DashboardUtils = {
    toggleSidebar,
    loadDashboardData,
    updateDashboardStats,
    showLoadingState,
    hideLoadingState,
    viewSale,
    printSale
};