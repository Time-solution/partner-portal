using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zahy.Settlement.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class AddSettlementWebhookEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StlSettlementWebhookEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Book = table.Column<int>(type: "int", nullable: true),
                    PartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ExternalEventId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    SignatureStatus = table.Column<int>(type: "int", nullable: false),
                    Outcome = table.Column<int>(type: "int", nullable: false),
                    SettlementCaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ResultingState = table.Column<int>(type: "int", nullable: true),
                    RawPayload = table.Column<string>(type: "nvarchar(max)", maxLength: 16384, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    AllocationSnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReceivedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExtraProperties = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StlSettlementWebhookEvents", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StlSettlementWebhookEvents_Book_ExternalEventId",
                table: "StlSettlementWebhookEvents",
                columns: new[] { "Book", "ExternalEventId" });

            migrationBuilder.CreateIndex(
                name: "IX_StlSettlementWebhookEvents_SettlementCaseId",
                table: "StlSettlementWebhookEvents",
                column: "SettlementCaseId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StlSettlementWebhookEvents");
        }
    }
}
