-- Migration script to add SupplierProductSupply table and update related tables
-- Execute this script on your database to add supplier product supply tracking functionality

-- Create SupplierProductSupply table
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='SupplierProductSupplies' AND xtype='U')
BEGIN
    CREATE TABLE [dbo].[SupplierProductSupplies](
        [SupplierProductSupplyId] [int] IDENTITY(1,1) NOT NULL,
        [SupplierId] [int] NOT NULL,
        [ProductId] [int] NOT NULL,
        [QuantitySupplied] [int] NOT NULL,
        [UnitCost] [decimal](18, 2) NOT NULL,
        [TotalCost] [decimal](18, 2) NOT NULL,
        [BatchNumber] [nvarchar](50) NULL,
        [SupplyDate] [datetime2](7) NOT NULL,
        [ExpiryDate] [datetime2](7) NULL,
        [PaymentStatus] [nvarchar](20) NOT NULL,
        [Notes] [nvarchar](500) NULL,
        [CreatedAt] [datetime2](7) NOT NULL,
        [UpdatedAt] [datetime2](7) NOT NULL,
        CONSTRAINT [PK_SupplierProductSupplies] PRIMARY KEY CLUSTERED ([SupplierProductSupplyId] ASC),
        CONSTRAINT [FK_SupplierProductSupplies_Suppliers] FOREIGN KEY([SupplierId]) REFERENCES [dbo].[Suppliers] ([SupplierId]) ON DELETE CASCADE,
        CONSTRAINT [FK_SupplierProductSupplies_Products] FOREIGN KEY([ProductId]) REFERENCES [dbo].[Products] ([ProductId]) ON DELETE CASCADE
    )
    
    -- Add default constraints
    ALTER TABLE [dbo].[SupplierProductSupplies] ADD CONSTRAINT [DF_SupplierProductSupplies_PaymentStatus] DEFAULT ('Pending') FOR [PaymentStatus]
    ALTER TABLE [dbo].[SupplierProductSupplies] ADD CONSTRAINT [DF_SupplierProductSupplies_SupplyDate] DEFAULT (getutcdate()) FOR [SupplyDate]
    ALTER TABLE [dbo].[SupplierProductSupplies] ADD CONSTRAINT [DF_SupplierProductSupplies_CreatedAt] DEFAULT (getutcdate()) FOR [CreatedAt]
    ALTER TABLE [dbo].[SupplierProductSupplies] ADD CONSTRAINT [DF_SupplierProductSupplies_UpdatedAt] DEFAULT (getutcdate()) FOR [UpdatedAt]
    
    PRINT 'SupplierProductSupplies table created successfully'
END
ELSE
BEGIN
    PRINT 'SupplierProductSupplies table already exists'
END

-- Create SupplierInvoices table if it doesn't exist
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='SupplierInvoices' AND xtype='U')
BEGIN
    CREATE TABLE [dbo].[SupplierInvoices](
        [SupplierInvoiceId] [int] IDENTITY(1,1) NOT NULL,
        [SupplierId] [int] NOT NULL,
        [InvoiceNumber] [nvarchar](50) NOT NULL,
        [InvoiceDate] [datetime2](7) NOT NULL,
        [DueDate] [datetime2](7) NOT NULL,
        [Subtotal] [decimal](18, 2) NOT NULL,
        [TaxAmount] [decimal](18, 2) NOT NULL,
        [TotalAmount] [decimal](18, 2) NOT NULL,
        [AmountPaid] [decimal](18, 2) NOT NULL,
        [AmountDue] [decimal](18, 2) NOT NULL,
        [Status] [nvarchar](20) NOT NULL,
        [Notes] [nvarchar](500) NULL,
        [CreatedAt] [datetime2](7) NOT NULL,
        [UpdatedAt] [datetime2](7) NOT NULL,
        CONSTRAINT [PK_SupplierInvoices] PRIMARY KEY CLUSTERED ([SupplierInvoiceId] ASC),
        CONSTRAINT [FK_SupplierInvoices_Suppliers] FOREIGN KEY([SupplierId]) REFERENCES [dbo].[Suppliers] ([SupplierId]) ON DELETE CASCADE,
        CONSTRAINT [UQ_SupplierInvoices_InvoiceNumber] UNIQUE ([InvoiceNumber])
    )
    
    -- Add default constraints
    ALTER TABLE [dbo].[SupplierInvoices] ADD CONSTRAINT [DF_SupplierInvoices_Status] DEFAULT ('Pending') FOR [Status]
    ALTER TABLE [dbo].[SupplierInvoices] ADD CONSTRAINT [DF_SupplierInvoices_AmountPaid] DEFAULT (0) FOR [AmountPaid]
    ALTER TABLE [dbo].[SupplierInvoices] ADD CONSTRAINT [DF_SupplierInvoices_CreatedAt] DEFAULT (getutcdate()) FOR [CreatedAt]
    ALTER TABLE [dbo].[SupplierInvoices] ADD CONSTRAINT [DF_SupplierInvoices_UpdatedAt] DEFAULT (getutcdate()) FOR [UpdatedAt]
    
    PRINT 'SupplierInvoices table created successfully'
