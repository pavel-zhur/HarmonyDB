using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneShelf.Videos.Database.Migrations.VideosDatabaseMigrations
{
    /// <inheritdoc />
    public partial class Media7 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SelectedType",
                table: "LiveMediae",
                type: "nvarchar(max)",
                nullable: true,
                computedColumnSql: "\r\ncase \r\nwhen type = 'Photo' then 'Photo' \r\nwhen MimeType like 'video/%' then 'Video' \r\nwhen mimetype = 'image/webp' then null \r\nwhen mimetype = 'application/x-tgsticker' then null\r\nwhen MimeType like 'image/%' then 'Photo' \r\nelse null\r\nend\r\n");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SelectedType",
                table: "LiveMediae");
        }
    }
}
