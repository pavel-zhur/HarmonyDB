using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneShelf.Videos.App.Migrations
{
    /// <inheritdoc />
    public partial class FileNameTimestamps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "FileNameTimestamp",
                table: "UploadedItems",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FileNameTimestamp",
                table: "UploadedItems");
        }
    }
}
