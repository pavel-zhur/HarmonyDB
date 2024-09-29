using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneShelf.Videos.Database.Migrations.VideosDatabaseMigrations
{
    /// <inheritdoc />
    public partial class Statics2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AlbumConstraints_Topics_StaticTopicId",
                table: "AlbumConstraints");

            migrationBuilder.DropForeignKey(
                name: "FK_Chats_ChatFolders_StaticChatFolderId",
                table: "Chats");

            migrationBuilder.DropForeignKey(
                name: "FK_Messages_Chats_StaticChatId",
                table: "Messages");

            migrationBuilder.DropForeignKey(
                name: "FK_Messages_Topics_StaticTopicId",
                table: "Messages");

            migrationBuilder.DropForeignKey(
                name: "FK_Topics_Chats_StaticChatId",
                table: "Topics");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Topics",
                table: "Topics");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Messages",
                table: "Messages");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Chats",
                table: "Chats");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ChatFolders",
                table: "ChatFolders");

            migrationBuilder.RenameTable(
                name: "Topics",
                newName: "StaticTopics");

            migrationBuilder.RenameTable(
                name: "Messages",
                newName: "StaticMessages");

            migrationBuilder.RenameTable(
                name: "Chats",
                newName: "StaticChats");

            migrationBuilder.RenameTable(
                name: "ChatFolders",
                newName: "StaticChatFolders");

            migrationBuilder.RenameIndex(
                name: "IX_Topics_StaticChatId_RootMessageIdOr0",
                table: "StaticTopics",
                newName: "IX_StaticTopics_StaticChatId_RootMessageIdOr0");

            migrationBuilder.RenameIndex(
                name: "IX_Messages_StaticTopicId",
                table: "StaticMessages",
                newName: "IX_StaticMessages_StaticTopicId");

            migrationBuilder.RenameIndex(
                name: "IX_Messages_StaticChatId_Id",
                table: "StaticMessages",
                newName: "IX_StaticMessages_StaticChatId_Id");

            migrationBuilder.RenameIndex(
                name: "IX_Chats_StaticChatFolderId",
                table: "StaticChats",
                newName: "IX_StaticChats_StaticChatFolderId");

            migrationBuilder.RenameIndex(
                name: "IX_ChatFolders_Name",
                table: "StaticChatFolders",
                newName: "IX_StaticChatFolders_Name");

            migrationBuilder.AddPrimaryKey(
                name: "PK_StaticTopics",
                table: "StaticTopics",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_StaticMessages",
                table: "StaticMessages",
                column: "DatabaseStaticMessageId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_StaticChats",
                table: "StaticChats",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_StaticChatFolders",
                table: "StaticChatFolders",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_AlbumConstraints_StaticTopics_StaticTopicId",
                table: "AlbumConstraints",
                column: "StaticTopicId",
                principalTable: "StaticTopics",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_StaticChats_StaticChatFolders_StaticChatFolderId",
                table: "StaticChats",
                column: "StaticChatFolderId",
                principalTable: "StaticChatFolders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_StaticMessages_StaticChats_StaticChatId",
                table: "StaticMessages",
                column: "StaticChatId",
                principalTable: "StaticChats",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_StaticMessages_StaticTopics_StaticTopicId",
                table: "StaticMessages",
                column: "StaticTopicId",
                principalTable: "StaticTopics",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_StaticTopics_StaticChats_StaticChatId",
                table: "StaticTopics",
                column: "StaticChatId",
                principalTable: "StaticChats",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AlbumConstraints_StaticTopics_StaticTopicId",
                table: "AlbumConstraints");

            migrationBuilder.DropForeignKey(
                name: "FK_StaticChats_StaticChatFolders_StaticChatFolderId",
                table: "StaticChats");

            migrationBuilder.DropForeignKey(
                name: "FK_StaticMessages_StaticChats_StaticChatId",
                table: "StaticMessages");

            migrationBuilder.DropForeignKey(
                name: "FK_StaticMessages_StaticTopics_StaticTopicId",
                table: "StaticMessages");

            migrationBuilder.DropForeignKey(
                name: "FK_StaticTopics_StaticChats_StaticChatId",
                table: "StaticTopics");

            migrationBuilder.DropPrimaryKey(
                name: "PK_StaticTopics",
                table: "StaticTopics");

            migrationBuilder.DropPrimaryKey(
                name: "PK_StaticMessages",
                table: "StaticMessages");

            migrationBuilder.DropPrimaryKey(
                name: "PK_StaticChats",
                table: "StaticChats");

            migrationBuilder.DropPrimaryKey(
                name: "PK_StaticChatFolders",
                table: "StaticChatFolders");

            migrationBuilder.RenameTable(
                name: "StaticTopics",
                newName: "Topics");

            migrationBuilder.RenameTable(
                name: "StaticMessages",
                newName: "Messages");

            migrationBuilder.RenameTable(
                name: "StaticChats",
                newName: "Chats");

            migrationBuilder.RenameTable(
                name: "StaticChatFolders",
                newName: "ChatFolders");

            migrationBuilder.RenameIndex(
                name: "IX_StaticTopics_StaticChatId_RootMessageIdOr0",
                table: "Topics",
                newName: "IX_Topics_StaticChatId_RootMessageIdOr0");

            migrationBuilder.RenameIndex(
                name: "IX_StaticMessages_StaticTopicId",
                table: "Messages",
                newName: "IX_Messages_StaticTopicId");

            migrationBuilder.RenameIndex(
                name: "IX_StaticMessages_StaticChatId_Id",
                table: "Messages",
                newName: "IX_Messages_StaticChatId_Id");

            migrationBuilder.RenameIndex(
                name: "IX_StaticChats_StaticChatFolderId",
                table: "Chats",
                newName: "IX_Chats_StaticChatFolderId");

            migrationBuilder.RenameIndex(
                name: "IX_StaticChatFolders_Name",
                table: "ChatFolders",
                newName: "IX_ChatFolders_Name");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Topics",
                table: "Topics",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Messages",
                table: "Messages",
                column: "DatabaseStaticMessageId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Chats",
                table: "Chats",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ChatFolders",
                table: "ChatFolders",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_AlbumConstraints_Topics_StaticTopicId",
                table: "AlbumConstraints",
                column: "StaticTopicId",
                principalTable: "Topics",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Chats_ChatFolders_StaticChatFolderId",
                table: "Chats",
                column: "StaticChatFolderId",
                principalTable: "ChatFolders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Messages_Chats_StaticChatId",
                table: "Messages",
                column: "StaticChatId",
                principalTable: "Chats",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Messages_Topics_StaticTopicId",
                table: "Messages",
                column: "StaticTopicId",
                principalTable: "Topics",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Topics_Chats_StaticChatId",
                table: "Topics",
                column: "StaticChatId",
                principalTable: "Chats",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
