using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneShelf.Videos.Database.Migrations.VideosDatabaseMigrations
{
    /// <inheritdoc />
    public partial class Media15 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TopicId",
                table: "Mediae",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Mediae_TopicId",
                table: "Mediae",
                column: "TopicId");

            migrationBuilder.AddForeignKey(
                name: "FK_Mediae_Topics_TopicId",
                table: "Mediae",
                column: "TopicId",
                principalTable: "Topics",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Mediae_Topics_TopicId",
                table: "Mediae");

            migrationBuilder.DropIndex(
                name: "IX_Mediae_TopicId",
                table: "Mediae");

            migrationBuilder.DropColumn(
                name: "TopicId",
                table: "Mediae");
        }
    }
}
