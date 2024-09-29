using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneShelf.Videos.Database.Migrations.VideosDatabaseMigrations
{
    /// <inheritdoc />
    public partial class Lives4 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_DownloadedItems",
                table: "DownloadedItems");

            migrationBuilder.RenameTable(
                name: "DownloadedItems",
                newName: "LiveDownloadedItems");

            migrationBuilder.RenameIndex(
                name: "IX_DownloadedItems_LiveMediaMediaId",
                table: "LiveDownloadedItems",
                newName: "IX_LiveDownloadedItems_LiveMediaMediaId");

            migrationBuilder.RenameIndex(
                name: "IX_DownloadedItems_FileName",
                table: "LiveDownloadedItems",
                newName: "IX_LiveDownloadedItems_FileName");

            migrationBuilder.AddPrimaryKey(
                name: "PK_LiveDownloadedItems",
                table: "LiveDownloadedItems",
                column: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_LiveDownloadedItems",
                table: "LiveDownloadedItems");

            migrationBuilder.RenameTable(
                name: "LiveDownloadedItems",
                newName: "DownloadedItems");

            migrationBuilder.RenameIndex(
                name: "IX_LiveDownloadedItems_LiveMediaMediaId",
                table: "DownloadedItems",
                newName: "IX_DownloadedItems_LiveMediaMediaId");

            migrationBuilder.RenameIndex(
                name: "IX_LiveDownloadedItems_FileName",
                table: "DownloadedItems",
                newName: "IX_DownloadedItems_FileName");

            migrationBuilder.AddPrimaryKey(
                name: "PK_DownloadedItems",
                table: "DownloadedItems",
                column: "Id");
        }
    }
}
