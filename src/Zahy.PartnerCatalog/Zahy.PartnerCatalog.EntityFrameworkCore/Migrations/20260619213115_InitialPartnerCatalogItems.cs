using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zahy.PartnerCatalog.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class InitialPartnerCatalogItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PcatPartnerCatalogItemReflections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PartnerCatalogItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VisibleFrom = table.Column<DateTime>(type: "datetime2", nullable: false),
                    VisibleTo = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsPublished = table.Column<bool>(type: "bit", nullable: false),
                    Audience = table.Column<int>(type: "int", nullable: false),
                    PlatformVariantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PcatPartnerCatalogItemReflections", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PcatPartnerCatalogItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    OfferingKind = table.Column<int>(type: "int", nullable: false),
                    PartnerCostAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PartnerCostCurrency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    PartnerCostVatInclusive = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    SettlementBookOverride = table.Column<int>(type: "int", nullable: true),
                    SettlementTriggerMode = table.Column<int>(type: "int", nullable: false),
                    DefaultVatTreatment = table.Column<int>(type: "int", nullable: false),
                    ArchivedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CarrierServiceCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    FulfilmentUnit = table.Column<int>(type: "int", nullable: true),
                    ExternalMenuItemId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    MenuCategoryCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RequiresPlatformCatalogSync = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_PcatPartnerCatalogItems", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PcatPartnerCatalogItemReflections_PartnerCatalogItemId",
                table: "PcatPartnerCatalogItemReflections",
                column: "PartnerCatalogItemId");

            migrationBuilder.CreateIndex(
                name: "IX_PcatPartnerCatalogItemReflections_PartnerId_IsPublished_VisibleFrom",
                table: "PcatPartnerCatalogItemReflections",
                columns: new[] { "PartnerId", "IsPublished", "VisibleFrom" });

            migrationBuilder.CreateIndex(
                name: "IX_PcatPartnerCatalogItems_PartnerId",
                table: "PcatPartnerCatalogItems",
                column: "PartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_PcatPartnerCatalogItems_PartnerId_Code",
                table: "PcatPartnerCatalogItems",
                columns: new[] { "PartnerId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PcatPartnerCatalogItems_PartnerId_Status",
                table: "PcatPartnerCatalogItems",
                columns: new[] { "PartnerId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PcatPartnerCatalogItemReflections");

            migrationBuilder.DropTable(
                name: "PcatPartnerCatalogItems");
        }
    }
}
