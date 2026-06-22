using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zahy.PartnerCatalog.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class AddUsagePackageSelections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PcatUsagePackageSelections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UsagePackageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MerchantName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ActivatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExtraProperties = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PcatUsagePackageSelections", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PcatUsagePackageSelections_PartnerId",
                table: "PcatUsagePackageSelections",
                column: "PartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_PcatUsagePackageSelections_TenantId",
                table: "PcatUsagePackageSelections",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PcatUsagePackageSelections_TenantId_PartnerId",
                table: "PcatUsagePackageSelections",
                columns: new[] { "TenantId", "PartnerId" });

            migrationBuilder.CreateIndex(
                name: "IX_PcatUsagePackageSelections_TenantId_UsagePackageId",
                table: "PcatUsagePackageSelections",
                columns: new[] { "TenantId", "UsagePackageId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PcatUsagePackageSelections");
        }
    }
}
