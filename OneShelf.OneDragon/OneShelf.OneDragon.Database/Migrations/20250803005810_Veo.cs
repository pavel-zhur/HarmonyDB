using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneShelf.OneDragon.Database.Migrations
{
    /// <inheritdoc />
    public partial class Veo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Group",
                table: "Users",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "anon");

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "Limits",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Group",
                table: "Limits",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "anon");

            migrationBuilder.AddColumn<bool>(
                name: "IsShared",
                table: "Limits",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "VeoModel",
                table: "AiParameters",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Group",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "Limits");

            migrationBuilder.DropColumn(
                name: "Group",
                table: "Limits");

            migrationBuilder.DropColumn(
                name: "IsShared",
                table: "Limits");

            migrationBuilder.DropColumn(
                name: "VeoModel",
                table: "AiParameters");
        }
    }
}
