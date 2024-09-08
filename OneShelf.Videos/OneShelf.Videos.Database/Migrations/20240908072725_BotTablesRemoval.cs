using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneShelf.Videos.Database.Migrations.VideosDatabaseMigrations
{
    /// <inheritdoc />
    public partial class BotTablesRemoval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TagTelegramMedia");

            migrationBuilder.DropTable(
                name: "Tags");

            migrationBuilder.DropTable(
                name: "TelegramMedia");

            migrationBuilder.DropTable(
                name: "TelegramUpdates");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Tags",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tags", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TelegramUpdates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Json = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TelegramUpdates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TelegramMedia",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TelegramUpdateId = table.Column<int>(type: "int", nullable: false),
                    ChatId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DownloadedFileName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DownloadedThumbnailFileName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Duration = table.Column<int>(type: "int", nullable: true),
                    FileId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FileSize = table.Column<long>(type: "bigint", nullable: true),
                    FileUniqueId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ForwardOriginTitle = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    HandlerMessageId = table.Column<int>(type: "int", nullable: true),
                    Height = table.Column<int>(type: "int", nullable: true),
                    MediaGroupId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    MessageId = table.Column<int>(type: "int", nullable: false),
                    MimeType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TelegramPublishedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ThumbnailFileId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ThumbnailHeight = table.Column<int>(type: "int", nullable: true),
                    ThumbnailWidth = table.Column<int>(type: "int", nullable: true),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Width = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TelegramMedia", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TelegramMedia_TelegramUpdates_TelegramUpdateId",
                        column: x => x.TelegramUpdateId,
                        principalTable: "TelegramUpdates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TagTelegramMedia",
                columns: table => new
                {
                    TagsId = table.Column<int>(type: "int", nullable: false),
                    TelegramMediaeId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TagTelegramMedia", x => new { x.TagsId, x.TelegramMediaeId });
                    table.ForeignKey(
                        name: "FK_TagTelegramMedia_Tags_TagsId",
                        column: x => x.TagsId,
                        principalTable: "Tags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TagTelegramMedia_TelegramMedia_TelegramMediaeId",
                        column: x => x.TelegramMediaeId,
                        principalTable: "TelegramMedia",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TagTelegramMedia_TelegramMediaeId",
                table: "TagTelegramMedia",
                column: "TelegramMediaeId");

            migrationBuilder.CreateIndex(
                name: "IX_TelegramMedia_MediaGroupId",
                table: "TelegramMedia",
                column: "MediaGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_TelegramMedia_TelegramUpdateId",
                table: "TelegramMedia",
                column: "TelegramUpdateId",
                unique: true);
        }
    }
}
