using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zahy.Settlement.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentIdempotencyKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                table: "StlPayments",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_StlPayments_IdempotencyKey",
                table: "StlPayments",
                column: "IdempotencyKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StlPayments_IdempotencyKey",
                table: "StlPayments");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "StlPayments");
        }
    }
}
