using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zahy.Settlement.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class AddActivationFeeConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StlActivationFeeConfigs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActivationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubscriptionEnabled = table.Column<bool>(type: "bit", nullable: false),
                    SubscriptionAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SubscriptionCurrency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    SubscriptionPayer = table.Column<int>(type: "int", nullable: false),
                    PerTransactionEnabled = table.Column<bool>(type: "bit", nullable: false),
                    PerTransactionAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PerTransactionCurrency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    PerTransactionPayer = table.Column<int>(type: "int", nullable: false),
                    ExtraProperties = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StlActivationFeeConfigs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StlActivationFeeConfigs_ActivationId",
                table: "StlActivationFeeConfigs",
                column: "ActivationId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StlActivationFeeConfigs");
        }
    }
}
