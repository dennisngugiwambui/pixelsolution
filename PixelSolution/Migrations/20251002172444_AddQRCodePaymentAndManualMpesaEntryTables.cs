using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PixelSolution.Migrations
{
    /// <inheritdoc />
    public partial class AddQRCodePaymentAndManualMpesaEntryTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ManualMpesaEntries",
                columns: table => new
                {
                    ManualMpesaEntryId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MpesaMessage = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TransactionCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SenderPhone = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SenderName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TransactionDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SaleId = table.Column<int>(type: "int", nullable: true),
                    EnteredByUserId = table.Column<int>(type: "int", nullable: false),
                    VerificationNotes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsVerified = table.Column<bool>(type: "bit", nullable: false),
                    VerifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TillNumber = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ManualMpesaEntries", x => x.ManualMpesaEntryId);
                    table.ForeignKey(
                        name: "FK_ManualMpesaEntries_Sales_SaleId",
                        column: x => x.SaleId,
                        principalTable: "Sales",
                        principalColumn: "SaleId");
                    table.ForeignKey(
                        name: "FK_ManualMpesaEntries_Users_EnteredByUserId",
                        column: x => x.EnteredByUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "QRCodePayments",
                columns: table => new
                {
                    QRCodePaymentId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    QRCodeReference = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TillNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CustomerPhone = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CustomerName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PaidAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MpesaReceiptNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TransactionCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SaleId = table.Column<int>(type: "int", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QRCodePayments", x => x.QRCodePaymentId);
                    table.ForeignKey(
                        name: "FK_QRCodePayments_Sales_SaleId",
                        column: x => x.SaleId,
                        principalTable: "Sales",
                        principalColumn: "SaleId");
                    table.ForeignKey(
                        name: "FK_QRCodePayments_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ManualMpesaEntries_EnteredByUserId",
                table: "ManualMpesaEntries",
                column: "EnteredByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ManualMpesaEntries_SaleId",
                table: "ManualMpesaEntries",
                column: "SaleId");

            migrationBuilder.CreateIndex(
                name: "IX_QRCodePayments_CreatedByUserId",
                table: "QRCodePayments",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_QRCodePayments_SaleId",
                table: "QRCodePayments",
                column: "SaleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ManualMpesaEntries");

            migrationBuilder.DropTable(
                name: "QRCodePayments");
        }
    }
}
