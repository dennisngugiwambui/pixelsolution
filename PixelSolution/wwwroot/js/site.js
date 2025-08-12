// PixelSolution - Global Site JavaScript

// Initialize global namespace
window.PixelSolution = window.PixelSolution || {};

// Global configuration
PixelSolution.Config = {
    apiBaseUrl: '/api',
    defaultTimeout: 30000,
    retryAttempts: 3,
    toastDuration: 5000,
    dateFormat: 'YYYY-MM-DD',
    currencyFormat: 'KSh',
    decimalPlaces: 2
};

// Utility functions
PixelSolution.Utils = {
    // Format currency
    formatCurrency: function (amount, showSymbol = true) {
        const formatted = new Intl.NumberFormat('en-KE', {
            minimumFractionDigits: PixelSolution.Config.decimalPlaces,
            maximumFractionDigits: PixelSolution.Config.decimalPlaces
        }).format(amount);

        return showSymbol ? `${PixelSolution.Config.currencyFormat} ${formatted}` : formatted;
    },

    // Format date
    formatDate: function (date, format = 'short') {
        const d = new Date(date);
        const options = {
            short: { year: 'numeric', month: 'short', day: 'numeric' },
            long: { year: 'numeric', month: 'long', day: 'numeric', weekday: 'long' },
            datetime: {
                year: 'numeric', month: 'short', day: 'numeric',
                hour: '2-digit', minute: '2-digit'
            }
        };

        return new Intl.DateTimeFormat('en-US', options[format] || options.short).format(d);
    },

    // Debounce function
    debounce: function (func, wait, immediate) {
        let timeout;
        return function executedFunction() {
            const context = this;
            const args = arguments;
            const later = function () {
                timeout = null;
                if (!immediate) func.apply(context, args);
            };
            const callNow = immediate && !timeout;
            clearTimeout(timeout);
            timeout = setTimeout(later, wait);
            if (callNow) func.apply(context, args);
        };
    },

    // Throttle function
    throttle: function (func, limit) {
        let inThrottle;
        return function () {
            const args = arguments;
            const context = this;
            if (!inThrottle) {
                func.apply(context, args);
                inThrottle = true;
                setTimeout(() => inThrottle = false, limit);
            }
        };
    },

    // Generate unique ID
    generateId: function (prefix = 'id') {
        return prefix + '_' + Math.random().toString(36).substr(2, 9) + '_' + Date.now();
    },

    // Sanitize HTML
    sanitizeHtml: function (html) {
        const temp = document.createElement('div');
        temp.textContent = html;
        return temp.innerHTML;
    },

    // Get query parameter
    getUrlParameter: function (name) {
        const urlParams = new URLSearchParams(window.location.search);
        return urlParams.get(name);
    },

    // Set query parameter
    setUrlParameter: function (name, value) {
        const url = new URL(window.location);
        url.searchParams.set(name, value);
        window.history.pushState({}, '', url);
    },

    // Validate email
    validateEmail: function (email) {
        const re = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
        return re.test(email);
    },

    // Validate phone
    validatePhone: function (phone) {
        const re = /^[\+]?[0-9\s\-\(\)]{10,}$/;
        return re.test(phone);
    },

    // Copy to clipboard
    copyToClipboard: function (text) {
        if (navigator.clipboard && window.isSecureContext) {
            return navigator.clipboard.writeText(text);
        } else {
            // Fallback for older browsers
            const textArea = document.createElement('textarea');
            textArea.value = text;
            textArea.style.position = 'absolute';
            textArea.style.left = '-999999px';
            document.body.prepend(textArea);
            textArea.select();
            try {
                document.execCommand('copy');
            } catch (error) {
                console.error('Failed to copy text: ', error);
            } finally {
                textArea.remove();
            }
        }
    }
};

