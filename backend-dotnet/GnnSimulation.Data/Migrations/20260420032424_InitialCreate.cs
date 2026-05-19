using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GnnSimulation.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "emission_sources",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    source_type = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false, defaultValue: "point"),
                    latitude = table.Column<double>(type: "REAL", nullable: false),
                    longitude = table.Column<double>(type: "REAL", nullable: false),
                    height = table.Column<double>(type: "REAL", nullable: false),
                    temperature = table.Column<double>(type: "REAL", nullable: true, defaultValue: 400.0),
                    velocity = table.Column<double>(type: "REAL", nullable: true, defaultValue: 15.0),
                    diameter = table.Column<double>(type: "REAL", nullable: true, defaultValue: 2.0),
                    area_shape = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true, defaultValue: "rectangle"),
                    area_length = table.Column<double>(type: "REAL", nullable: true, defaultValue: 100.0),
                    area_width = table.Column<double>(type: "REAL", nullable: true, defaultValue: 100.0),
                    area_height = table.Column<double>(type: "REAL", nullable: true, defaultValue: 0.0),
                    area_temperature = table.Column<double>(type: "REAL", nullable: true, defaultValue: 300.0),
                    sigma_z0_area = table.Column<double>(type: "REAL", nullable: true),
                    line_type = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true, defaultValue: "straight"),
                    start_lon = table.Column<double>(type: "REAL", nullable: true),
                    start_lat = table.Column<double>(type: "REAL", nullable: true),
                    end_lon = table.Column<double>(type: "REAL", nullable: true),
                    end_lat = table.Column<double>(type: "REAL", nullable: true),
                    line_width = table.Column<double>(type: "REAL", nullable: true, defaultValue: 10.0),
                    line_height = table.Column<double>(type: "REAL", nullable: true, defaultValue: 0.0),
                    line_temperature = table.Column<double>(type: "REAL", nullable: true, defaultValue: 300.0),
                    sigma_z0_line = table.Column<double>(type: "REAL", nullable: true),
                    line_segment_length = table.Column<double>(type: "REAL", nullable: true, defaultValue: 10.0),
                    marker_symbol = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false, defaultValue: "factory"),
                    marker_color = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false, defaultValue: "#FF5722"),
                    is_active = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_emission_sources", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "marker_configs",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    type = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    symbol = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true, defaultValue: "circle"),
                    color = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true, defaultValue: "#FF5722"),
                    size = table.Column<int>(type: "INTEGER", nullable: true, defaultValue: 10),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_marker_configs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "meteorology",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    wind_speed = table.Column<double>(type: "REAL", nullable: false, defaultValue: 2.0),
                    wind_direction = table.Column<double>(type: "REAL", nullable: false, defaultValue: 0.0),
                    boundary_layer_height = table.Column<double>(type: "REAL", nullable: true, defaultValue: 1000.0),
                    stability_class = table.Column<string>(type: "TEXT", maxLength: 1, nullable: true, defaultValue: "D"),
                    temperature = table.Column<double>(type: "REAL", nullable: true, defaultValue: 293.14999999999998),
                    humidity = table.Column<double>(type: "REAL", nullable: true, defaultValue: 50.0),
                    cloud_cover = table.Column<double>(type: "REAL", nullable: true, defaultValue: 0.0),
                    precipitation = table.Column<double>(type: "REAL", nullable: true, defaultValue: 0.0),
                    record_time = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_meteorology", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "receptors",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    latitude = table.Column<double>(type: "REAL", nullable: false),
                    longitude = table.Column<double>(type: "REAL", nullable: false),
                    height = table.Column<double>(type: "REAL", nullable: false),
                    marker_symbol = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false, defaultValue: "monitor"),
                    marker_color = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false, defaultValue: "#2196F3"),
                    is_active = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_receptors", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "pollutant_emissions",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    source_id = table.Column<int>(type: "INTEGER", nullable: false),
                    pollutant_type = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    emission_rate = table.Column<double>(type: "REAL", nullable: false, defaultValue: 0.0),
                    concentration = table.Column<double>(type: "REAL", nullable: true),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pollutant_emissions", x => x.id);
                    table.ForeignKey(
                        name: "FK_pollutant_emissions_emission_sources_source_id",
                        column: x => x.source_id,
                        principalTable: "emission_sources",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_emission_sources_id",
                table: "emission_sources",
                column: "id");

            migrationBuilder.CreateIndex(
                name: "ix_marker_configs_id",
                table: "marker_configs",
                column: "id");

            migrationBuilder.CreateIndex(
                name: "ix_meteorology_id",
                table: "meteorology",
                column: "id");

            migrationBuilder.CreateIndex(
                name: "ix_pollutant_emissions_id",
                table: "pollutant_emissions",
                column: "id");

            migrationBuilder.CreateIndex(
                name: "IX_pollutant_emissions_source_id",
                table: "pollutant_emissions",
                column: "source_id");

            migrationBuilder.CreateIndex(
                name: "ix_receptors_id",
                table: "receptors",
                column: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "marker_configs");

            migrationBuilder.DropTable(
                name: "meteorology");

            migrationBuilder.DropTable(
                name: "pollutant_emissions");

            migrationBuilder.DropTable(
                name: "receptors");

            migrationBuilder.DropTable(
                name: "emission_sources");
        }
    }
}
