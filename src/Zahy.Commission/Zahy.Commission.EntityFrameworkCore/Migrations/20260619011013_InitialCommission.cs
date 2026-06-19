using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zahy.Commission.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class InitialCommission : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ComBillingCharges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    PeriodKey = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    CommissionLedgerEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    ChargedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExtraProperties = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComBillingCharges", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ComLedgerEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RuleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SourceId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    OrderRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    BasisAmount = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    ComputedCommission = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Direction = table.Column<int>(type: "int", nullable: false),
                    EntryKind = table.Column<int>(type: "int", nullable: false),
                    ReversesEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PaidAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExtraProperties = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComLedgerEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ComPartnerBillingProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActivationFeeAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    MonthlySubscriptionAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
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
                    table.PrimaryKey("PK_ComPartnerBillingProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ComRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    Direction = table.Column<int>(type: "int", nullable: false),
                    TriggerType = table.Column<int>(type: "int", nullable: false),
                    FeeType = table.Column<int>(type: "int", nullable: false),
                    BasisAmountKind = table.Column<int>(type: "int", nullable: false),
                    ScopeKind = table.Column<int>(type: "int", nullable: false),
                    ScopePartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ScopePartnerType = table.Column<int>(type: "int", nullable: true),
                    ScopeCategoryCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ScopeProductSku = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    BasisDefinitionJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EffectiveFromUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EffectiveToUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Priority = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_ComRules", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ComBillingCharges_CommissionLedgerEntryId",
                table: "ComBillingCharges",
                column: "CommissionLedgerEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_ComBillingCharges_IdempotencyKey",
                table: "ComBillingCharges",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ComBillingCharges_PartnerId",
                table: "ComBillingCharges",
                column: "PartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_ComLedgerEntries_IdempotencyKey",
                table: "ComLedgerEntries",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ComLedgerEntries_PartnerId",
                table: "ComLedgerEntries",
                column: "PartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_ComLedgerEntries_ReversesEntryId",
                table: "ComLedgerEntries",
                column: "ReversesEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_ComLedgerEntries_SourceType_SourceId_RuleId",
                table: "ComLedgerEntries",
                columns: new[] { "SourceType", "SourceId", "RuleId" });

            migrationBuilder.CreateIndex(
                name: "IX_ComPartnerBillingProfiles_PartnerId",
                table: "ComPartnerBillingProfiles",
                column: "PartnerId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ComRules_ScopeKind_ScopePartnerId_FeeType_IsEnabled",
                table: "ComRules",
                columns: new[] { "ScopeKind", "ScopePartnerId", "FeeType", "IsEnabled" });

            migrationBuilder.CreateIndex(
                name: "IX_ComRules_ScopeKind_ScopePartnerType_FeeType_IsEnabled",
                table: "ComRules",
                columns: new[] { "ScopeKind", "ScopePartnerType", "FeeType", "IsEnabled" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ComBillingCharges");

            migrationBuilder.DropTable(
                name: "ComLedgerEntries");

            migrationBuilder.DropTable(
                name: "ComPartnerBillingProfiles");

            migrationBuilder.DropTable(
                name: "ComRules");
        }
    }
}
