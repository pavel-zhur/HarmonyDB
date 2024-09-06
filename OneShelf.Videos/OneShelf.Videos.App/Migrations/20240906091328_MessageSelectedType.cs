using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneShelf.Videos.App.Migrations.VideosDatabaseMigrations
{
    /// <inheritdoc />
    public partial class MessageSelectedType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SelectedType",
                table: "Messages",
                type: "nvarchar(max)",
                nullable: true,
                computedColumnSql: "case when photo is not null then 'photo' when mimetype like 'video/%' and isnull(mediatype, 'null') in ('video_file', 'null') then 'video' else null end");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SelectedType",
                table: "Messages");
        }
    }
}
