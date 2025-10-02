-- Fix CompletedDate duplicate column issue
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[PurchaseRequests]') AND name = 'CompletedDate')
BEGIN
    PRINT 'CompletedDate column already exists - no action needed'
END
ELSE
BEGIN
    ALTER TABLE [PurchaseRequests] ADD [CompletedDate] datetime2 NULL;
    PRINT 'CompletedDate column added successfully'
END
GO
