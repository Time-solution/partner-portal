using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zahy.Connectors.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class InitialConnectors : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConnBranchMappings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConnectorCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ExternalOutletId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    InternalOutletId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_ConnBranchMappings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ConnRegistrations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConnectorCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ConnectorKind = table.Column<int>(type: "int", nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ConfigJson = table.Column<string>(type: "nvarchar(max)", maxLength: 4096, nullable: true),
                    SecretReference = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
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
                    table.PrimaryKey("PK_ConnRegistrations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConnBranchMappings_PartnerId",
                table: "ConnBranchMappings",
                column: "PartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_ConnBranchMappings_PartnerId_TenantId_ConnectorCode_ExternalOutletId",
                table: "ConnBranchMappings",
                columns: new[] { "PartnerId", "TenantId", "ConnectorCode", "ExternalOutletId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConnRegistrations_PartnerId",
                table: "ConnRegistrations",
                column: "PartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_ConnRegistrations_PartnerId_TenantId_ConnectorCode",
                table: "ConnRegistrations",
                columns: new[] { "PartnerId", "TenantId", "ConnectorCode" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConnBranchMappings");

            migrationBuilder.DropTable(
                name: "ConnRegistrations");
        }
    }
}
