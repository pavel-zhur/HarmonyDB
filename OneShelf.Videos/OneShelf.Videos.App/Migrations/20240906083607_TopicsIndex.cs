using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneShelf.Videos.App.Migrations.VideosDatabaseMigrations
{
    /// <inheritdoc />
    public partial class TopicsIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Topics_ChatId",
                table: "Topics");

            migrationBuilder.CreateIndex(
                name: "IX_Topics_ChatId_RootMessageIdOr0",
                table: "Topics",
                columns: new[] { "ChatId", "RootMessageIdOr0" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Topics_ChatId_RootMessageIdOr0",
                table: "Topics");

            migrationBuilder.CreateIndex(
                name: "IX_Topics_ChatId",
                table: "Topics",
                column: "ChatId");
        }
    }
}
