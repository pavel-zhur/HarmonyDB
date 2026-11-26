using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HarmonyDB.Shelf.Database.Migrations
{
    /// <inheritdoc />
    public partial class ChordsMadeFrom : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "made_from_recording_id",
                table: "chords",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_chords_made_from_recording_id",
                table: "chords",
                column: "made_from_recording_id");

            migrationBuilder.AddForeignKey(
                name: "fk_chords_recordings_made_from_recording_id",
                table: "chords",
                column: "made_from_recording_id",
                principalTable: "recordings",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_chords_recordings_made_from_recording_id",
                table: "chords");

            migrationBuilder.DropIndex(
                name: "ix_chords_made_from_recording_id",
                table: "chords");

            migrationBuilder.DropColumn(
                name: "made_from_recording_id",
                table: "chords");
        }
    }
}
