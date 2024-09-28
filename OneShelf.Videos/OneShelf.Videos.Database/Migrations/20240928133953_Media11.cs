using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneShelf.Videos.Database.Migrations.VideosDatabaseMigrations
{
    /// <inheritdoc />
    public partial class Media11 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Topics",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StaticChatId = table.Column<long>(type: "bigint", nullable: true),
                    StaticTopicRootMessageIdOr0 = table.Column<int>(type: "int", nullable: true),
                    LiveChatId = table.Column<long>(type: "bigint", nullable: true),
                    LiveTopicId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Topics", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Topics_LiveChats_LiveChatId",
                        column: x => x.LiveChatId,
                        principalTable: "LiveChats",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Topics_LiveTopics_LiveTopicId_LiveChatId",
                        columns: x => new { x.LiveTopicId, x.LiveChatId },
                        principalTable: "LiveTopics",
                        principalColumns: new[] { "Id", "LiveChatId" });
                    table.ForeignKey(
                        name: "FK_Topics_StaticChats_StaticChatId",
                        column: x => x.StaticChatId,
                        principalTable: "StaticChats",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Topics_StaticTopics_StaticChatId_StaticTopicRootMessageIdOr0",
                        columns: x => new { x.StaticChatId, x.StaticTopicRootMessageIdOr0 },
                        principalTable: "StaticTopics",
                        principalColumns: new[] { "StaticChatId", "RootMessageIdOr0" });
                });

            migrationBuilder.CreateIndex(
                name: "IX_Topics_LiveChatId",
                table: "Topics",
                column: "LiveChatId");

            migrationBuilder.CreateIndex(
                name: "IX_Topics_LiveTopicId_LiveChatId",
                table: "Topics",
                columns: new[] { "LiveTopicId", "LiveChatId" },
                unique: true,
                filter: "[LiveTopicId] IS NOT NULL AND [LiveChatId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Topics_StaticChatId_StaticTopicRootMessageIdOr0",
                table: "Topics",
                columns: new[] { "StaticChatId", "StaticTopicRootMessageIdOr0" },
                unique: true,
                filter: "[StaticChatId] IS NOT NULL AND [StaticTopicRootMessageIdOr0] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Topics");
        }
    }
}
