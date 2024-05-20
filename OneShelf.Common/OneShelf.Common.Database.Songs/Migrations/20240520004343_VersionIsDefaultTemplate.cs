using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneShelf.Common.Database.Songs.Migrations
{
    /// <inheritdoc />
    public partial class VersionIsDefaultTemplate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsDefaultTemplate",
                table: "Versions",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsDefaultTemplate",
                table: "Versions");
        }
    }
}
