using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PixelSolution.Migrations
{
    /// <inheritdoc />
    public partial class UpdatePurchaseRequestModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Check if columns already exist before adding them
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[PurchaseRequests]') AND name = 'DeliveryDate')
                BEGIN
                    ALTER TABLE [PurchaseRequests] ADD [DeliveryDate] datetime2 NULL;
                END
            ");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[PurchaseRequests]') AND name = 'CompletedDate')
                BEGIN
                    ALTER TABLE [PurchaseRequests] ADD [CompletedDate] datetime2 NULL;
                END
            ");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[PurchaseRequests]') AND name = 'DeliveryAddress')
                BEGIN
                    ALTER TABLE [PurchaseRequests] ADD [DeliveryAddress] nvarchar(500) NOT NULL DEFAULT '';
                END
            ");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[PurchaseRequests]') AND name = 'PaymentStatus')
                BEGIN
                    ALTER TABLE [PurchaseRequests] ADD [PaymentStatus] nvarchar(20) NOT NULL DEFAULT 'Pending';
                END
            ");

            // Add Status column to PurchaseRequestItems table
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[PurchaseRequestItems]') AND name = 'Status')
                BEGIN
                    ALTER TABLE [PurchaseRequestItems] ADD [Status] nvarchar(20) NOT NULL DEFAULT 'Pending';
                END
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeliveryDate",
                table: "PurchaseRequests");

            migrationBuilder.DropColumn(
                name: "CompletedDate",
                table: "PurchaseRequests");

            migrationBuilder.DropColumn(
                name: "DeliveryAddress",
                table: "PurchaseRequests");

            migrationBuilder.DropColumn(
                name: "PaymentStatus",
                table: "PurchaseRequests");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "PurchaseRequestItems");
        }
    }
}
