using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PixelSolution.Migrations
{
    /// <inheritdoc />
    public partial class AddMpesaTokenTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MpesaTokens",
                columns: table => new
                {
                    MpesaTokenId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AccessToken = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    TokenType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MpesaTokens", x => x.MpesaTokenId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MpesaTokens_CreatedAt",
                table: "MpesaTokens",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_MpesaTokens_ExpiresAt",
                table: "MpesaTokens",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_MpesaTokens_IsActive",
                table: "MpesaTokens",
                column: "IsActive");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MpesaTokens");
        }
    }
}
