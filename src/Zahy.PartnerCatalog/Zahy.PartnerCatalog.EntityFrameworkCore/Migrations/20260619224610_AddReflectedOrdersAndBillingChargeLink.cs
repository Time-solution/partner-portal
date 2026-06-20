using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zahy.PartnerCatalog.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class AddReflectedOrdersAndBillingChargeLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "BillingChargeId",
                table: "PcatSettlementCostMarkupSnapshots",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PcatReflectedPartnerOrders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MerchantActivationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PartnerCatalogItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SettlementCostMarkupSnapshotId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExternalTransactionId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    OrderLineId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false, defaultValue: ""),
                    ReflectedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PcatReflectedPartnerOrders", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PcatReflectedPartnerOrders_ExternalTransactionId_OrderLineId",
                table: "PcatReflectedPartnerOrders",
                columns: new[] { "ExternalTransactionId", "OrderLineId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PcatReflectedPartnerOrders_MerchantActivationId",
                table: "PcatReflectedPartnerOrders",
                column: "MerchantActivationId");

            migrationBuilder.CreateIndex(
                name: "IX_PcatReflectedPartnerOrders_PartnerId",
                table: "PcatReflectedPartnerOrders",
                column: "PartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_PcatReflectedPartnerOrders_SettlementCostMarkupSnapshotId",
                table: "PcatReflectedPartnerOrders",
                column: "SettlementCostMarkupSnapshotId");

            migrationBuilder.CreateIndex(
                name: "IX_PcatReflectedPartnerOrders_TenantId",
                table: "PcatReflectedPartnerOrders",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PcatReflectedPartnerOrders");

            migrationBuilder.DropColumn(
                name: "BillingChargeId",
                table: "PcatSettlementCostMarkupSnapshots");
        }
    }
}
