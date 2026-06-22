using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zahy.Settlement.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class AddPartnerLedgerAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StlPartnerLedgerAccounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PartnerName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    PayableCode = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ReceivableCode = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_StlPartnerLedgerAccounts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StlPartnerLedgerAccounts_PartnerId",
                table: "StlPartnerLedgerAccounts",
                column: "PartnerId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StlPartnerLedgerAccounts_PayableCode",
                table: "StlPartnerLedgerAccounts",
                column: "PayableCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StlPartnerLedgerAccounts_ReceivableCode",
                table: "StlPartnerLedgerAccounts",
                column: "ReceivableCode",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StlPartnerLedgerAccounts");
        }
    }
}
