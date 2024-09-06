using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneShelf.Videos.App.Migrations.VideosDatabaseMigrations
{
    /// <inheritdoc />
    public partial class AlbumConstraintsSelectedTypeVarChar : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "MessageSelectedType",
                table: "AlbumConstraints",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "MessageSelectedType",
                table: "AlbumConstraints",
                type: "int",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);
        }
    }
}
