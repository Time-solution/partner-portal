using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zahy.Webhooks.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class InitialWebhooks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WhkDeadLetters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OutboxMessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", maxLength: 65536, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    FinalAttemptAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReplayedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WhkDeadLetters", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WhkDeliveries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OutboxMessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubscriptionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AttemptNumber = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    HttpStatusCode = table.Column<int>(type: "int", nullable: true),
                    ResponseBodySnippet = table.Column<string>(type: "nvarchar(max)", maxLength: 4096, nullable: true),
                    DurationMs = table.Column<int>(type: "int", nullable: false),
                    ErrorCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    AttemptedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WhkDeliveries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WhkOutboxMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", maxLength: 65536, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ScheduledAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WhkOutboxMessages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WhkSubscriptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TargetUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    SigningSecret = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    EventTypesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FilterRulesJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_WhkSubscriptions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WhkDeadLetters_OutboxMessageId",
                table: "WhkDeadLetters",
                column: "OutboxMessageId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WhkDeadLetters_PartnerId",
                table: "WhkDeadLetters",
                column: "PartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_WhkDeliveries_AttemptedAt",
                table: "WhkDeliveries",
                column: "AttemptedAt");

            migrationBuilder.CreateIndex(
                name: "IX_WhkDeliveries_OutboxMessageId",
                table: "WhkDeliveries",
                column: "OutboxMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_WhkDeliveries_PartnerId",
                table: "WhkDeliveries",
                column: "PartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_WhkOutboxMessages_IdempotencyKey",
                table: "WhkOutboxMessages",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WhkOutboxMessages_PartnerId",
                table: "WhkOutboxMessages",
                column: "PartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_WhkOutboxMessages_Status_ScheduledAt",
                table: "WhkOutboxMessages",
                columns: new[] { "Status", "ScheduledAt" });

            migrationBuilder.CreateIndex(
                name: "IX_WhkSubscriptions_PartnerId",
                table: "WhkSubscriptions",
                column: "PartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_WhkSubscriptions_PartnerId_TargetUrl",
                table: "WhkSubscriptions",
                columns: new[] { "PartnerId", "TargetUrl" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WhkDeadLetters");

            migrationBuilder.DropTable(
                name: "WhkDeliveries");

            migrationBuilder.DropTable(
                name: "WhkOutboxMessages");

            migrationBuilder.DropTable(
                name: "WhkSubscriptions");
        }
    }
}
