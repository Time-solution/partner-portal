using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zahy.Finance.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class AddManualInvoiceLineItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "FinDocuments",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Recipient",
                table: "FinDocuments",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RecipientReference",
                table: "FinDocuments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RecipientType",
                table: "FinDocuments",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Source",
                table: "FinDocuments",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "FinInvoiceLines",
                columns: table => new
                {
                    LineNo = table.Column<int>(type: "int", nullable: false),
                    FinanceDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    UnitPriceInclusive = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    LineTotalInclusive = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    VatNet = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    VatAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AccountCode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinInvoiceLines", x => new { x.FinanceDocumentId, x.LineNo });
                    table.ForeignKey(
                        name: "FK_FinInvoiceLines_FinDocuments_FinanceDocumentId",
                        column: x => x.FinanceDocumentId,
                        principalTable: "FinDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FinInvoiceLines");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "FinDocuments");

            migrationBuilder.DropColumn(
                name: "Recipient",
                table: "FinDocuments");

            migrationBuilder.DropColumn(
                name: "RecipientReference",
                table: "FinDocuments");

            migrationBuilder.DropColumn(
                name: "RecipientType",
                table: "FinDocuments");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "FinDocuments");
        }
    }
}
