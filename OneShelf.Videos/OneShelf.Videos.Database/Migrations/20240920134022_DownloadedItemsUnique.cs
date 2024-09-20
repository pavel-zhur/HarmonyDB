using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneShelf.Videos.Database.Migrations.VideosDatabaseMigrations
{
    /// <inheritdoc />
    public partial class DownloadedItemsUnique : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "FileName",
                table: "DownloadedItems",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.CreateIndex(
                name: "IX_DownloadedItems_FileName",
                table: "DownloadedItems",
                column: "FileName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DownloadedItems_LiveMediaId",
                table: "DownloadedItems",
                column: "LiveMediaId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DownloadedItems_FileName",
                table: "DownloadedItems");

            migrationBuilder.DropIndex(
                name: "IX_DownloadedItems_LiveMediaId",
                table: "DownloadedItems");

            migrationBuilder.AlterColumn<string>(
                name: "FileName",
                table: "DownloadedItems",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");
        }
    }
}
