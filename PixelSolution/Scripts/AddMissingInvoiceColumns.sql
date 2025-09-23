-- Add missing columns to SupplierInvoices table
-- Check if columns exist before adding them

-- Add CreatedByUserId column if it doesn't exist
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[SupplierInvoices]') AND name = 'CreatedByUserId')
BEGIN
    ALTER TABLE [dbo].[SupplierInvoices] 
    ADD [CreatedByUserId] int NOT NULL DEFAULT 1;
    
    -- Add foreign key constraint
    ALTER TABLE [dbo].[SupplierInvoices]
    ADD CONSTRAINT FK_SupplierInvoices_Users_CreatedByUserId 
    FOREIGN KEY ([CreatedByUserId]) REFERENCES [dbo].[Users]([UserId]);
    
    PRINT 'Added CreatedByUserId column to SupplierInvoices table';
END
ELSE
BEGIN
    PRINT 'CreatedByUserId column already exists in SupplierInvoices table';
END

-- Add PaymentStatus column if it doesn't exist
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[SupplierInvoices]') AND name = 'PaymentStatus')
BEGIN
    ALTER TABLE [dbo].[SupplierInvoices] 
    ADD [PaymentStatus] nvarchar(20) NOT NULL DEFAULT 'Unpaid';
    
    PRINT 'Added PaymentStatus column to SupplierInvoices table';
END
ELSE
BEGIN
    PRINT 'PaymentStatus column already exists in SupplierInvoices table';
END

-- Verify the columns were added
SELECT 
    COLUMN_NAME, 
    DATA_TYPE, 
    IS_NULLABLE, 
    COLUMN_DEFAULT
FROM INFORMATION_SCHEMA.COLUMNS 
WHERE TABLE_NAME = 'SupplierInvoices' 
    AND COLUMN_NAME IN ('CreatedByUserId', 'PaymentStatus')
ORDER BY COLUMN_NAME;

PRINT 'Missing columns check completed for SupplierInvoices table';
