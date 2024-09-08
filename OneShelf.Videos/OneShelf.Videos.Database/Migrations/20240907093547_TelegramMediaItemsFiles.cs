using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneShelf.Videos.Database.Migrations.VideosDatabaseMigrations
{
    /// <inheritdoc />
    public partial class TelegramMediaItemsFiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "MediaGroupId",
                table: "TelegramMedia",
                type: "nvarchar(450)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DownloadedFileName",
                table: "TelegramMedia",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DownloadedThumbnailFileName",
                table: "TelegramMedia",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TelegramMedia_MediaGroupId",
                table: "TelegramMedia",
                column: "MediaGroupId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TelegramMedia_MediaGroupId",
                table: "TelegramMedia");

            migrationBuilder.DropColumn(
                name: "DownloadedFileName",
                table: "TelegramMedia");

            migrationBuilder.DropColumn(
                name: "DownloadedThumbnailFileName",
                table: "TelegramMedia");

            migrationBuilder.AlterColumn<string>(
                name: "MediaGroupId",
                table: "TelegramMedia",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);
        }
    }
}
