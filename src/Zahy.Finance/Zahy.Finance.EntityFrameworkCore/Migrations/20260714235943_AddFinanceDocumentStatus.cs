using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zahy.Finance.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class AddFinanceDocumentStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "FinDocuments",
                type: "int",
                nullable: false,
                defaultValue: 2);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Status",
                table: "FinDocuments");
        }
    }
}
