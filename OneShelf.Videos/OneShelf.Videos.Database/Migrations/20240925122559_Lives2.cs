using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneShelf.Videos.Database.Migrations.VideosDatabaseMigrations
{
    /// <inheritdoc />
    public partial class Lives2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LiveMediae_LiveTopics_TopicId_TopicChatId",
                table: "LiveMediae");

            migrationBuilder.DropForeignKey(
                name: "FK_LiveTopics_LiveChats_ChatId",
                table: "LiveTopics");

            migrationBuilder.RenameColumn(
                name: "ChatId",
                table: "LiveTopics",
                newName: "LiveChatId");

            migrationBuilder.RenameIndex(
                name: "IX_LiveTopics_ChatId",
                table: "LiveTopics",
                newName: "IX_LiveTopics_LiveChatId");

            migrationBuilder.RenameColumn(
                name: "TopicId",
                table: "LiveMediae",
                newName: "LiveTopicId");

            migrationBuilder.RenameColumn(
                name: "TopicChatId",
                table: "LiveMediae",
                newName: "LiveTopicLiveChatId");

            migrationBuilder.RenameIndex(
                name: "IX_LiveMediae_TopicId_TopicChatId",
                table: "LiveMediae",
                newName: "IX_LiveMediae_LiveTopicId_LiveTopicLiveChatId");

            migrationBuilder.AddForeignKey(
                name: "FK_LiveMediae_LiveTopics_LiveTopicId_LiveTopicLiveChatId",
                table: "LiveMediae",
                columns: new[] { "LiveTopicId", "LiveTopicLiveChatId" },
                principalTable: "LiveTopics",
                principalColumns: new[] { "Id", "LiveChatId" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_LiveTopics_LiveChats_LiveChatId",
                table: "LiveTopics",
                column: "LiveChatId",
                principalTable: "LiveChats",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LiveMediae_LiveTopics_LiveTopicId_LiveTopicLiveChatId",
                table: "LiveMediae");

            migrationBuilder.DropForeignKey(
                name: "FK_LiveTopics_LiveChats_LiveChatId",
                table: "LiveTopics");

            migrationBuilder.RenameColumn(
                name: "LiveChatId",
                table: "LiveTopics",
                newName: "ChatId");

            migrationBuilder.RenameIndex(
                name: "IX_LiveTopics_LiveChatId",
                table: "LiveTopics",
                newName: "IX_LiveTopics_ChatId");

            migrationBuilder.RenameColumn(
                name: "LiveTopicId",
                table: "LiveMediae",
                newName: "TopicId");

            migrationBuilder.RenameColumn(
                name: "LiveTopicLiveChatId",
                table: "LiveMediae",
                newName: "TopicChatId");

            migrationBuilder.RenameIndex(
                name: "IX_LiveMediae_LiveTopicId_LiveTopicLiveChatId",
                table: "LiveMediae",
                newName: "IX_LiveMediae_TopicId_TopicChatId");

            migrationBuilder.AddForeignKey(
                name: "FK_LiveMediae_LiveTopics_TopicId_TopicChatId",
                table: "LiveMediae",
                columns: new[] { "TopicId", "TopicChatId" },
                principalTable: "LiveTopics",
                principalColumns: new[] { "Id", "ChatId" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_LiveTopics_LiveChats_ChatId",
                table: "LiveTopics",
                column: "ChatId",
                principalTable: "LiveChats",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
