using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneShelf.Videos.Database.Migrations.VideosDatabaseMigrations
{
    /// <inheritdoc />
    public partial class Lives : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LiveChats",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LiveChats", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LiveTopics",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    ChatId = table.Column<long>(type: "bigint", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LiveTopics", x => new { x.Id, x.ChatId });
                    table.ForeignKey(
                        name: "FK_LiveTopics_LiveChats_ChatId",
                        column: x => x.ChatId,
                        principalTable: "LiveChats",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LiveMediae",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    TopicId = table.Column<int>(type: "int", nullable: false),
                    TopicChatId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LiveMediae", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LiveMediae_LiveTopics_TopicId_TopicChatId",
                        columns: x => new { x.TopicId, x.TopicChatId },
                        principalTable: "LiveTopics",
                        principalColumns: new[] { "Id", "ChatId" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LiveMediae_TopicId_TopicChatId",
                table: "LiveMediae",
                columns: new[] { "TopicId", "TopicChatId" });

            migrationBuilder.CreateIndex(
                name: "IX_LiveTopics_ChatId",
                table: "LiveTopics",
                column: "ChatId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LiveMediae");

            migrationBuilder.DropTable(
                name: "LiveTopics");

            migrationBuilder.DropTable(
                name: "LiveChats");
        }
    }
}
