using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneShelf.OneDog.Database.Migrations
{
    /// <inheritdoc />
    public partial class ImagesLimit2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImagesLimit_Window",
                table: "Domains");
            migrationBuilder.AddColumn<long>(
                name: "ImagesLimit_Window",
                table: "Domains",
                type: "bigint",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<TimeSpan>(
                name: "ImagesLimit_Window",
                table: "Domains",
                type: "time",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);
        }
    }
}
