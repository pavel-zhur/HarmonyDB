using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneShelf.Videos.Database.Migrations.VideosDatabaseMigrations
{
    /// <inheritdoc />
    public partial class Lives3 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "LiveMediaId",
                table: "DownloadedItems",
                newName: "LiveMediaMediaId");

            migrationBuilder.RenameIndex(
                name: "IX_DownloadedItems_LiveMediaId",
                table: "DownloadedItems",
                newName: "IX_DownloadedItems_LiveMediaMediaId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "LiveMediaMediaId",
                table: "DownloadedItems",
                newName: "LiveMediaId");

            migrationBuilder.RenameIndex(
                name: "IX_DownloadedItems_LiveMediaMediaId",
                table: "DownloadedItems",
                newName: "IX_DownloadedItems_LiveMediaId");
        }
    }
}
