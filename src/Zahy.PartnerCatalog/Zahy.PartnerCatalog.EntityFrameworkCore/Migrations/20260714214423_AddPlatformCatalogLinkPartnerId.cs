using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zahy.PartnerCatalog.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class AddPlatformCatalogLinkPartnerId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PartnerId",
                table: "PcatPlatformCatalogLinks",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            // P12 backfill — denormalize PartnerId from the source catalog item for any pre-existing
            // links. Correlated subquery on purpose: portable across SQL Server AND SQLite (no
            // UPDATE...FROM join syntax). Rows whose item vanished keep the empty-guid default.
            migrationBuilder.Sql(
                @"UPDATE PcatPlatformCatalogLinks
SET PartnerId = (
    SELECT i.PartnerId
    FROM PcatPartnerCatalogItems i
    WHERE i.Id = PcatPlatformCatalogLinks.PartnerCatalogItemId
)
WHERE EXISTS (
    SELECT 1
    FROM PcatPartnerCatalogItems i
    WHERE i.Id = PcatPlatformCatalogLinks.PartnerCatalogItemId
)");

            migrationBuilder.CreateIndex(
                name: "IX_PcatPlatformCatalogLinks_PartnerId",
                table: "PcatPlatformCatalogLinks",
                column: "PartnerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PcatPlatformCatalogLinks_PartnerId",
                table: "PcatPlatformCatalogLinks");

            migrationBuilder.DropColumn(
                name: "PartnerId",
                table: "PcatPlatformCatalogLinks");
        }
    }
}
