using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zahy.Settlement.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class AddSettlementCaseReversalReasonAndActor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Reason",
                table: "StlSettlementCases",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReversedByUserId",
                table: "StlSettlementCases",
                type: "uniqueidentifier",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Reason",
                table: "StlSettlementCases");

            migrationBuilder.DropColumn(
                name: "ReversedByUserId",
                table: "StlSettlementCases");
        }
    }
}
