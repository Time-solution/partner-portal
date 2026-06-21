using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zahy.Settlement.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class AddReflectionLogDisplayFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // DISPLAY-ONLY breakdown columns (all nullable, no financial impact, post no journal).
            migrationBuilder.AddColumn<decimal>(
                name: "MenuPrice",
                table: "StlReflectionLogs",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PartnerListPrice",
                table: "StlReflectionLogs",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DeliveryFee",
                table: "StlReflectionLogs",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CustomerPaid",
                table: "StlReflectionLogs",
                type: "decimal(18,2)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "MenuPrice", table: "StlReflectionLogs");
            migrationBuilder.DropColumn(name: "PartnerListPrice", table: "StlReflectionLogs");
            migrationBuilder.DropColumn(name: "DeliveryFee", table: "StlReflectionLogs");
            migrationBuilder.DropColumn(name: "CustomerPaid", table: "StlReflectionLogs");
        }
    }
}
