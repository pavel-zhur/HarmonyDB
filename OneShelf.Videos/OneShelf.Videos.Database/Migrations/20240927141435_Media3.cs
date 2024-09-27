using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneShelf.Videos.Database.Migrations.VideosDatabaseMigrations
{
    /// <inheritdoc />
    public partial class Media3 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MediaId",
                table: "UploadedItems",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_UploadedItems_MediaId",
                table: "UploadedItems",
                column: "MediaId",
                unique: true,
                filter: "[MediaId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_UploadedItems_Mediae_MediaId",
                table: "UploadedItems",
                column: "MediaId",
                principalTable: "Mediae",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UploadedItems_Mediae_MediaId",
                table: "UploadedItems");

            migrationBuilder.DropIndex(
                name: "IX_UploadedItems_MediaId",
                table: "UploadedItems");

            migrationBuilder.DropColumn(
                name: "MediaId",
                table: "UploadedItems");
        }
    }
}
