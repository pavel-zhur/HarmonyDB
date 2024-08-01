using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneShelf.OneDog.Database.Migrations
{
    /// <inheritdoc />
    public partial class ImagesLimit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ImagesLimit_Limit",
                table: "Domains",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<TimeSpan>(
                name: "ImagesLimit_Window",
                table: "Domains",
                type: "time",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImagesLimit_Limit",
                table: "Domains");

            migrationBuilder.DropColumn(
                name: "ImagesLimit_Window",
                table: "Domains");
        }
    }
}
