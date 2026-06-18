using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zahy.PartnerPlatform.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class InitialPartnerPlatform : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ZahyPartners",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    LegalName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    TradeName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ContactInfo_ContactName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ContactInfo_Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ContactInfo_Phone = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ContactInfo_AddressLine1 = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ContactInfo_AddressLine2 = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ContactInfo_City = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ContactInfo_Region = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ContactInfo_PostalCode = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ContactInfo_CountryCode = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    BankInfo_BankName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    BankInfo_AccountHolderName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    BankInfo_Iban = table.Column<string>(type: "nvarchar(34)", maxLength: 34, nullable: true),
                    BankInfo_SwiftCode = table.Column<string>(type: "nvarchar(11)", maxLength: 11, nullable: true),
                    PrimaryContactEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    RegistrantName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    RegistrantPhone = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CloseReason = table.Column<int>(type: "int", nullable: true),
                    CloseNotes = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    OpenIddictClientId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
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
                    table.PrimaryKey("PK_ZahyPartners", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ZahyPartnerUsers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IdentityUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Role = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    InvitedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ActivatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_ZahyPartnerUsers", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ZahyPartners_PrimaryContactEmail",
                table: "ZahyPartners",
                column: "PrimaryContactEmail");

            migrationBuilder.CreateIndex(
                name: "IX_ZahyPartners_Status",
                table: "ZahyPartners",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ZahyPartners_Status_PrimaryContactEmail",
                table: "ZahyPartners",
                columns: new[] { "Status", "PrimaryContactEmail" });

            migrationBuilder.CreateIndex(
                name: "IX_ZahyPartnerUsers_PartnerId",
                table: "ZahyPartnerUsers",
                column: "PartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_ZahyPartnerUsers_PartnerId_IdentityUserId",
                table: "ZahyPartnerUsers",
                columns: new[] { "PartnerId", "IdentityUserId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ZahyPartners");

            migrationBuilder.DropTable(
                name: "ZahyPartnerUsers");
        }
    }
}