END
ELSE
BEGIN
    PRINT 'SupplierInvoices table already exists'
END

-- Create SupplierInvoiceItems table if it doesn't exist
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='SupplierInvoiceItems' AND xtype='U')
BEGIN
    CREATE TABLE [dbo].[SupplierInvoiceItems](
        [SupplierInvoiceItemId] [int] IDENTITY(1,1) NOT NULL,
        [SupplierInvoiceId] [int] NOT NULL,
        [SupplierProductSupplyId] [int] NOT NULL,
        [Quantity] [int] NOT NULL,
        [UnitCost] [decimal](18, 2) NOT NULL,
        [TotalCost] [decimal](18, 2) NOT NULL,
        [Description] [nvarchar](500) NULL,
        CONSTRAINT [PK_SupplierInvoiceItems] PRIMARY KEY CLUSTERED ([SupplierInvoiceItemId] ASC),
        CONSTRAINT [FK_SupplierInvoiceItems_SupplierInvoices] FOREIGN KEY([SupplierInvoiceId]) REFERENCES [dbo].[SupplierInvoices] ([SupplierInvoiceId]) ON DELETE CASCADE,
        CONSTRAINT [FK_SupplierInvoiceItems_SupplierProductSupplies] FOREIGN KEY([SupplierProductSupplyId]) REFERENCES [dbo].[SupplierProductSupplies] ([SupplierProductSupplyId]) ON DELETE CASCADE
    )
    
    PRINT 'SupplierInvoiceItems table created successfully'
END
ELSE
BEGIN
    PRINT 'SupplierInvoiceItems table already exists'
END

-- Create SupplierPayments table if it doesn't exist
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='SupplierPayments' AND xtype='U')
BEGIN
    CREATE TABLE [dbo].[SupplierPayments](
        [SupplierPaymentId] [int] IDENTITY(1,1) NOT NULL,
        [SupplierInvoiceId] [int] NOT NULL,
        [Amount] [decimal](18, 2) NOT NULL,
        [PaymentMethod] [nvarchar](50) NOT NULL,
        [PaymentReference] [nvarchar](100) NULL,
        [PaymentDate] [datetime2](7) NOT NULL,
        [Notes] [nvarchar](500) NULL,
        [Status] [nvarchar](20) NOT NULL,
        [ProcessedBy] [nvarchar](256) NOT NULL,
        [CreatedAt] [datetime2](7) NOT NULL,
        CONSTRAINT [PK_SupplierPayments] PRIMARY KEY CLUSTERED ([SupplierPaymentId] ASC),
        CONSTRAINT [FK_SupplierPayments_SupplierInvoices] FOREIGN KEY([SupplierInvoiceId]) REFERENCES [dbo].[SupplierInvoices] ([SupplierInvoiceId]) ON DELETE CASCADE
    )
    
    -- Add default constraints
    ALTER TABLE [dbo].[SupplierPayments] ADD CONSTRAINT [DF_SupplierPayments_Status] DEFAULT ('Completed') FOR [Status]
    ALTER TABLE [dbo].[SupplierPayments] ADD CONSTRAINT [DF_SupplierPayments_CreatedAt] DEFAULT (getutcdate()) FOR [CreatedAt]
    
    PRINT 'SupplierPayments table created successfully'
