using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zahy.PartnerCatalog.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class AddConsignmentOwnershipMode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ConsignmentOwnershipMode",
                table: "PcatPartnerCatalogItems",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConsignmentOwnershipMode",
                table: "PcatPartnerCatalogItems");
        }
    }
}
