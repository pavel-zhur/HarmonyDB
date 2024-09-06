using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneShelf.Videos.App.Migrations.VideosDatabaseMigrations
{
    /// <inheritdoc />
    public partial class Albums : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Albums",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Albums", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AlbumConstraints",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AlbumId = table.Column<int>(type: "int", nullable: false),
                    TopicId = table.Column<int>(type: "int", nullable: true),
                    MessageSelectedType = table.Column<int>(type: "int", nullable: true),
                    IsSquare = table.Column<bool>(type: "bit", nullable: false),
                    Before = table.Column<DateTime>(type: "datetime2", nullable: true),
                    After = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlbumConstraints", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AlbumConstraints_Albums_AlbumId",
                        column: x => x.AlbumId,
                        principalTable: "Albums",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AlbumConstraints_Topics_TopicId",
                        column: x => x.TopicId,
                        principalTable: "Topics",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_AlbumConstraints_AlbumId",
                table: "AlbumConstraints",
                column: "AlbumId");

            migrationBuilder.CreateIndex(
                name: "IX_AlbumConstraints_TopicId",
                table: "AlbumConstraints",
                column: "TopicId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AlbumConstraints");

            migrationBuilder.DropTable(
                name: "Albums");
        }
    }
}
