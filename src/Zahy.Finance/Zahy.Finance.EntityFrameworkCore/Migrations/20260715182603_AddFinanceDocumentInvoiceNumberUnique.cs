using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zahy.Finance.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class AddFinanceDocumentInvoiceNumberUnique : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_FinDocuments_InvoiceNumber",
                table: "FinDocuments",
                column: "InvoiceNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FinDocuments_InvoiceNumber",
                table: "FinDocuments");
        }
    }
}
