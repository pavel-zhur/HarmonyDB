using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneShelf.Videos.Database.Migrations.VideosDatabaseMigrations
{
    /// <inheritdoc />
    public partial class LiveMediaPrimaryKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_LiveMediae",
                table: "LiveMediae");

            migrationBuilder.AddPrimaryKey(
                name: "PK_LiveMediae",
                table: "LiveMediae",
                columns: new[] { "Id", "TopicChatId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_LiveMediae",
                table: "LiveMediae");

            migrationBuilder.AddPrimaryKey(
                name: "PK_LiveMediae",
                table: "LiveMediae",
                column: "Id");
        }
    }
}
