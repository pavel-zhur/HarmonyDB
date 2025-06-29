using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneShelf.OneDragon.Database.Migrations
{
    /// <inheritdoc />
    public partial class VideosAndSongs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Songs",
                table: "Limits",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Videos",
                table: "Limits",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LyriaModel",
                table: "AiParameters",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "VeoModel",
                table: "AiParameters",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "VeoVersion",
                table: "AiParameters",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Songs",
                table: "Limits");

            migrationBuilder.DropColumn(
                name: "Videos",
                table: "Limits");

            migrationBuilder.DropColumn(
                name: "LyriaModel",
                table: "AiParameters");

            migrationBuilder.DropColumn(
                name: "VeoModel",
                table: "AiParameters");

            migrationBuilder.DropColumn(
                name: "VeoVersion",
                table: "AiParameters");
        }
    }
}
