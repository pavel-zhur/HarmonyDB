using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneShelf.OneDragon.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddLimits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "UseLimits",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "Limits",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Images = table.Column<int>(type: "int", nullable: true),
                    Texts = table.Column<int>(type: "int", nullable: true),
                    Window = table.Column<long>(type: "bigint", nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Limits", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Limits");

            migrationBuilder.DropColumn(
                name: "UseLimits",
                table: "Users");
        }
    }
}
