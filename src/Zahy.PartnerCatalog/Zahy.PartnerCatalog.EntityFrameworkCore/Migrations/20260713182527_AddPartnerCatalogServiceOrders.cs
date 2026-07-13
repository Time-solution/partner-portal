using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zahy.PartnerCatalog.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class AddPartnerCatalogServiceOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PcatServiceOrders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PartnerCatalogItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OfferingNameSnapshot = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ParticipationModeSnapshot = table.Column<int>(type: "int", nullable: false),
                    BuySnapshotAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    SellSnapshotAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    FeeSnapshotAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    PriceVatInclusive = table.Column<bool>(type: "bit", nullable: false),
                    RevisionCount = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DeliveredAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExtraProperties = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PcatServiceOrders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PcatServiceOrderAnswers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderIndex = table.Column<int>(type: "int", nullable: false),
                    RequirementTitleSnapshot = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    RequirementType = table.Column<int>(type: "int", nullable: false),
                    AnswerText = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    ServiceOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PcatServiceOrderAnswers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PcatServiceOrderAnswers_PcatServiceOrders_ServiceOrderId",
                        column: x => x.ServiceOrderId,
                        principalTable: "PcatServiceOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PcatServiceOrderHistory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderIndex = table.Column<int>(type: "int", nullable: false),
                    Action = table.Column<int>(type: "int", nullable: false),
                    FromStatus = table.Column<int>(type: "int", nullable: true),
                    ToStatus = table.Column<int>(type: "int", nullable: false),
                    Actor = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    At = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ServiceOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PcatServiceOrderHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PcatServiceOrderHistory_PcatServiceOrders_ServiceOrderId",
                        column: x => x.ServiceOrderId,
                        principalTable: "PcatServiceOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PcatServiceOrderMilestones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderIndex = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ServiceOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PcatServiceOrderMilestones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PcatServiceOrderMilestones_PcatServiceOrders_ServiceOrderId",
                        column: x => x.ServiceOrderId,
                        principalTable: "PcatServiceOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PcatServiceOrderAnswers_ServiceOrderId",
                table: "PcatServiceOrderAnswers",
                column: "ServiceOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_PcatServiceOrderHistory_ServiceOrderId",
                table: "PcatServiceOrderHistory",
                column: "ServiceOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_PcatServiceOrderMilestones_ServiceOrderId",
                table: "PcatServiceOrderMilestones",
                column: "ServiceOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_PcatServiceOrders_PartnerId",
                table: "PcatServiceOrders",
                column: "PartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_PcatServiceOrders_Status",
                table: "PcatServiceOrders",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_PcatServiceOrders_TenantId",
                table: "PcatServiceOrders",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PcatServiceOrders_TenantId_PartnerCatalogItemId",
                table: "PcatServiceOrders",
                columns: new[] { "TenantId", "PartnerCatalogItemId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PcatServiceOrderAnswers");

            migrationBuilder.DropTable(
                name: "PcatServiceOrderHistory");

            migrationBuilder.DropTable(
                name: "PcatServiceOrderMilestones");

            migrationBuilder.DropTable(
                name: "PcatServiceOrders");
        }
    }
}
