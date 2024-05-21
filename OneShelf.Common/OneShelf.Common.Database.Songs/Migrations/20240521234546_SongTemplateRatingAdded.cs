using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneShelf.Common.Database.Songs.Migrations
{
    /// <inheritdoc />
    public partial class SongTemplateRatingAdded : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<float>(
                name: "TemplateRating",
                table: "Songs",
                type: "real",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TemplateRating",
                table: "Songs");
        }
    }
}
