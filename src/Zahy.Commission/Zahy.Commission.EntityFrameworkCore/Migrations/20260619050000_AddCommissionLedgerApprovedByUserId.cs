using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zahy.Commission.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class AddCommissionLedgerApprovedByUserId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ApprovedByUserId",
                table: "ComLedgerEntries",
                type: "uniqueidentifier",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApprovedByUserId",
                table: "ComLedgerEntries");
        }
    }
}
