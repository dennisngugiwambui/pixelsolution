-- Add MpesaReceiptNumber column to Sales table
-- Run this SQL script directly in SQL Server Management Studio or Azure Data Studio

-- Check if column already exists before adding
IF NOT EXISTS (
    SELECT * FROM sys.columns 
    WHERE object_id = OBJECT_ID(N'[dbo].[Sales]') 
    AND name = 'MpesaReceiptNumber'
)
BEGIN
    ALTER TABLE [Sales] 
    ADD [MpesaReceiptNumber] NVARCHAR(50) NULL;
    
    PRINT 'Column MpesaReceiptNumber added successfully to Sales table';
END
ELSE
BEGIN
    PRINT 'Column MpesaReceiptNumber already exists in Sales table';
END
GO

-- Create index for faster searches
IF NOT EXISTS (
    SELECT * FROM sys.indexes 
    WHERE name = 'IX_Sales_MpesaReceiptNumber' 
    AND object_id = OBJECT_ID(N'[dbo].[Sales]')
)
BEGIN
    CREATE INDEX IX_Sales_MpesaReceiptNumber 
    ON [Sales] ([MpesaReceiptNumber]);
    
    PRINT 'Index IX_Sales_MpesaReceiptNumber created successfully';
END
ELSE
BEGIN
    PRINT 'Index IX_Sales_MpesaReceiptNumber already exists';
END
GO

PRINT 'Migration completed successfully!';
