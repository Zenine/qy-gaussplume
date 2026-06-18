using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GnnSimulation.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFixedRegions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "regions",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    key = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    sort_order = table.Column<int>(type: "INTEGER", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_regions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "region_meteorology",
                columns: table => new
                {
                    region_id = table.Column<int>(type: "INTEGER", nullable: false),
                    meteorology_id = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_region_meteorology", x => new { x.region_id, x.meteorology_id });
                    table.ForeignKey(
                        name: "FK_region_meteorology_meteorology_meteorology_id",
                        column: x => x.meteorology_id,
                        principalTable: "meteorology",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_region_meteorology_regions_region_id",
                        column: x => x.region_id,
                        principalTable: "regions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "region_receptors",
                columns: table => new
                {
                    region_id = table.Column<int>(type: "INTEGER", nullable: false),
                    receptor_id = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_region_receptors", x => new { x.region_id, x.receptor_id });
                    table.ForeignKey(
                        name: "FK_region_receptors_receptors_receptor_id",
                        column: x => x.receptor_id,
                        principalTable: "receptors",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_region_receptors_regions_region_id",
                        column: x => x.region_id,
                        principalTable: "regions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "region_sources",
                columns: table => new
                {
                    region_id = table.Column<int>(type: "INTEGER", nullable: false),
                    source_id = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_region_sources", x => new { x.region_id, x.source_id });
                    table.ForeignKey(
                        name: "FK_region_sources_emission_sources_source_id",
                        column: x => x.source_id,
                        principalTable: "emission_sources",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_region_sources_regions_region_id",
                        column: x => x.region_id,
                        principalTable: "regions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_region_meteorology_meteorology_id",
                table: "region_meteorology",
                column: "meteorology_id");

            migrationBuilder.CreateIndex(
                name: "IX_region_receptors_receptor_id",
                table: "region_receptors",
                column: "receptor_id");

            migrationBuilder.CreateIndex(
                name: "IX_region_sources_source_id",
                table: "region_sources",
                column: "source_id");

            migrationBuilder.CreateIndex(
                name: "IX_regions_key",
                table: "regions",
                column: "key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "region_meteorology");

            migrationBuilder.DropTable(
                name: "region_receptors");

            migrationBuilder.DropTable(
                name: "region_sources");

            migrationBuilder.DropTable(
                name: "regions");
        }
    }
}
