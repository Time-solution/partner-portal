using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zahy.Settlement.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class AddAggregatorStatementReconciliation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StlAggregatorStatementExceptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StatementId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StatementLineId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Type = table.Column<int>(type: "int", nullable: false),
                    ExternalOrderRef = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ExpectedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ActualAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Details = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    Resolved = table.Column<bool>(type: "bit", nullable: false),
                    ResolutionNote = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    ResolvedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StlAggregatorStatementExceptions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StlAggregatorStatementLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StatementId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExternalOrderRef = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    OrderDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Gross = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AggregatorFee = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Net = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StlAggregatorStatementLines", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StlAggregatorStatements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Source = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    PeriodFrom = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PeriodTo = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ImportIdempotencyKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    DeclaredGross = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DeclaredFees = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DeclaredNet = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ImportedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ImportedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    MatchedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClosedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ExtraProperties = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StlAggregatorStatements", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StlAggregatorStatementExceptions_StatementId",
                table: "StlAggregatorStatementExceptions",
                column: "StatementId");

            migrationBuilder.CreateIndex(
                name: "IX_StlAggregatorStatementLines_StatementId_ExternalOrderRef",
                table: "StlAggregatorStatementLines",
                columns: new[] { "StatementId", "ExternalOrderRef" });

            migrationBuilder.CreateIndex(
                name: "IX_StlAggregatorStatements_ImportIdempotencyKey",
                table: "StlAggregatorStatements",
                column: "ImportIdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StlAggregatorStatements_PartnerId_PeriodFrom_PeriodTo",
                table: "StlAggregatorStatements",
                columns: new[] { "PartnerId", "PeriodFrom", "PeriodTo" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StlAggregatorStatementExceptions");

            migrationBuilder.DropTable(
                name: "StlAggregatorStatementLines");

            migrationBuilder.DropTable(
                name: "StlAggregatorStatements");
        }
    }
}
