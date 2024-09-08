using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneShelf.Videos.Database.Migrations.VideosDatabaseMigrations
{
    /// <inheritdoc />
    public partial class SourceTopics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Sources_Chats_ChatId",
                table: "Sources");

            migrationBuilder.DropIndex(
                name: "IX_Sources_ChatId",
                table: "Sources");

            migrationBuilder.DropColumn(
                name: "ChatId",
                table: "Sources");

            migrationBuilder.DropColumn(
                name: "RootMessageId",
                table: "Sources");

            migrationBuilder.CreateTable(
                name: "SourceTopics",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SourceId = table.Column<int>(type: "int", nullable: false),
                    ChatId = table.Column<long>(type: "bigint", nullable: false),
                    RootMessageId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SourceTopics", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SourceTopics_Chats_ChatId",
                        column: x => x.ChatId,
                        principalTable: "Chats",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SourceTopics_Sources_SourceId",
                        column: x => x.SourceId,
                        principalTable: "Sources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SourceTopics_ChatId",
                table: "SourceTopics",
                column: "ChatId");

            migrationBuilder.CreateIndex(
                name: "IX_SourceTopics_SourceId",
                table: "SourceTopics",
                column: "SourceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SourceTopics");

            migrationBuilder.AddColumn<long>(
                name: "ChatId",
                table: "Sources",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<int>(
                name: "RootMessageId",
                table: "Sources",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sources_ChatId",
                table: "Sources",
                column: "ChatId");

            migrationBuilder.AddForeignKey(
                name: "FK_Sources_Chats_ChatId",
                table: "Sources",
                column: "ChatId",
                principalTable: "Chats",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
