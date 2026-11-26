using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HarmonyDB.Shelf.Database.Migrations
{
    /// <inheritdoc />
    public partial class ChordsOldAddress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "old_address",
                table: "chords",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "old_address",
                table: "chords");
        }
    }
}
