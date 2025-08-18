-- Migration to add UserActivityLog table
-- Run this script in your SQL Server database

CREATE TABLE [dbo].[UserActivityLogs] (
    [ActivityId] int IDENTITY(1,1) NOT NULL,
    [UserId] int NOT NULL,
    [ActivityType] nvarchar(100) NOT NULL,
    [Description] nvarchar(500) NOT NULL,
    [EntityType] nvarchar(50) NULL,
    [EntityId] int NULL,
    [Details] nvarchar(1000) NULL,
    [IpAddress] nvarchar(45) NOT NULL,
    [UserAgent] nvarchar(500) NULL,
    [CreatedAt] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
    CONSTRAINT [PK_UserActivityLogs] PRIMARY KEY ([ActivityId]),
    CONSTRAINT [FK_UserActivityLogs_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([UserId]) ON DELETE CASCADE
);

-- Create performance indexes
CREATE INDEX [IX_UserActivityLogs_UserId] ON [UserActivityLogs] ([UserId]);
CREATE INDEX [IX_UserActivityLogs_ActivityType] ON [UserActivityLogs] ([ActivityType]);
CREATE INDEX [IX_UserActivityLogs_CreatedAt] ON [UserActivityLogs] ([CreatedAt]);
CREATE INDEX [IX_UserActivityLogs_UserId_CreatedAt] ON [UserActivityLogs] ([UserId], [CreatedAt]);

PRINT 'UserActivityLog table and indexes created successfully!';
