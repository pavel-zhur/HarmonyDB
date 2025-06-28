using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneShelf.OneDragon.Database.Migrations
{
    /// <inheritdoc />
    public partial class Sora : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "VeoVersion",
                table: "AiParameters");

            migrationBuilder.RenameColumn(
                name: "VeoModel",
                table: "AiParameters",
                newName: "SoraModel");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "SoraModel",
                table: "AiParameters",
                newName: "VeoModel");

            migrationBuilder.AddColumn<int>(
                name: "VeoVersion",
                table: "AiParameters",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }
    }
}
