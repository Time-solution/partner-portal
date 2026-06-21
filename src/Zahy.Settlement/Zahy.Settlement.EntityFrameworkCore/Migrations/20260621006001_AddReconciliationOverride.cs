using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zahy.Settlement.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class AddReconciliationOverride : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Loud audit of an override commit (committing over an Exception with a mandatory note).
            // Both nullable — only set on ReconciledWithOverride. No money, no journal.
            migrationBuilder.AddColumn<string>(
                name: "OverrideBy",
                table: "StlReconciliationBatches",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OverrideReason",
                table: "StlReconciliationBatches",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "OverrideBy", table: "StlReconciliationBatches");
            migrationBuilder.DropColumn(name: "OverrideReason", table: "StlReconciliationBatches");
        }
    }
}
