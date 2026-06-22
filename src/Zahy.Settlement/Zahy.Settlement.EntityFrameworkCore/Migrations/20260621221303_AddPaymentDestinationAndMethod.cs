using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zahy.Settlement.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentDestinationAndMethod : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "Method",
                table: "StlPayments",
                type: "int",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BankAccountCode",
                table: "StlPayments",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BankAccountCode",
                table: "StlPayments");

            migrationBuilder.AlterColumn<string>(
                name: "Method",
                table: "StlPayments",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);
        }
    }
}
