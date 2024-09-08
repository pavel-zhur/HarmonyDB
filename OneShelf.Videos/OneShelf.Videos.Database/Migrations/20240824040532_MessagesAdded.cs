using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneShelf.Videos.Database.Migrations.VideosDatabaseMigrations
{
    /// <inheritdoc />
    public partial class MessagesAdded : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Messages",
                columns: table => new
                {
                    DatabaseMessageId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ChatId = table.Column<long>(type: "bigint", nullable: false),
                    Id = table.Column<int>(type: "int", nullable: false),
                    MessageId = table.Column<int>(type: "int", nullable: true),
                    ReplyToMessageId = table.Column<int>(type: "int", nullable: true),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Actor = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ActorId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    From = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FromId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DateUnixtime = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Edited = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EditedUnixtime = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Action = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Inviter = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Text = table.Column<JsonElement>(type: "nvarchar(max)", nullable: false),
                    Members = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Photo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Width = table.Column<int>(type: "int", nullable: true),
                    Height = table.Column<int>(type: "int", nullable: true),
                    File = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FileName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Thumbnail = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MediaType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MimeType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DurationSeconds = table.Column<int>(type: "int", nullable: true),
                    ForwardedFrom = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StickerEmoji = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Poll = table.Column<JsonElement>(type: "nvarchar(max)", nullable: true),
                    Performer = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LocationInformation = table.Column<JsonElement>(type: "nvarchar(max)", nullable: true),
                    ContactInformation = table.Column<JsonElement>(type: "nvarchar(max)", nullable: true),
                    SavedFrom = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ViaBot = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NewIconEmojiId = table.Column<long>(type: "bigint", nullable: true),
                    InlineBotButtons = table.Column<JsonElement>(type: "nvarchar(max)", nullable: true),
                    ScheduleDate = table.Column<int>(type: "int", nullable: true),
                    Duration = table.Column<int>(type: "int", nullable: true),
                    NewTitle = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReplyToPeerId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Boosts = table.Column<int>(type: "int", nullable: true),
                    LiveLocationPeriodSeconds = table.Column<int>(type: "int", nullable: true),
                    TextEntities = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Messages", x => x.DatabaseMessageId);
                    table.ForeignKey(
                        name: "FK_Messages_Chats_ChatId",
                        column: x => x.ChatId,
                        principalTable: "Chats",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Messages_ChatId",
                table: "Messages",
                column: "ChatId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Messages");
        }
    }
}
