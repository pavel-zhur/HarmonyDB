using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HarmonyDB.Shelf.Database.Migrations
{
    /// <inheritdoc />
    public partial class EntriesAddressRecentNames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "header_updated_at",
                table: "chords");

            migrationBuilder.DropColumn(
                name: "source_icon",
                table: "chords");

            migrationBuilder.DropColumn(
                name: "source_key",
                table: "chords");

            migrationBuilder.DropColumn(
                name: "source_title",
                table: "chords");

            migrationBuilder.DropColumn(
                name: "source_uri",
                table: "chords");

            migrationBuilder.AddColumn<List<string>>(
                name: "artists",
                table: "recent",
                type: "text[]",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "source_icon_url",
                table: "recent",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "source_key",
                table: "recent",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "source_title",
                table: "recent",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "title",
                table: "recent",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "address",
                table: "playlist_song_items",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "private",
                table: "playlist_song_items",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "artists",
                table: "recent");

            migrationBuilder.DropColumn(
                name: "source_icon_url",
                table: "recent");

            migrationBuilder.DropColumn(
                name: "source_key",
                table: "recent");

            migrationBuilder.DropColumn(
                name: "source_title",
                table: "recent");

            migrationBuilder.DropColumn(
                name: "title",
                table: "recent");

            migrationBuilder.DropColumn(
                name: "address",
                table: "playlist_song_items");

            migrationBuilder.DropColumn(
                name: "private",
                table: "playlist_song_items");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "header_updated_at",
                table: "chords",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "source_icon",
                table: "chords",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "source_key",
                table: "chords",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "source_title",
                table: "chords",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "source_uri",
                table: "chords",
                type: "text",
                nullable: true);
        }
    }
}
