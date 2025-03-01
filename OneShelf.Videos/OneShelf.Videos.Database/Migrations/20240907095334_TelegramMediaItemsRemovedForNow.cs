﻿using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneShelf.Videos.Database.Migrations.VideosDatabaseMigrations
{
    /// <inheritdoc />
    public partial class TelegramMediaItemsRemovedForNow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DownloadedFileName",
                table: "TelegramMedia");

            migrationBuilder.DropColumn(
                name: "DownloadedThumbnailFileName",
                table: "TelegramMedia");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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
        }
    }
}
