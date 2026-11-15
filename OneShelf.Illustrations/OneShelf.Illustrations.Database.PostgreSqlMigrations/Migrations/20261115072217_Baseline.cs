using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneShelf.Illustrations.Database.PostgreSqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class Baseline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Illustrations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Image = table.Column<byte[]>(type: "bytea", nullable: false),
                    Version = table.Column<string>(type: "text", nullable: false),
                    AttemptIndex = table.Column<int>(type: "integer", nullable: false),
                    PhotoIndex = table.Column<int>(type: "integer", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    PromptsId = table.Column<Guid>(type: "uuid", nullable: false),
                    Url = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Illustrations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IllustrationsPrompts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Url = table.Column<string>(type: "text", nullable: false),
                    Lyrics = table.Column<string>(type: "text", nullable: false),
                    Prompts = table.Column<string>(type: "jsonb", nullable: false),
                    Version = table.Column<string>(type: "character varying(100)", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Trace = table.Column<string>(type: "jsonb", nullable: false),
                    SpecialSystemMessage = table.Column<int>(type: "integer", nullable: true),
                    CustomSystemMessage = table.Column<string>(type: "text", nullable: true),
                    AlterationKey = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IllustrationsPrompts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IllustrationCopies",
                columns: table => new
                {
                    IllustrationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Size = table.Column<int>(type: "integer", nullable: false),
                    Image = table.Column<byte[]>(type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IllustrationCopies", x => new { x.IllustrationId, x.Size });
                    table.ForeignKey(
                        name: "FK_IllustrationCopies_Illustrations_IllustrationId",
                        column: x => x.IllustrationId,
                        principalTable: "Illustrations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Illustrations_Url",
                table: "Illustrations",
                column: "Url");

            migrationBuilder.CreateIndex(
                name: "IX_IllustrationsPrompts_Url",
                table: "IllustrationsPrompts",
                column: "Url");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IllustrationCopies");

            migrationBuilder.DropTable(
                name: "IllustrationsPrompts");

            migrationBuilder.DropTable(
                name: "Illustrations");
        }
    }
}
