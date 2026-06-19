using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zahy.Finance.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class InitialFinance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FinAccountPostings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccountKind = table.Column<int>(type: "int", nullable: false),
                    AccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PostingAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    PostedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SourceModule = table.Column<int>(type: "int", nullable: false),
                    SourceType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SourceId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    SourceRowId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    ReversesPostingId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ExtraProperties = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinAccountPostings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FinAccountStatusAudits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccountKind = table.Column<int>(type: "int", nullable: false),
                    AccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FromStatus = table.Column<int>(type: "int", nullable: false),
                    ToStatus = table.Column<int>(type: "int", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TransitionedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinAccountStatusAudits", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FinDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccountKind = table.Column<int>(type: "int", nullable: false),
                    AccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentKind = table.Column<int>(type: "int", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    InvoiceNumber = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    FiscalYear = table.Column<int>(type: "int", nullable: false),
                    SequenceNumber = table.Column<int>(type: "int", nullable: false),
                    PostingSum = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    GeneratedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SourceRowId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ExtraProperties = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinDocuments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FinInvoiceNumberSequences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentKind = table.Column<int>(type: "int", nullable: false),
                    FiscalYear = table.Column<int>(type: "int", nullable: false),
                    LastNumber = table.Column<int>(type: "int", nullable: false),
                    ExtraProperties = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinInvoiceNumberSequences", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FinKycSubmissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EntityKind = table.Column<int>(type: "int", nullable: false),
                    EntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    SubmittedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProtectedLegalNameAr = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    ProtectedLegalNameEn = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    ProtectedCommercialRegistrationNumber = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    ProtectedVatNumber = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    ProtectedIban = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    ProtectedLegalAddress = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    ExtraProperties = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinKycSubmissions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FinKycVerifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EntityKind = table.Column<int>(type: "int", nullable: false),
                    EntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    KycSubmissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ReviewerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    VerifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    VerifiedLegalNameAr = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    VerifiedLegalNameEn = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    VerifiedCommercialRegistrationNumber = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    VerifiedVatNumber = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    VerifiedIban = table.Column<string>(type: "nvarchar(34)", maxLength: 34, nullable: true),
                    VerifiedLegalAddress = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    ExtraProperties = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinKycVerifications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FinMerchantAccounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    KycVerificationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OpenedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    InvoiceGenerationModeOverride = table.Column<int>(type: "int", nullable: true),
                    ExtraProperties = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinMerchantAccounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FinPartnerAccounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    KycVerificationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OpenedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    InvoiceGenerationModeOverride = table.Column<int>(type: "int", nullable: true),
                    ExtraProperties = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinPartnerAccounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FinPlatformSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DefaultInvoiceGenerationMode = table.Column<int>(type: "int", nullable: false),
                    ExtraProperties = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinPlatformSettings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FinAccountPostings_AccountKind_AccountId",
                table: "FinAccountPostings",
                columns: new[] { "AccountKind", "AccountId" });

            migrationBuilder.CreateIndex(
                name: "IX_FinAccountPostings_IdempotencyKey",
                table: "FinAccountPostings",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FinAccountStatusAudits_AccountKind_AccountId",
                table: "FinAccountStatusAudits",
                columns: new[] { "AccountKind", "AccountId" });

            migrationBuilder.CreateIndex(
                name: "IX_FinDocuments_AccountKind_AccountId",
                table: "FinDocuments",
                columns: new[] { "AccountKind", "AccountId" });

            migrationBuilder.CreateIndex(
                name: "IX_FinDocuments_IdempotencyKey",
                table: "FinDocuments",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FinInvoiceNumberSequences_DocumentKind_FiscalYear",
                table: "FinInvoiceNumberSequences",
                columns: new[] { "DocumentKind", "FiscalYear" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FinKycSubmissions_EntityKind_EntityId",
                table: "FinKycSubmissions",
                columns: new[] { "EntityKind", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_FinKycSubmissions_EntityKind_EntityId_Version",
                table: "FinKycSubmissions",
                columns: new[] { "EntityKind", "EntityId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FinKycVerifications_EntityKind_EntityId_Status",
                table: "FinKycVerifications",
                columns: new[] { "EntityKind", "EntityId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_FinKycVerifications_KycSubmissionId",
                table: "FinKycVerifications",
                column: "KycSubmissionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FinMerchantAccounts_TenantId",
                table: "FinMerchantAccounts",
                column: "TenantId",
                unique: true,
                filter: "[TenantId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_FinPartnerAccounts_PartnerId",
                table: "FinPartnerAccounts",
                column: "PartnerId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FinAccountPostings");

            migrationBuilder.DropTable(
                name: "FinAccountStatusAudits");

            migrationBuilder.DropTable(
                name: "FinDocuments");

            migrationBuilder.DropTable(
                name: "FinInvoiceNumberSequences");

            migrationBuilder.DropTable(
                name: "FinKycSubmissions");

            migrationBuilder.DropTable(
                name: "FinKycVerifications");

            migrationBuilder.DropTable(
                name: "FinMerchantAccounts");

            migrationBuilder.DropTable(
                name: "FinPartnerAccounts");

            migrationBuilder.DropTable(
                name: "FinPlatformSettings");
        }
    }
}
