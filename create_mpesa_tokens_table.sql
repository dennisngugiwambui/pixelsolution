-- Manual SQL script to create MpesaTokens table
-- Run this directly in SQL Server Management Studio or Azure Data Studio

USE [PixelSolutionDb]
GO

-- Check if table exists and drop if it does
IF OBJECT_ID('dbo.MpesaTokens', 'U') IS NOT NULL
    DROP TABLE dbo.MpesaTokens
GO

-- Create MpesaTokens table
CREATE TABLE [dbo].[MpesaTokens] (
    [MpesaTokenId] int IDENTITY(1,1) NOT NULL,
    [AccessToken] nvarchar(500) NOT NULL,
    [ExpiresAt] datetime2 NOT NULL,
    [CreatedAt] datetime2 NOT NULL DEFAULT (getutcdate()),
    [TokenType] nvarchar(50) NOT NULL DEFAULT ('Bearer'),
    [IsActive] bit NOT NULL DEFAULT (1),
    CONSTRAINT [PK_MpesaTokens] PRIMARY KEY ([MpesaTokenId])
)
GO

-- Create indexes for performance
CREATE INDEX [IX_MpesaTokens_IsActive_ExpiresAt] ON [dbo].[MpesaTokens] ([IsActive], [ExpiresAt])
GO

CREATE INDEX [IX_MpesaTokens_CreatedAt] ON [dbo].[MpesaTokens] ([CreatedAt])
GO

-- Verify table creation
SELECT 'MpesaTokens table created successfully' AS Result
GO

-- Show table structure
SELECT 
    COLUMN_NAME,
    DATA_TYPE,
    IS_NULLABLE,
    COLUMN_DEFAULT
FROM INFORMATION_SCHEMA.COLUMNS 
WHERE TABLE_NAME = 'MpesaTokens'
ORDER BY ORDINAL_POSITION
GO
