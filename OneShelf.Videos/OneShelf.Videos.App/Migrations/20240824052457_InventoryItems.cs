using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneShelf.Videos.App.Migrations.VideosDatabaseMigrations
{
    /// <inheritdoc />
    public partial class InventoryItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InventoryItems",
                columns: table => new
                {
                    DatabaseInventoryItemId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Id = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BaseUrl = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FileName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsPhoto = table.Column<bool>(type: "bit", nullable: false),
                    IsVideo = table.Column<bool>(type: "bit", nullable: false),
                    ProductUrl = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SyncDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    MimeType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ContributorInfoDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ContributorInfoProfilePictureBaseUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MediaMetadataHeight = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MediaMetadataWidth = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MediaMetadataCreationTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    MediaMetadataPhotoApertureFNumber = table.Column<float>(type: "real", nullable: true),
                    MediaMetadataPhotoExposureTime = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MediaMetadataPhotoFocalLength = table.Column<float>(type: "real", nullable: true),
                    MediaMetadataPhotoIsoEquivalent = table.Column<int>(type: "int", nullable: true),
                    MediaMetadataPhotoCameraMake = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MediaMetadataPhotoCameraModel = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MediaMetadataVideoStatus = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MediaMetadataVideoFps = table.Column<double>(type: "float", nullable: true),
                    MediaMetadataVideoCameraMake = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MediaMetadataVideoCameraModel = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryItems", x => x.DatabaseInventoryItemId);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InventoryItems");
        }
    }
}
