using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneShelf.Videos.Database.Migrations.VideosDatabaseMigrations
{
    /// <inheritdoc />
    public partial class ItemsTagsAdded : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TagTelegramMedia");
        }
    }
}
