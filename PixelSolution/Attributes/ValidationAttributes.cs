using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace PixelSolution.Attributes
{
    /// <summary>
    /// Validates Kenyan phone numbers (254 format)
    /// </summary>
    public class KenyanPhoneNumberAttribute : ValidationAttribute
    {
        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            if (value == null || string.IsNullOrWhiteSpace(value.ToString()))
            {
                return ValidationResult.Success; // Let [Required] handle empty values
            }

            var phoneNumber = value.ToString().Trim();
            
            // Remove common phone number characters
            phoneNumber = Regex.Replace(phoneNumber, @"[\s\-\(\)]", "");
            
            // Valid formats: 254XXXXXXXXX (12 digits) or 0XXXXXXXXX (10 digits starting with 0)
            if (Regex.IsMatch(phoneNumber, @"^254[17]\d{8}$") || Regex.IsMatch(phoneNumber, @"^0[17]\d{8}$"))
            {
                return ValidationResult.Success;
            }

            return new ValidationResult("Please enter a valid Kenyan phone number (e.g., 254712345678 or 0712345678)");
        }
    }

    /// <summary>
    /// Sanitizes input to prevent SQL Injection and XSS
    /// </summary>
    public class SanitizeInputAttribute : ValidationAttribute
    {
        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            if (value == null)
            {
                return ValidationResult.Success;
            }

            var input = value.ToString();
            
            // Check for SQL injection patterns
            var sqlPatterns = new[]
            {
                @"(\bOR\b|\bAND\b)\s+\d+\s*=\s*\d+",
                @"UNION\s+SELECT",
                @"DROP\s+TABLE",
                @"INSERT\s+INTO",
                @"DELETE\s+FROM",
                @"UPDATE\s+\w+\s+SET",
                @"EXEC(\s|\+)+(s|x)p\w+",
                @"--",
                @";.*--",
                @"xp_cmdshell"
            };

            foreach (var pattern in sqlPatterns)
            {
                if (Regex.IsMatch(input, pattern, RegexOptions.IgnoreCase))
                {
                    return new ValidationResult("Input contains invalid characters or patterns");
                }
            }

            // Check for XSS patterns
            var xssPatterns = new[]
            {
                @"<script[^>]*>.*?</script>",
                @"javascript:",
                @"onerror\s*=",
                @"onload\s*=",
                @"<iframe",
                @"<object",
                @"<embed"
            };

            foreach (var pattern in xssPatterns)
            {
                if (Regex.IsMatch(input, pattern, RegexOptions.IgnoreCase))
                {
                    return new ValidationResult("Input contains invalid characters or patterns");
                }
            }

            return ValidationResult.Success;
        }
    }

    /// <summary>
    /// Validates amount is within reasonable range for M-Pesa transactions
    /// </summary>
    public class MpesaAmountAttribute : ValidationAttribute
    {
        private const decimal MIN_AMOUNT = 1;
        private const decimal MAX_AMOUNT = 250000; // M-Pesa transaction limit

        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            if (value == null)
            {
                return new ValidationResult("Amount is required");
            }

            if (decimal.TryParse(value.ToString(), out decimal amount))
            {
                if (amount < MIN_AMOUNT)
                {
                    return new ValidationResult($"Amount must be at least KSh {MIN_AMOUNT}");
                }

                if (amount > MAX_AMOUNT)
                {
                    return new ValidationResult($"Amount cannot exceed KSh {MAX_AMOUNT:N0} (M-Pesa limit)");
                }

                return ValidationResult.Success;
            }

            return new ValidationResult("Invalid amount format");
        }
    }

    /// <summary>
    /// Validates password strength
    /// </summary>
    public class StrongPasswordAttribute : ValidationAttribute
    {
        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            if (value == null || string.IsNullOrWhiteSpace(value.ToString()))
            {
                return new ValidationResult("Password is required");
            }

            var password = value.ToString();

            if (password.Length < 8)
            {
                return new ValidationResult("Password must be at least 8 characters long");
            }

            if (!Regex.IsMatch(password, @"[A-Z]"))
            {
                return new ValidationResult("Password must contain at least one uppercase letter");
            }

            if (!Regex.IsMatch(password, @"[a-z]"))
            {
                return new ValidationResult("Password must contain at least one lowercase letter");
            }

            if (!Regex.IsMatch(password, @"\d"))
            {
                return new ValidationResult("Password must contain at least one number");
            }

            if (!Regex.IsMatch(password, @"[!@#$%^&*()_+\-=\[\]{};':""\\|,.<>?]"))
            {
                return new ValidationResult("Password must contain at least one special character");
            }

            // Check for common weak passwords
            var weakPasswords = new[] { "password", "12345678", "qwerty", "admin", "letmein" };
            if (weakPasswords.Any(weak => password.ToLower().Contains(weak)))
            {
                return new ValidationResult("Password is too common. Please choose a stronger password");
            }

            return ValidationResult.Success;
        }
    }

    /// <summary>
    /// Validates email format with additional security checks
    /// </summary>
    public class SecureEmailAttribute : ValidationAttribute
    {
        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            if (value == null || string.IsNullOrWhiteSpace(value.ToString()))
            {
                return new ValidationResult("Email is required");
            }

            var email = value.ToString().Trim();

            // Basic email format validation
            var emailRegex = @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$";
            if (!Regex.IsMatch(email, emailRegex))
            {
                return new ValidationResult("Please enter a valid email address");
            }

            // Block disposable email domains
            var disposableDomains = new[] { "tempmail.com", "10minutemail.com", "guerrillamail.com", "mailinator.com" };
            var domain = email.Split('@')[1].ToLower();
            if (disposableDomains.Contains(domain))
            {
                return new ValidationResult("Please use a permanent email address");
            }

            return ValidationResult.Success;
        }
    }
}
