using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace REIGN.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBusinessAIProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Greeting",
                table: "Businesses",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Instructions",
                table: "Businesses",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Personality",
                table: "Businesses",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Tone",
                table: "Businesses",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Greeting",
                table: "Businesses");

            migrationBuilder.DropColumn(
                name: "Instructions",
                table: "Businesses");

            migrationBuilder.DropColumn(
                name: "Personality",
                table: "Businesses");

            migrationBuilder.DropColumn(
                name: "Tone",
                table: "Businesses");
        }
    }
}