// HTTP request utilities
PixelSolution.Http = {
    // Get CSRF token
    getCsrfToken: function () {
        return document.querySelector('input[name="__RequestVerificationToken"]')?.value ||
            document.querySelector('meta[name="csrf-token"]')?.getAttribute('content');
    },

    // Default request options
    getDefaultOptions: function () {
        return {
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': this.getCsrfToken()
            },
            credentials: 'same-origin'
        };
    },

    // GET request
    get: async function (url) {
        try {
            const response = await fetch(url, {
                method: 'GET',
                ...this.getDefaultOptions()
            });
            return await this.handleResponse(response);
        } catch (error) {
            throw this.handleError(error);
        }
    },

    // POST request
    post: async function (url, data) {
        try {
            const response = await fetch(url, {
                method: 'POST',
                body: JSON.stringify(data),
                ...this.getDefaultOptions()
            });
            return await this.handleResponse(response);
        } catch (error) {
            throw this.handleError(error);
        }
    },

    // PUT request
    put: async function (url, data) {
        try {
            const response = await fetch(url, {
                method: 'PUT',
                body: JSON.stringify(data),
                ...this.getDefaultOptions()
            });
            return await this.handleResponse(response);
        } catch (error) {
            throw this.handleError(error);
        }
    },

    // DELETE request
    delete: async function (url) {
        try {
            const response = await fetch(url, {
                method: 'DELETE',
                ...this.getDefaultOptions()
            });
            return await this.handleResponse(response);
        } catch (error) {
            throw this.handleError(error);
        }
    },

    // Handle response
    handleResponse: async function (response) {
        if (!response.ok) {
            const error = new Error(`HTTP error! status: ${response.status}`);
            error.status = response.status;
            error.response = response;
            throw error;
        }

        const contentType = response.headers.get('content-type');
        if (contentType && contentType.includes('application/json')) {
            return await response.json();
        }
        return await response.text();
    },

    // Handle error
    handleError: function (error) {
        console.error('HTTP request error:', error);
        return error;
    }
};

// Toast notification system
PixelSolution.Toast = {
    container: null,

    // Initialize toast container
    init: function () {
        if (!this.container) {
            this.container = document.createElement('div');
            this.container.className = 'toast-container';
            this.container.style.cssText = `
                position: fixed;
                top: 20px;
                right: 20px;
                z-index: 9999;
                max-width: 400px;
            `;
            document.body.appendChild(this.container);
        }
    },

    // Show toast
    show: function (message, type = 'info', duration = PixelSolution.Config.toastDuration) {
        this.init();

        const toast = document.createElement('div');
        toast.className = `toast toast-${type}`;

        const colors = {
            success: '#10b981',
            error: '#ef4444',
            warning: '#f59e0b',
            info: '#3b82f6'
        };

        const icons = {
            success: 'check-circle',
            error: 'exclamation-circle',
            warning: 'exclamation-triangle',
            info: 'info-circle'
        };

        toast.style.cssText = `
            background: ${colors[type] || colors.info};
            color: white;
            padding: 1rem 1.5rem;
            border-radius: 8px;
            box-shadow: 0 4px 12px rgba(0,0,0,0.15);
            margin-bottom: 10px;
            animation: slideInRight 0.3s ease-out;
            cursor: pointer;
            position: relative;
            overflow: hidden;
        `;

        toast.innerHTML = `
            <div style="display: flex; align-items: center; gap: 0.5rem;">
                <i class="fas fa-${icons[type] || icons.info}"></i>
                <span>${PixelSolution.Utils.sanitizeHtml(message)}</span>
                <button onclick="this.closest('.toast').remove()" style="
                    background: none;
                    border: none;
                    color: white;
                    margin-left: auto;
                    cursor: pointer;
                    padding: 0.25rem;
                    border-radius: 4px;
                ">
                    <i class="fas fa-times"></i>
                </button>
            </div>
        `;

        // Auto remove
        setTimeout(() => {
            if (toast.parentNode) {
                toast.style.animation = 'slideOutRight 0.3s ease-out';
                setTimeout(() => toast.remove(), 300);
            }
        }, duration);

        // Remove on click
        toast.addEventListener('click', () => {
            toast.style.animation = 'slideOutRight 0.3s ease-out';
            setTimeout(() => toast.remove(), 300);
        });

        this.container.appendChild(toast);
        return toast;
    },

    success: function (message, duration) {
        return this.show(message, 'success', duration);
    },

    error: function (message, duration) {
        return this.show(message, 'error', duration);
    },

    warning: function (message, duration) {
        return this.show(message, 'warning', duration);
    },

    info: function (message, duration) {
        return this.show(message, 'info', duration);
    }
};

