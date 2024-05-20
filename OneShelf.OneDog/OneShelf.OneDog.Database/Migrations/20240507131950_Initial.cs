using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneShelf.OneDog.Database.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Domains",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ChatId = table.Column<long>(type: "bigint", nullable: false),
                    TopicId = table.Column<int>(type: "int", nullable: false),
                    PrivateDescription = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SystemMessage = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    GptVersion = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DalleVersion = table.Column<int>(type: "int", nullable: false),
                    FrequencyPenalty = table.Column<float>(type: "real", nullable: true),
                    PresencePenalty = table.Column<float>(type: "real", nullable: true),
                    BotToken = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    WebHooksSecretToken = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    BillingRatio = table.Column<float>(type: "real", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Domains", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Chats",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DomainId = table.Column<int>(type: "int", nullable: false),
                    ChatId = table.Column<long>(type: "bigint", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsForum = table.Column<bool>(type: "bit", nullable: true),
                    FirstUpdateReceivedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastUpdateReceivedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatesCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Chats", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Chats_Domains_DomainId",
                        column: x => x.DomainId,
                        principalTable: "Domains",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DomainUser",
                columns: table => new
                {
                    AdministratedDomainsId = table.Column<int>(type: "int", nullable: false),
                    AdministratorsId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DomainUser", x => new { x.AdministratedDomainsId, x.AdministratorsId });
                    table.ForeignKey(
                        name: "FK_DomainUser_Domains_AdministratedDomainsId",
                        column: x => x.AdministratedDomainsId,
                        principalTable: "Domains",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DomainUser_Users_AdministratorsId",
                        column: x => x.AdministratorsId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Interactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ShortInfoSerialized = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Serialized = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    InteractionType = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    DomainId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Interactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Interactions_Domains_DomainId",
                        column: x => x.DomainId,
                        principalTable: "Domains",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Interactions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Chats_ChatId",
                table: "Chats",
                column: "ChatId");

            migrationBuilder.CreateIndex(
                name: "IX_Chats_DomainId_ChatId",
                table: "Chats",
                columns: new[] { "DomainId", "ChatId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Domains_WebHooksSecretToken",
                table: "Domains",
                column: "WebHooksSecretToken");

            migrationBuilder.CreateIndex(
                name: "IX_DomainUser_AdministratorsId",
                table: "DomainUser",
                column: "AdministratorsId");

            migrationBuilder.CreateIndex(
                name: "IX_Interactions_DomainId",
                table: "Interactions",
                column: "DomainId");

            migrationBuilder.CreateIndex(
                name: "IX_Interactions_InteractionType_CreatedOn",
                table: "Interactions",
                columns: new[] { "InteractionType", "CreatedOn" });

            migrationBuilder.CreateIndex(
                name: "IX_Interactions_UserId",
                table: "Interactions",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Chats");

            migrationBuilder.DropTable(
                name: "DomainUser");

            migrationBuilder.DropTable(
                name: "Interactions");

            migrationBuilder.DropTable(
                name: "Domains");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