END
ELSE
BEGIN
    PRINT 'SupplierPayments table already exists'
END

-- Drop old SupplierItems table if it exists (since we're replacing it with SupplierProductSupplies)
IF EXISTS (SELECT * FROM sysobjects WHERE name='SupplierItems' AND xtype='U')
BEGIN
    -- First drop any foreign key constraints that reference SupplierItems
    IF EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_SupplierInvoiceItems_SupplierItems')
    BEGIN
        ALTER TABLE [dbo].[SupplierInvoiceItems] DROP CONSTRAINT [FK_SupplierInvoiceItems_SupplierItems]
        PRINT 'Dropped FK_SupplierInvoiceItems_SupplierItems constraint'
    END
    
    -- Check if there's data in SupplierItems before dropping
    DECLARE @SupplierItemsCount INT
    SELECT @SupplierItemsCount = COUNT(*) FROM [dbo].[SupplierItems]
    
    IF @SupplierItemsCount > 0
    BEGIN
        PRINT 'WARNING: SupplierItems table contains ' + CAST(@SupplierItemsCount AS VARCHAR(10)) + ' records.'
        PRINT 'Please migrate this data to SupplierProductSupplies table before dropping SupplierItems.'
        PRINT 'Skipping drop of SupplierItems table to preserve data.'
    END
    ELSE
    BEGIN
        DROP TABLE [dbo].[SupplierItems]
        PRINT 'SupplierItems table dropped successfully (was empty)'
    END
END

-- Create indexes for better performance
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_SupplierProductSupplies_SupplierId')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_SupplierProductSupplies_SupplierId] ON [dbo].[SupplierProductSupplies] ([SupplierId])
    PRINT 'Created index IX_SupplierProductSupplies_SupplierId'
END

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_SupplierProductSupplies_ProductId')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_SupplierProductSupplies_ProductId] ON [dbo].[SupplierProductSupplies] ([ProductId])
    PRINT 'Created index IX_SupplierProductSupplies_ProductId'
END

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_SupplierProductSupplies_SupplyDate')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_SupplierProductSupplies_SupplyDate] ON [dbo].[SupplierProductSupplies] ([SupplyDate])
    PRINT 'Created index IX_SupplierProductSupplies_SupplyDate'
END

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_SupplierProductSupplies_PaymentStatus')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_SupplierProductSupplies_PaymentStatus] ON [dbo].[SupplierProductSupplies] ([PaymentStatus])
    PRINT 'Created index IX_SupplierProductSupplies_PaymentStatus'
END

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_SupplierInvoices_SupplierId')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_SupplierInvoices_SupplierId] ON [dbo].[SupplierInvoices] ([SupplierId])
    PRINT 'Created index IX_SupplierInvoices_SupplierId'
END

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_SupplierInvoices_Status')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_SupplierInvoices_Status] ON [dbo].[SupplierInvoices] ([Status])
    PRINT 'Created index IX_SupplierInvoices_Status'
END

PRINT 'Migration completed successfully!'
PRINT 'New tables created:'
PRINT '- SupplierProductSupplies: Tracks supply batches for products from suppliers'
PRINT '- SupplierInvoices: Manages invoices for supplier supplies'
PRINT '- SupplierInvoiceItems: Line items for supplier invoices'
PRINT '- SupplierPayments: Tracks payments made to suppliers'
PRINT ''
PRINT 'Next steps:'
PRINT '1. Verify all tables were created correctly'
PRINT '2. Test the supplier product supply workflow in the application'
PRINT '3. If you have existing SupplierItems data, migrate it to SupplierProductSupplies'
