using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HarmonyDB.Shelf.Database.Migrations
{
    /// <inheritdoc />
    public partial class OAuthAuthorizationRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "client_name",
                table: "oauth_clients",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            // a client that gave no name was kept with an empty one before the column allowed none
            migrationBuilder.Sql("UPDATE oauth_clients SET client_name = NULL WHERE client_name = ''");

            migrationBuilder.CreateTable(
                name: "oauth_authorization_requests",
                columns: table => new
                {
                    id_hash = table.Column<byte[]>(type: "bytea", nullable: false),
                    client_id = table.Column<string>(type: "text", nullable: false),
                    redirect_uri = table.Column<string>(type: "text", nullable: false),
                    state = table.Column<string>(type: "text", nullable: true),
                    code_challenge = table.Column<string>(type: "text", nullable: false),
                    scopes = table.Column<List<string>>(type: "text[]", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    decided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_oauth_authorization_requests", x => x.id_hash);
                    table.ForeignKey(
                        name: "fk_oauth_authorization_requests_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_oauth_authorization_requests_oauth_clients_client_id",
                        column: x => x.client_id,
                        principalTable: "oauth_clients",
                        principalColumn: "client_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_oauth_authorization_requests_account_id",
                table: "oauth_authorization_requests",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "ix_oauth_authorization_requests_client_id",
                table: "oauth_authorization_requests",
                column: "client_id");

            migrationBuilder.CreateIndex(
                name: "ix_oauth_authorization_requests_expires_at",
                table: "oauth_authorization_requests",
                column: "expires_at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "oauth_authorization_requests");

            migrationBuilder.Sql("UPDATE oauth_clients SET client_name = '' WHERE client_name IS NULL");

            migrationBuilder.AlterColumn<string>(
                name: "client_name",
                table: "oauth_clients",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);
        }
    }
}
