using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneShelf.Videos.Database.Migrations.VideosDatabaseMigrations
{
    /// <inheritdoc />
    public partial class Statics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AlbumConstraints_Topics_TopicId",
                table: "AlbumConstraints");

            migrationBuilder.DropForeignKey(
                name: "FK_Chats_ChatFolders_ChatFolderId",
                table: "Chats");

            migrationBuilder.DropForeignKey(
                name: "FK_Messages_Chats_ChatId",
                table: "Messages");

            migrationBuilder.DropForeignKey(
                name: "FK_Messages_Topics_TopicId",
                table: "Messages");

            migrationBuilder.DropForeignKey(
                name: "FK_Topics_Chats_ChatId",
                table: "Topics");

            migrationBuilder.RenameColumn(
                name: "MessageId",
                table: "UploadedItems",
                newName: "StaticMessageId");

            migrationBuilder.RenameColumn(
                name: "ChatId",
                table: "UploadedItems",
                newName: "StaticChatId");

            migrationBuilder.RenameColumn(
                name: "ChatId",
                table: "Topics",
                newName: "StaticChatId");

            migrationBuilder.RenameIndex(
                name: "IX_Topics_ChatId_RootMessageIdOr0",
                table: "Topics",
                newName: "IX_Topics_StaticChatId_RootMessageIdOr0");

            migrationBuilder.RenameColumn(
                name: "TopicId",
                table: "Messages",
                newName: "StaticTopicId");

            migrationBuilder.RenameColumn(
                name: "ChatId",
                table: "Messages",
                newName: "StaticChatId");

            migrationBuilder.RenameColumn(
                name: "DatabaseMessageId",
                table: "Messages",
                newName: "DatabaseStaticMessageId");

            migrationBuilder.RenameIndex(
                name: "IX_Messages_TopicId",
                table: "Messages",
                newName: "IX_Messages_StaticTopicId");

            migrationBuilder.RenameIndex(
                name: "IX_Messages_ChatId_Id",
                table: "Messages",
                newName: "IX_Messages_StaticChatId_Id");

            migrationBuilder.RenameColumn(
                name: "ChatFolderId",
                table: "Chats",
                newName: "StaticChatFolderId");

            migrationBuilder.RenameIndex(
                name: "IX_Chats_ChatFolderId",
                table: "Chats",
                newName: "IX_Chats_StaticChatFolderId");

            migrationBuilder.RenameColumn(
                name: "TopicId",
                table: "AlbumConstraints",
                newName: "StaticTopicId");

            migrationBuilder.RenameColumn(
                name: "MessageSelectedType",
                table: "AlbumConstraints",
                newName: "StaticMessageSelectedType");

            migrationBuilder.RenameIndex(
                name: "IX_AlbumConstraints_TopicId",
                table: "AlbumConstraints",
                newName: "IX_AlbumConstraints_StaticTopicId");

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
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

            migrationBuilder.RenameColumn(
                name: "StaticMessageId",
                table: "UploadedItems",
                newName: "MessageId");

            migrationBuilder.RenameColumn(
                name: "StaticChatId",
                table: "UploadedItems",
                newName: "ChatId");

            migrationBuilder.RenameColumn(
                name: "StaticChatId",
                table: "Topics",
                newName: "ChatId");

            migrationBuilder.RenameIndex(
                name: "IX_Topics_StaticChatId_RootMessageIdOr0",
                table: "Topics",
                newName: "IX_Topics_ChatId_RootMessageIdOr0");

            migrationBuilder.RenameColumn(
                name: "StaticTopicId",
                table: "Messages",
                newName: "TopicId");

            migrationBuilder.RenameColumn(
                name: "StaticChatId",
                table: "Messages",
                newName: "ChatId");

            migrationBuilder.RenameColumn(
                name: "DatabaseStaticMessageId",
                table: "Messages",
                newName: "DatabaseMessageId");

            migrationBuilder.RenameIndex(
                name: "IX_Messages_StaticTopicId",
                table: "Messages",
                newName: "IX_Messages_TopicId");

            migrationBuilder.RenameIndex(
                name: "IX_Messages_StaticChatId_Id",
                table: "Messages",
                newName: "IX_Messages_ChatId_Id");

            migrationBuilder.RenameColumn(
                name: "StaticChatFolderId",
                table: "Chats",
                newName: "ChatFolderId");

            migrationBuilder.RenameIndex(
                name: "IX_Chats_StaticChatFolderId",
                table: "Chats",
                newName: "IX_Chats_ChatFolderId");

            migrationBuilder.RenameColumn(
                name: "StaticTopicId",
                table: "AlbumConstraints",
                newName: "TopicId");

            migrationBuilder.RenameColumn(
                name: "StaticMessageSelectedType",
                table: "AlbumConstraints",
                newName: "MessageSelectedType");

            migrationBuilder.RenameIndex(
                name: "IX_AlbumConstraints_StaticTopicId",
                table: "AlbumConstraints",
                newName: "IX_AlbumConstraints_TopicId");

            migrationBuilder.AddForeignKey(
                name: "FK_AlbumConstraints_Topics_TopicId",
                table: "AlbumConstraints",
                column: "TopicId",
                principalTable: "Topics",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Chats_ChatFolders_ChatFolderId",
                table: "Chats",
                column: "ChatFolderId",
                principalTable: "ChatFolders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Messages_Chats_ChatId",
                table: "Messages",
                column: "ChatId",
                principalTable: "Chats",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Messages_Topics_TopicId",
                table: "Messages",
                column: "TopicId",
                principalTable: "Topics",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Topics_Chats_ChatId",
                table: "Topics",
                column: "ChatId",
                principalTable: "Chats",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