// Modal system
PixelSolution.Modal = {
    // Show confirmation dialog
    confirm: function (message, title = 'Confirm', options = {}) {
        return new Promise((resolve) => {
            const modal = this.create({
                title: title,
                body: message,
                actions: [
                    {
                        text: options.cancelText || 'Cancel',
                        class: 'btn-secondary',
                        action: () => {
                            this.close(modal);
                            resolve(false);
                        }
                    },
                    {
                        text: options.confirmText || 'Confirm',
                        class: options.confirmClass || 'btn-primary',
                        action: () => {
                            this.close(modal);
                            resolve(true);
                        }
                    }
                ]
            });
        });
    },

    // Show alert dialog
    alert: function (message, title = 'Alert', type = 'info') {
        return new Promise((resolve) => {
            const modal = this.create({
                title: title,
                body: message,
                type: type,
                actions: [
                    {
                        text: 'OK',
                        class: 'btn-primary',
                        action: () => {
                            this.close(modal);
                            resolve();
                        }
                    }
                ]
            });
        });
    },

    // Create modal
    create: function (options) {
        const overlay = document.createElement('div');
        overlay.style.cssText = `
            position: fixed;
            top: 0;
            left: 0;
            width: 100%;
            height: 100%;
            background: rgba(0,0,0,0.5);
            z-index: 10000;
            display: flex;
            align-items: center;
            justify-content: center;
            animation: fadeIn 0.3s ease-out;
        `;

        const modal = document.createElement('div');
        modal.style.cssText = `
            background: white;
            border-radius: 8px;
            box-shadow: 0 10px 25px rgba(0,0,0,0.25);
            max-width: 500px;
            width: 90%;
            max-height: 80vh;
            overflow: auto;
            animation: slideInUp 0.3s ease-out;
        `;

        const header = document.createElement('div');
        header.style.cssText = `
            padding: 1.5rem;
            border-bottom: 1px solid #e5e7eb;
            display: flex;
            justify-content: space-between;
            align-items: center;
        `;

        const title = document.createElement('h3');
        title.textContent = options.title;
        title.style.margin = '0';

        const closeBtn = document.createElement('button');
        closeBtn.innerHTML = '<i class="fas fa-times"></i>';
        closeBtn.style.cssText = `
            background: none;
            border: none;
            font-size: 1.2rem;
            cursor: pointer;
            padding: 0.5rem;
            border-radius: 4px;
        `;
        closeBtn.onclick = () => this.close(overlay);

        header.appendChild(title);
        header.appendChild(closeBtn);

        const body = document.createElement('div');
        body.style.padding = '1.5rem';
        body.innerHTML = options.body;

        const footer = document.createElement('div');
        footer.style.cssText = `
            padding: 1rem 1.5rem;
            border-top: 1px solid #e5e7eb;
            display: flex;
            gap: 0.5rem;
            justify-content: flex-end;
        `;

        if (options.actions) {
            options.actions.forEach(action => {
                const btn = document.createElement('button');
                btn.textContent = action.text;
                btn.className = `btn ${action.class || 'btn-secondary'}`;
                btn.onclick = action.action;
                footer.appendChild(btn);
            });
        }

        modal.appendChild(header);
        modal.appendChild(body);
        if (options.actions) modal.appendChild(footer);
        overlay.appendChild(modal);
        document.body.appendChild(overlay);

        // Close on overlay click
        overlay.addEventListener('click', (e) => {
            if (e.target === overlay) {
                this.close(overlay);
            }
        });

        // Close on Escape key
        const escapeHandler = (e) => {
            if (e.key === 'Escape') {
                this.close(overlay);
                document.removeEventListener('keydown', escapeHandler);
            }
        };
        document.addEventListener('keydown', escapeHandler);

        return overlay;
    },

    // Close modal
    close: function (modal) {
        modal.style.animation = 'fadeOut 0.3s ease-out';
        setTimeout(() => {
            if (modal.parentNode) {
                modal.remove();
            }
        }, 300);
    }
};

