using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneShelf.Videos.Database.Migrations.VideosDatabaseMigrations
{
    /// <inheritdoc />
    public partial class Media13 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_AlbumConstraints_TopicId",
                table: "AlbumConstraints",
                column: "TopicId");

            migrationBuilder.AddForeignKey(
                name: "FK_AlbumConstraints_Topics_TopicId",
                table: "AlbumConstraints",
                column: "TopicId",
                principalTable: "Topics",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AlbumConstraints_Topics_TopicId",
                table: "AlbumConstraints");

            migrationBuilder.DropIndex(
                name: "IX_AlbumConstraints_TopicId",
                table: "AlbumConstraints");
        }
    }
}
