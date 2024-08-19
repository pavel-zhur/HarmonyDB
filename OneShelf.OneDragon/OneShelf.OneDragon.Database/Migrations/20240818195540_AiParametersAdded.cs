using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneShelf.OneDragon.Database.Migrations
{
    /// <inheritdoc />
    public partial class AiParametersAdded : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DalleVersion",
                table: "AiParameters",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<float>(
                name: "FrequencyPenalty",
                table: "AiParameters",
                type: "real",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GptVersion",
                table: "AiParameters",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<float>(
                name: "PresencePenalty",
                table: "AiParameters",
                type: "real",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SystemMessage",
                table: "AiParameters",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DalleVersion",
                table: "AiParameters");

            migrationBuilder.DropColumn(
                name: "FrequencyPenalty",
                table: "AiParameters");

            migrationBuilder.DropColumn(
                name: "GptVersion",
                table: "AiParameters");

            migrationBuilder.DropColumn(
                name: "PresencePenalty",
                table: "AiParameters");

            migrationBuilder.DropColumn(
                name: "SystemMessage",
                table: "AiParameters");
        }
    }
}
