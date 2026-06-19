using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zahy.Commission.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class AddBillingChargeTarget : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ChargeTarget",
                table: "ComBillingCharges",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ChargeTarget",
                table: "ComBillingCharges");
        }
    }
}
