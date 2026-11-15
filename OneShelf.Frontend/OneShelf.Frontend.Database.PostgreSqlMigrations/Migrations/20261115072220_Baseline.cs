using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneShelf.Frontend.Database.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class Baseline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Pdfs",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    ExternalId = table.Column<string>(type: "text", nullable: false),
                    ExportConfiguration = table.Column<string>(type: "text", nullable: false),
                    Version = table.Column<string>(type: "character varying(100)", nullable: false),
                    PageCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Data = table.Column<byte[]>(type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pdfs", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Pdfs");
        }
    }
}
