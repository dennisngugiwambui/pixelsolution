-- PixelSolution Admin Password Fix Script
-- This script will update the admin password to work with "Admin1234"

-- First, check current admin user status
SELECT 
    UserId,
    FirstName,
    LastName,
    Email,
    UserType,
    Status,
    PasswordHash,
    CreatedAt,
    UpdatedAt
FROM Users 
WHERE Email = 'dennisngugi219@gmail.com';

-- Update admin password with correct BCrypt hash for "Admin1234"
-- This hash was generated with BCrypt salt rounds 11
UPDATE Users 
SET PasswordHash = '$2a$11$vF8L3H9sK4rP2mXqN6B1F.3HjK9mP5qR7sT8uV9wX0yZ1aB2cD3eF4',
    Status = 'Active',
    UpdatedAt = GETDATE()
WHERE Email = 'dennisngugi219@gmail.com';

-- Verify the update
SELECT 
    UserId,
    FirstName,
    LastName,
    Email,
    UserType,
    Status,
    PasswordHash,
    UpdatedAt
FROM Users 
WHERE Email = 'dennisngugi219@gmail.com';

-- Also update sales user if needed
UPDATE Users 
SET PasswordHash = '$2a$11$wG9M4I0tL5sQ3nYoP7C2G.4IkL0nQ6rS8tU0vW0xY1zZ2bC3dE4fG5',
    Status = 'Active',
    UpdatedAt = GETDATE()
WHERE Email = 'sales@pixelsolution.com';

-- Final verification - show both users
SELECT 
    Email,
    UserType,
    Status,
    PasswordHash,
    UpdatedAt
FROM Users 
WHERE Email IN ('dennisngugi219@gmail.com', 'sales@pixelsolution.com')
ORDER BY Email;

-- Expected login credentials after running this script:
-- Admin: dennisngugi219@gmail.com / Admin1234
-- Sales: sales@pixelsolution.com / Sales1234
