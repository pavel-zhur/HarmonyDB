using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneShelf.Videos.Database.Migrations.VideosDatabaseMigrations
{
    /// <inheritdoc />
    public partial class Media2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Mediae",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StaticChatId = table.Column<long>(type: "bigint", nullable: true),
                    StaticMessageId = table.Column<int>(type: "int", nullable: true),
                    LiveChatId = table.Column<long>(type: "bigint", nullable: true),
                    LiveMediaId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Mediae", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Mediae_LiveChats_LiveChatId",
                        column: x => x.LiveChatId,
                        principalTable: "LiveChats",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Mediae_LiveMediae_LiveMediaId_LiveChatId",
                        columns: x => new { x.LiveMediaId, x.LiveChatId },
                        principalTable: "LiveMediae",
                        principalColumns: new[] { "Id", "LiveTopicLiveChatId" });
                    table.ForeignKey(
                        name: "FK_Mediae_StaticChats_StaticChatId",
                        column: x => x.StaticChatId,
                        principalTable: "StaticChats",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Mediae_StaticMessages_StaticChatId_StaticMessageId",
                        columns: x => new { x.StaticChatId, x.StaticMessageId },
                        principalTable: "StaticMessages",
                        principalColumns: new[] { "StaticChatId", "Id" });
                });

            migrationBuilder.CreateIndex(
                name: "IX_Mediae_LiveChatId",
                table: "Mediae",
                column: "LiveChatId");

            migrationBuilder.CreateIndex(
                name: "IX_Mediae_LiveMediaId_LiveChatId",
                table: "Mediae",
                columns: new[] { "LiveMediaId", "LiveChatId" },
                unique: true,
                filter: "[LiveMediaId] IS NOT NULL AND [LiveChatId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Mediae_StaticChatId_StaticMessageId",
                table: "Mediae",
                columns: new[] { "StaticChatId", "StaticMessageId" },
                unique: true,
                filter: "[StaticChatId] IS NOT NULL AND [StaticMessageId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Mediae");
        }
    }
}
