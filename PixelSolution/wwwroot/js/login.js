// Minimal Login JavaScript - Server-side validation only
document.addEventListener('DOMContentLoaded', function () {
    initializeLoginPage();
});

function initializeLoginPage() {
    setupPasswordToggle();
    setupFormSubmission();
    setupInputAnimations();
    setupFloatingShapes();
    setupSocialLinks();
    autoHideMessages();
    console.log('Login page initialized successfully');
}

// Toggle password visibility
function togglePassword() {
    const passwordField = document.getElementById('Password');
    const toggleIcon = document.getElementById('passwordToggleIcon');

    if (passwordField && toggleIcon) {
        if (passwordField.type === 'password') {
            passwordField.type = 'text';
            toggleIcon.classList.remove('fa-eye');
            toggleIcon.classList.add('fa-eye-slash');
        } else {
            passwordField.type = 'password';
            toggleIcon.classList.remove('fa-eye-slash');
            toggleIcon.classList.add('fa-eye');
        }
    }
}

// Setup password toggle functionality
function setupPasswordToggle() {
    const passwordToggle = document.querySelector('.password-toggle');
    if (passwordToggle) {
        passwordToggle.addEventListener('click', function (e) {
            e.preventDefault();
            togglePassword();
        });
    }
}

// Simple form submission - NO CLIENT-SIDE VALIDATION
function setupFormSubmission() {
    const form = document.getElementById('loginForm');
    if (form) {
        form.addEventListener('submit', function (e) {
            console.log('=== FORM SUBMISSION DEBUG ===');

            // Just show loading state - let server handle validation
            showLoadingState();
            hideMessages();

            // Debug form data
            const formData = new FormData(this);
            console.log('Form Data:');
            for (let [key, value] of formData.entries()) {
                const displayValue = key.toLowerCase().includes('password') ? '***HIDDEN***' : value;
                console.log(`${key}: ${displayValue}`);
            }
            console.log('=== END DEBUG ===');

            // Let form submit normally to server
            console.log('Submitting to server for validation...');
        });
    }
}

// Show loading state
function showLoadingState() {
    const button = document.getElementById('loginButton');
    const buttonText = button?.querySelector('.button-text');
    const buttonLoader = document.getElementById('buttonLoader');

    if (button && buttonText && buttonLoader) {
        buttonText.style.opacity = '0';
        buttonLoader.style.display = 'block';
        button.disabled = true;
    }
}

// Hide all messages
function hideMessages() {
    const errorDiv = document.getElementById('errorMessage');
    const successDiv = document.getElementById('successMessage');

    if (errorDiv) errorDiv.style.display = 'none';
    if (successDiv) successDiv.style.display = 'none';
}

// Demo credentials functionality
function fillCredentials(type) {
    const emailField = document.getElementById('Email');
    const passwordField = document.getElementById('Password');

    if (emailField && passwordField) {
        if (type === 'admin') {
            emailField.value = 'dennisngugi219@gmail.com';
            passwordField.value = 'AdminPassword123!';
        } else if (type === 'employee') {
            emailField.value = 'sales@pixelsolution.com';
            passwordField.value = 'Employee123!';
        }
    }
}

// Input field animations
function setupInputAnimations() {
    const inputFields = document.querySelectorAll('.input-field');
    inputFields.forEach(input => {
        input.addEventListener('focus', function () {
            if (this.parentElement) {
                this.parentElement.style.transform = 'scale(1.02)';
                this.parentElement.style.transition = 'transform 0.2s ease';
            }
        });

        input.addEventListener('blur', function () {
            if (this.parentElement) {
                this.parentElement.style.transform = 'scale(1)';
            }
        });
    });
}

// Add floating animation to shapes
function setupFloatingShapes() {
    const shapes = document.querySelectorAll('.shape');
    shapes.forEach((shape, index) => {
        if (shape && shape.style) {
            shape.style.animationDelay = `${index * 0.5}s`;
            shape.style.animationDuration = `${6 + (index * 0.5)}s`;
        }
    });
}

// Loading animation for social links
function setupSocialLinks() {
    const socialLinks = document.querySelectorAll('.social-link');
    socialLinks.forEach(link => {
        link.addEventListener('click', function (e) {
            e.preventDefault();
            const originalIcon = this.innerHTML;
            this.innerHTML = '<i class="fas fa-spinner fa-spin"></i>';
            setTimeout(() => {
                this.innerHTML = originalIcon;
            }, 2000);
        });
    });
}

// Auto-hide messages after 5 seconds
function autoHideMessages() {
    setTimeout(function () {
        const messages = document.querySelectorAll('.error-message, .success-message, .info-message');
        messages.forEach(function (msg) {
            if (msg.style.display !== 'none') {
                msg.style.opacity = '0';
                setTimeout(function () {
                    msg.style.display = 'none';
                }, 300);
            }
        });
    }, 5000);
}

// Make functions globally available
window.fillCredentials = fillCredentials;
window.togglePassword = togglePassword;

console.log('Minimal login JavaScript loaded successfully');