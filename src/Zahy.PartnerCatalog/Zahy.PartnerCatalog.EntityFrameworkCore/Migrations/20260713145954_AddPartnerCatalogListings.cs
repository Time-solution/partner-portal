using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zahy.PartnerCatalog.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class AddPartnerCatalogListings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PcatListings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PartnerCatalogItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExtraProperties = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PcatListings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PcatListingDeliverables",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderIndex = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    ListingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PcatListingDeliverables", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PcatListingDeliverables_PcatListings_ListingId",
                        column: x => x.ListingId,
                        principalTable: "PcatListings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PcatListingExecutionSteps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderIndex = table.Column<int>(type: "int", nullable: false),
                    Text = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    ListingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PcatListingExecutionSteps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PcatListingExecutionSteps_PcatListings_ListingId",
                        column: x => x.ListingId,
                        principalTable: "PcatListings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PcatListingFaqs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderIndex = table.Column<int>(type: "int", nullable: false),
                    Question = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Answer = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ListingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PcatListingFaqs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PcatListingFaqs_PcatListings_ListingId",
                        column: x => x.ListingId,
                        principalTable: "PcatListings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PcatListingRequirements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderIndex = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    ChoicesJson = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    ListingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PcatListingRequirements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PcatListingRequirements_PcatListings_ListingId",
                        column: x => x.ListingId,
                        principalTable: "PcatListings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PcatListingTerms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderIndex = table.Column<int>(type: "int", nullable: false),
                    Text = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    ListingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PcatListingTerms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PcatListingTerms_PcatListings_ListingId",
                        column: x => x.ListingId,
                        principalTable: "PcatListings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PcatListingDeliverables_ListingId",
                table: "PcatListingDeliverables",
                column: "ListingId");

            migrationBuilder.CreateIndex(
                name: "IX_PcatListingExecutionSteps_ListingId",
                table: "PcatListingExecutionSteps",
                column: "ListingId");

            migrationBuilder.CreateIndex(
                name: "IX_PcatListingFaqs_ListingId",
                table: "PcatListingFaqs",
                column: "ListingId");

            migrationBuilder.CreateIndex(
                name: "IX_PcatListingRequirements_ListingId",
                table: "PcatListingRequirements",
                column: "ListingId");

            migrationBuilder.CreateIndex(
                name: "IX_PcatListings_PartnerCatalogItemId",
                table: "PcatListings",
                column: "PartnerCatalogItemId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PcatListingTerms_ListingId",
                table: "PcatListingTerms",
                column: "ListingId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PcatListingDeliverables");

            migrationBuilder.DropTable(
                name: "PcatListingExecutionSteps");

            migrationBuilder.DropTable(
                name: "PcatListingFaqs");

            migrationBuilder.DropTable(
                name: "PcatListingRequirements");

            migrationBuilder.DropTable(
                name: "PcatListingTerms");

            migrationBuilder.DropTable(
                name: "PcatListings");
        }
    }
}
