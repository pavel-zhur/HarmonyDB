using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneShelf.Videos.Database.Migrations.VideosDatabaseMigrations
{
    /// <inheritdoc />
    public partial class Media1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_StaticMessages",
                table: "StaticMessages");

            migrationBuilder.DropIndex(
                name: "IX_StaticMessages_StaticChatId_Id",
                table: "StaticMessages");

            migrationBuilder.DropColumn(
                name: "DatabaseStaticMessageId",
                table: "StaticMessages");

            migrationBuilder.AddPrimaryKey(
                name: "PK_StaticMessages",
                table: "StaticMessages",
                columns: new[] { "StaticChatId", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_StaticMessages",
                table: "StaticMessages");

            migrationBuilder.AddColumn<int>(
                name: "DatabaseStaticMessageId",
                table: "StaticMessages",
                type: "int",
                nullable: false,
                defaultValue: 0)
                .Annotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AddPrimaryKey(
                name: "PK_StaticMessages",
                table: "StaticMessages",
                column: "DatabaseStaticMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_StaticMessages_StaticChatId_Id",
                table: "StaticMessages",
                columns: new[] { "StaticChatId", "Id" },
                unique: true);
        }
    }
}
