using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneShelf.Videos.Database.Migrations.VideosDatabaseMigrations
{
    /// <inheritdoc />
    public partial class LiveAttributes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "Duration",
                table: "LiveMediae",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FileName",
                table: "LiveMediae",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Flags",
                table: "LiveMediae",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "Height",
                table: "LiveMediae",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsForwarded",
                table: "LiveMediae",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "MediaDate",
                table: "LiveMediae",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<long>(
                name: "MediaId",
                table: "LiveMediae",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<DateTime>(
                name: "MessageDate",
                table: "LiveMediae",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "MimeType",
                table: "LiveMediae",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "Size",
                table: "LiveMediae",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<int>(
                name: "Type",
                table: "LiveMediae",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "VideoFlags",
                table: "LiveMediae",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Width",
                table: "LiveMediae",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Duration",
                table: "LiveMediae");

            migrationBuilder.DropColumn(
                name: "FileName",
                table: "LiveMediae");

            migrationBuilder.DropColumn(
                name: "Flags",
                table: "LiveMediae");

            migrationBuilder.DropColumn(
                name: "Height",
                table: "LiveMediae");

            migrationBuilder.DropColumn(
                name: "IsForwarded",
                table: "LiveMediae");

            migrationBuilder.DropColumn(
                name: "MediaDate",
                table: "LiveMediae");

            migrationBuilder.DropColumn(
                name: "MediaId",
                table: "LiveMediae");

            migrationBuilder.DropColumn(
                name: "MessageDate",
                table: "LiveMediae");

            migrationBuilder.DropColumn(
                name: "MimeType",
                table: "LiveMediae");

            migrationBuilder.DropColumn(
                name: "Size",
                table: "LiveMediae");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "LiveMediae");

            migrationBuilder.DropColumn(
                name: "VideoFlags",
                table: "LiveMediae");

            migrationBuilder.DropColumn(
                name: "Width",
                table: "LiveMediae");
        }
    }
}
