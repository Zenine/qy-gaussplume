using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GnnSimulation.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMeteorologyIsActive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_active",
                table: "meteorology",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "is_active",
                table: "meteorology");
        }
    }
}
