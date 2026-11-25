using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HarmonyDB.Shelf.Database.Migrations
{
    /// <inheritdoc />
    public partial class ShareLinksCarryAccess : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_share_links_kind_playlist_id_song_id_chords_id",
                table: "share_links");

            migrationBuilder.AddColumn<List<string>>(
                name: "carried_keys",
                table: "share_links",
                type: "text[]",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_share_links_chords_id_created_by_id",
                table: "share_links",
                columns: new[] { "chords_id", "created_by_id" },
                unique: true,
                filter: "carried_keys IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_share_links_kind_playlist_id_song_id_chords_id",
                table: "share_links",
                columns: new[] { "kind", "playlist_id", "song_id", "chords_id" },
                unique: true,
                filter: "carried_keys IS NULL")
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.AddCheckConstraint(
                name: "ck_share_links_carried_keys",
                table: "share_links",
                sql: "carried_keys IS NULL OR kind = 'chords'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_share_links_chords_id_created_by_id",
                table: "share_links");

            migrationBuilder.DropIndex(
                name: "ix_share_links_kind_playlist_id_song_id_chords_id",
                table: "share_links");

            migrationBuilder.DropCheckConstraint(
                name: "ck_share_links_carried_keys",
                table: "share_links");

            migrationBuilder.DropColumn(
                name: "carried_keys",
                table: "share_links");

            migrationBuilder.CreateIndex(
                name: "ix_share_links_kind_playlist_id_song_id_chords_id",
                table: "share_links",
                columns: new[] { "kind", "playlist_id", "song_id", "chords_id" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);
        }
    }
}
