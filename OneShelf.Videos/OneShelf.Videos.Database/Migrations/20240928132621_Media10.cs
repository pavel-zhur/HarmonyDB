using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneShelf.Videos.Database.Migrations.VideosDatabaseMigrations
{
    /// <inheritdoc />
    public partial class Media10 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AlbumConstraints_StaticTopics_StaticTopicId",
                table: "AlbumConstraints");

            migrationBuilder.DropForeignKey(
                name: "FK_StaticMessages_StaticTopics_StaticTopicId",
                table: "StaticMessages");

            migrationBuilder.DropPrimaryKey(
                name: "PK_StaticTopics",
                table: "StaticTopics");

            migrationBuilder.DropIndex(
                name: "IX_StaticTopics_StaticChatId_RootMessageIdOr0",
                table: "StaticTopics");

            migrationBuilder.DropIndex(
                name: "IX_StaticMessages_StaticTopicId",
                table: "StaticMessages");

            migrationBuilder.DropIndex(
                name: "IX_AlbumConstraints_StaticTopicId",
                table: "AlbumConstraints");

            migrationBuilder.Sql("update StaticMessages set StaticTopicId = null");

            migrationBuilder.RenameColumn(
                name: "StaticTopicId",
                table: "StaticMessages",
                newName: "StaticTopicRootMessageIdOr0");

            migrationBuilder.AddColumn<int>(
                name: "Id2",
                table: "StaticTopics",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql("update StaticTopics set Id2 = Id");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "StaticTopics");

            migrationBuilder.RenameColumn("Id2",
                table: "StaticTopics",
                newName: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_StaticTopics",
                table: "StaticTopics",
                columns: new[] { "StaticChatId", "RootMessageIdOr0" });

            migrationBuilder.CreateIndex(
                name: "IX_StaticMessages_StaticChatId_StaticTopicRootMessageIdOr0",
                table: "StaticMessages",
                columns: new[] { "StaticChatId", "StaticTopicRootMessageIdOr0" });

            migrationBuilder.AddForeignKey(
                name: "FK_StaticMessages_StaticTopics_StaticChatId_StaticTopicRootMessageIdOr0",
                table: "StaticMessages",
                columns: new[] { "StaticChatId", "StaticTopicRootMessageIdOr0" },
                principalTable: "StaticTopics",
                principalColumns: new[] { "StaticChatId", "RootMessageIdOr0" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StaticMessages_StaticTopics_StaticChatId_StaticTopicRootMessageIdOr0",
                table: "StaticMessages");

            migrationBuilder.DropPrimaryKey(
                name: "PK_StaticTopics",
                table: "StaticTopics");

            migrationBuilder.DropIndex(
                name: "IX_StaticMessages_StaticChatId_StaticTopicRootMessageIdOr0",
                table: "StaticMessages");

            migrationBuilder.RenameColumn(
                name: "StaticTopicRootMessageIdOr0",
                table: "StaticMessages",
                newName: "StaticTopicId");

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "StaticTopics",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int")
                .Annotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AddPrimaryKey(
                name: "PK_StaticTopics",
                table: "StaticTopics",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_StaticTopics_StaticChatId_RootMessageIdOr0",
                table: "StaticTopics",
                columns: new[] { "StaticChatId", "RootMessageIdOr0" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StaticMessages_StaticTopicId",
                table: "StaticMessages",
                column: "StaticTopicId");

            migrationBuilder.CreateIndex(
                name: "IX_AlbumConstraints_StaticTopicId",
                table: "AlbumConstraints",
                column: "StaticTopicId");

            migrationBuilder.AddForeignKey(
                name: "FK_AlbumConstraints_StaticTopics_StaticTopicId",
                table: "AlbumConstraints",
                column: "StaticTopicId",
                principalTable: "StaticTopics",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_StaticMessages_StaticTopics_StaticTopicId",
                table: "StaticMessages",
                column: "StaticTopicId",
                principalTable: "StaticTopics",
                principalColumn: "Id");
        }
    }
}
