using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zahy.OrderLedger.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderMonetarySplit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "DeliveryFee",
                table: "OlgOrderRecords",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "Subtotal",
                table: "OlgOrderRecords",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "SubtotalResolution",
                table: "OlgOrderRecords",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "TaxAmount",
                table: "OlgOrderRecords",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeliveryFee",
                table: "OlgOrderRecords");

            migrationBuilder.DropColumn(
                name: "Subtotal",
                table: "OlgOrderRecords");

            migrationBuilder.DropColumn(
                name: "SubtotalResolution",
                table: "OlgOrderRecords");

            migrationBuilder.DropColumn(
                name: "TaxAmount",
                table: "OlgOrderRecords");
        }
    }
}
