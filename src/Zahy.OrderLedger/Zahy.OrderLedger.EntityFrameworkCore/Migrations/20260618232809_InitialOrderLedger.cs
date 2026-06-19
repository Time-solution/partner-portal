using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zahy.OrderLedger.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class InitialOrderLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OlgOrderRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SourceSystem = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SourceOrderId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    SourceVersion = table.Column<long>(type: "bigint", nullable: false),
                    Direction = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    PaymentStatus = table.Column<int>(type: "int", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CapturedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SourceTimestamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExtraProperties = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OlgOrderRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OlgOrderLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LineNumber = table.Column<int>(type: "int", nullable: false),
                    Sku = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ProductName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    LineTotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OlgOrderLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OlgOrderLines_OlgOrderRecords_OrderRecordId",
                        column: x => x.OrderRecordId,
                        principalTable: "OlgOrderRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OlgOrderLines_OrderRecordId",
                table: "OlgOrderLines",
                column: "OrderRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_OlgOrderRecords_PartnerId",
                table: "OlgOrderRecords",
                column: "PartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_OlgOrderRecords_SourceSystem_SourceOrderId_SourceVersion",
                table: "OlgOrderRecords",
                columns: new[] { "SourceSystem", "SourceOrderId", "SourceVersion" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OlgOrderRecords_TenantId",
                table: "OlgOrderRecords",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OlgOrderLines");

            migrationBuilder.DropTable(
                name: "OlgOrderRecords");
        }
    }
}
