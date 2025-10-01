using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PixelSolution.Migrations
{
    /// <inheritdoc />
    public partial class AddMpesaReceiptNumberToSales : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SupplierInvoiceItems_SupplierItems_SupplierItemId",
                table: "SupplierInvoiceItems");

            migrationBuilder.DropTable(
                name: "SupplierItems");

            migrationBuilder.RenameColumn(
                name: "SubTotal",
                table: "SupplierInvoices",
                newName: "Subtotal");

            migrationBuilder.RenameColumn(
                name: "SupplierItemId",
                table: "SupplierInvoiceItems",
                newName: "SupplierProductSupplyId");

            migrationBuilder.RenameIndex(
                name: "IX_SupplierInvoiceItems_SupplierItemId",
                table: "SupplierInvoiceItems",
                newName: "IX_SupplierInvoiceItems_SupplierProductSupplyId");

            migrationBuilder.AddColumn<string>(
                name: "ProcessedBy",
                table: "SupplierPayments",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "MpesaReceiptNumber",
                table: "Sales",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SupplierProductSupplies",
                columns: table => new
                {
                    SupplierProductSupplyId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SupplierId = table.Column<int>(type: "int", nullable: false),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    QuantitySupplied = table.Column<int>(type: "int", nullable: false),
                    UnitCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    BatchNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SupplyDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiryDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PaymentStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierProductSupplies", x => x.SupplierProductSupplyId);
                    table.ForeignKey(
                        name: "FK_SupplierProductSupplies_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "ProductId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierProductSupplies_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "SupplierId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SupplierProductSupplies_ProductId",
                table: "SupplierProductSupplies",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierProductSupplies_SupplierId",
                table: "SupplierProductSupplies",
                column: "SupplierId");

            migrationBuilder.AddForeignKey(
                name: "FK_SupplierInvoiceItems_SupplierProductSupplies_SupplierProductSupplyId",
                table: "SupplierInvoiceItems",
                column: "SupplierProductSupplyId",
                principalTable: "SupplierProductSupplies",
                principalColumn: "SupplierProductSupplyId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SupplierInvoiceItems_SupplierProductSupplies_SupplierProductSupplyId",
                table: "SupplierInvoiceItems");

            migrationBuilder.DropTable(
                name: "SupplierProductSupplies");

            migrationBuilder.DropColumn(
                name: "ProcessedBy",
                table: "SupplierPayments");

            migrationBuilder.DropColumn(
                name: "MpesaReceiptNumber",
                table: "Sales");

            migrationBuilder.RenameColumn(
                name: "Subtotal",
                table: "SupplierInvoices",
                newName: "SubTotal");

            migrationBuilder.RenameColumn(
                name: "SupplierProductSupplyId",
                table: "SupplierInvoiceItems",
                newName: "SupplierItemId");

            migrationBuilder.RenameIndex(
                name: "IX_SupplierInvoiceItems_SupplierProductSupplyId",
                table: "SupplierInvoiceItems",
                newName: "IX_SupplierInvoiceItems_SupplierItemId");

            migrationBuilder.CreateTable(
                name: "SupplierItems",
                columns: table => new
                {
                    SupplierItemId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    SupplierId = table.Column<int>(type: "int", nullable: false),
                    BatchNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    ExpiryDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SupplyDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TotalCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    UnitCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierItems", x => x.SupplierItemId);
                    table.ForeignKey(
                        name: "FK_SupplierItems_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "ProductId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierItems_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "SupplierId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SupplierItems_ProductId",
                table: "SupplierItems",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierItems_SupplierId",
                table: "SupplierItems",
                column: "SupplierId");

            migrationBuilder.AddForeignKey(
                name: "FK_SupplierInvoiceItems_SupplierItems_SupplierItemId",
                table: "SupplierInvoiceItems",
                column: "SupplierItemId",
                principalTable: "SupplierItems",
                principalColumn: "SupplierItemId",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
