using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneShelf.Videos.App.Migrations.VideosDatabaseMigrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UploadedItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ChatId = table.Column<long>(type: "bigint", nullable: false),
                    MessageId = table.Column<int>(type: "int", nullable: false),
                    TelegramPublishedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StatusCode = table.Column<int>(type: "int", nullable: false),
                    StatusMessage = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MediaItemId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MediaItemIsPhoto = table.Column<bool>(type: "bit", nullable: true),
                    MediaItemIsVideo = table.Column<bool>(type: "bit", nullable: true),
                    MediaItemMimeType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MediaItemSyncDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MediaItemMetadataCreationType = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Json = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FileNameTimestamp = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UploadedItems", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UploadedItems");
        }
    }
}
