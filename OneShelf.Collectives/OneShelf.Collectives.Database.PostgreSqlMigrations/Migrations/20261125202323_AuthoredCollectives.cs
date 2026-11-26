using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneShelf.Collectives.Database.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AuthoredCollectives : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuthoredCollectives",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Author = table.Column<string>(type: "text", nullable: false),
                    Versions = table.Column<string>(type: "jsonb", nullable: false),
                    LatestVisibility = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuthoredCollectives", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuthoredCollectives_Author",
                table: "AuthoredCollectives",
                column: "Author");

            migrationBuilder.CreateIndex(
                name: "IX_AuthoredCollectives_LatestVisibility",
                table: "AuthoredCollectives",
                column: "LatestVisibility");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuthoredCollectives");
        }
    }
}
