using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zahy.PartnerCatalog.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class AddMerchantActivations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SettlementParticipationMode",
                table: "PcatPartnerCatalogItems",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "PcatMerchantActivations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PartnerCatalogItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResalePriceAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ResalePriceCurrency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    ResalePriceVatInclusive = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ActivatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EndedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ExternalReference = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
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
                    table.PrimaryKey("PK_PcatMerchantActivations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PcatMerchantActivations_IdempotencyKey",
                table: "PcatMerchantActivations",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PcatMerchantActivations_PartnerId",
                table: "PcatMerchantActivations",
                column: "PartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_PcatMerchantActivations_TenantId",
                table: "PcatMerchantActivations",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PcatMerchantActivations_TenantId_PartnerCatalogItemId",
                table: "PcatMerchantActivations",
                columns: new[] { "TenantId", "PartnerCatalogItemId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PcatMerchantActivations");

            migrationBuilder.DropColumn(
                name: "SettlementParticipationMode",
                table: "PcatPartnerCatalogItems");
        }
    }
}