// Loading indicator
PixelSolution.Loading = {
    show: function (target = document.body, message = 'Loading...') {
        const loader = document.createElement('div');
        loader.className = 'loading-overlay';
        loader.style.cssText = `
            position: ${target === document.body ? 'fixed' : 'absolute'};
            top: 0;
            left: 0;
            width: 100%;
            height: 100%;
            background: rgba(255,255,255,0.8);
            display: flex;
            align-items: center;
            justify-content: center;
            z-index: 9998;
            flex-direction: column;
            gap: 1rem;
        `;

        loader.innerHTML = `
            <div class="spinner" style="
                width: 40px;
                height: 40px;
                border: 4px solid #e5e7eb;
                border-top: 4px solid #3b82f6;
                border-radius: 50%;
                animation: spin 1s linear infinite;
            "></div>
            <div style="color: #6b7280; font-weight: 500;">${message}</div>
        `;

        if (target !== document.body) {
            target.style.position = 'relative';
        }

        target.appendChild(loader);
        return loader;
    },

    hide: function (loader) {
        if (loader && loader.parentNode) {
            loader.remove();
        }
    }
};

// Form utilities
PixelSolution.Form = {
    // Serialize form data
    serialize: function (form) {
        const formData = new FormData(form);
        const data = {};

        for (let [key, value] of formData.entries()) {
            if (data[key]) {
                if (Array.isArray(data[key])) {
                    data[key].push(value);
                } else {
                    data[key] = [data[key], value];
                }
            } else {
                data[key] = value;
            }
        }

        return data;
    },

    // Validate form
    validate: function (form) {
        const errors = [];
        const inputs = form.querySelectorAll('input[required], select[required], textarea[required]');

        inputs.forEach(input => {
            if (!input.value.trim()) {
                errors.push(`${input.name || input.id} is required`);
                input.classList.add('input-validation-error');
            } else {
                input.classList.remove('input-validation-error');
            }
        });

        return {
            isValid: errors.length === 0,
            errors: errors
        };
    },

    // Clear form
    clear: function (form) {
        const inputs = form.querySelectorAll('input, select, textarea');
        inputs.forEach(input => {
            if (input.type === 'checkbox' || input.type === 'radio') {
                input.checked = false;
            } else {
                input.value = '';
            }
            input.classList.remove('input-validation-error');
        });
    }
};

// Add CSS animations
document.addEventListener('DOMContentLoaded', function () {
    const style = document.createElement('style');
    style.textContent = `
        @keyframes fadeIn {
            from { opacity: 0; }
            to { opacity: 1; }
        }
        
        @keyframes fadeOut {
            from { opacity: 1; }
            to { opacity: 0; }
        }
        
        @keyframes slideInRight {
            from {
                transform: translateX(100%);
                opacity: 0;
            }
            to {
                transform: translateX(0);
                opacity: 1;
            }
        }
        
        @keyframes slideOutRight {
            from {
                transform: translateX(0);
                opacity: 1;
            }
            to {
                transform: translateX(100%);
                opacity: 0;
            }
        }
        
        @keyframes slideInUp {
            from {
                transform: translateY(30px);
                opacity: 0;
            }
            to {
                transform: translateY(0);
                opacity: 1;
            }
        }
        
        @keyframes spin {
            to { transform: rotate(360deg); }
        }
    `;
    document.head.appendChild(style);
});

// Global aliases for convenience
window.showToast = PixelSolution.Toast.show.bind(PixelSolution.Toast);
window.showSuccess = PixelSolution.Toast.success.bind(PixelSolution.Toast);
window.showError = PixelSolution.Toast.error.bind(PixelSolution.Toast);
window.showWarning = PixelSolution.Toast.warning.bind(PixelSolution.Toast);
window.showInfo = PixelSolution.Toast.info.bind(PixelSolution.Toast);
window.confirmDialog = PixelSolution.Modal.confirm.bind(PixelSolution.Modal);
window.alertDialog = PixelSolution.Modal.alert.bind(PixelSolution.Modal);

// Initialize on DOM ready
document.addEventListener('DOMContentLoaded', function () {
    // Initialize toast container
    PixelSolution.Toast.init();

    // Add global error handler
    window.addEventListener('error', function (e) {
        console.error('Global error:', e.error);
        if (window.location.hostname !== 'localhost') {
            showError('An unexpected error occurred. Please refresh the page and try again.');
        }
    });

    // Add unhandled promise rejection handler
    window.addEventListener('unhandledrejection', function (e) {
        console.error('Unhandled promise rejection:', e.reason);
        e.preventDefault();
    });

    console.log('PixelSolution initialized successfully');
});