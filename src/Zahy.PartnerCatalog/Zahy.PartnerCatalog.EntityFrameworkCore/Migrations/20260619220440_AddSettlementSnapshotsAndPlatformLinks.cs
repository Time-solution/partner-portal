using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zahy.PartnerCatalog.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class AddSettlementSnapshotsAndPlatformLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PcatPlatformCatalogLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PartnerCatalogItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    PlatformProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PlatformVariantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastSyncAttemptAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastSyncError = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    Shape2HandshakeVersion = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PcatPlatformCatalogLinks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PcatSettlementCostMarkupSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MerchantActivationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PartnerCatalogItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BuyPriceAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    BuyPriceCurrency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    BuyPriceVatInclusive = table.Column<bool>(type: "bit", nullable: false),
                    SellPriceAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    SellPriceCurrency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    SellPriceVatInclusive = table.Column<bool>(type: "bit", nullable: false),
                    SellPriceSource = table.Column<int>(type: "int", nullable: false),
                    SettlementBook = table.Column<int>(type: "int", nullable: false),
                    VatTreatment = table.Column<int>(type: "int", nullable: false),
                    Trigger = table.Column<int>(type: "int", nullable: false),
                    ExternalTransactionId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    OrderLineId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false, defaultValue: ""),
                    SettlementCaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PcatSettlementCostMarkupSnapshots", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PcatPlatformCatalogLinks_PartnerCatalogItemId",
                table: "PcatPlatformCatalogLinks",
                column: "PartnerCatalogItemId");

            migrationBuilder.CreateIndex(
                name: "IX_PcatPlatformCatalogLinks_TenantId",
                table: "PcatPlatformCatalogLinks",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PcatSettlementCostMarkupSnapshots_ExternalTransactionId_OrderLineId",
                table: "PcatSettlementCostMarkupSnapshots",
                columns: new[] { "ExternalTransactionId", "OrderLineId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PcatSettlementCostMarkupSnapshots_MerchantActivationId",
                table: "PcatSettlementCostMarkupSnapshots",
                column: "MerchantActivationId");

            migrationBuilder.CreateIndex(
                name: "IX_PcatSettlementCostMarkupSnapshots_PartnerId",
                table: "PcatSettlementCostMarkupSnapshots",
                column: "PartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_PcatSettlementCostMarkupSnapshots_TenantId",
                table: "PcatSettlementCostMarkupSnapshots",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PcatPlatformCatalogLinks");

            migrationBuilder.DropTable(
                name: "PcatSettlementCostMarkupSnapshots");
        }
    }
}
