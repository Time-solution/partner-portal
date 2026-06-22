using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zahy.PartnerCatalog.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class AddUsagePackageVolumeTiers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TiersJson",
                table: "PcatUsagePackages",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TiersJson",
                table: "PcatUsagePackages");
        }
    }
}
