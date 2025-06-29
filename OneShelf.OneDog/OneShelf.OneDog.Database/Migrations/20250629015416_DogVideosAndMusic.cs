using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneShelf.OneDog.Database.Migrations
{
    /// <inheritdoc />
    public partial class DogVideosAndMusic : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LyriaModel",
                table: "Domains",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MusicLimit_Limit",
                table: "Domains",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "MusicLimit_Window",
                table: "Domains",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SoraModel",
                table: "Domains",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "VideosLimit_Limit",
                table: "Domains",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "VideosLimit_Window",
                table: "Domains",
                type: "bigint",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LyriaModel",
                table: "Domains");

            migrationBuilder.DropColumn(
                name: "MusicLimit_Limit",
                table: "Domains");

            migrationBuilder.DropColumn(
                name: "MusicLimit_Window",
                table: "Domains");

            migrationBuilder.DropColumn(
                name: "SoraModel",
                table: "Domains");

            migrationBuilder.DropColumn(
                name: "VideosLimit_Limit",
                table: "Domains");

            migrationBuilder.DropColumn(
                name: "VideosLimit_Window",
                table: "Domains");
        }
    }
}
