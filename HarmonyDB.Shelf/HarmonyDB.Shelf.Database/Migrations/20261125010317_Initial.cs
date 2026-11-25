using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace HarmonyDB.Shelf.Database.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateVersionFunction();

            migrationBuilder.CreateTable(
                name: "accounts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    display_name = table.Column<string>(type: "text", nullable: false),
                    picture_url = table.Column<string>(type: "text", nullable: true),
                    locale = table.Column<string>(type: "text", nullable: true),
                    system_name = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_seen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_accounts", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "audit_log",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    actor = table.Column<string>(type: "text", nullable: false),
                    action = table.Column<string>(type: "text", nullable: false),
                    data = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_audit_log", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "chords",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_id = table.Column<string>(type: "text", nullable: false),
                    title = table.Column<string>(type: "text", nullable: true),
                    artists = table.Column<List<string>>(type: "text[]", nullable: false),
                    source_key = table.Column<string>(type: "text", nullable: true),
                    source_title = table.Column<string>(type: "text", nullable: true),
                    source_uri = table.Column<string>(type: "text", nullable: true),
                    source_icon = table.Column<string>(type: "text", nullable: true),
                    kinds = table.Column<List<string>>(type: "text[]", nullable: false),
                    header_updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    recordings_searched_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_chords", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "oauth_clients",
                columns: table => new
                {
                    client_id = table.Column<string>(type: "text", nullable: false),
                    client_name = table.Column<string>(type: "text", nullable: false),
                    redirect_uris = table.Column<List<string>>(type: "text[]", nullable: false),
                    grant_types = table.Column<List<string>>(type: "text[]", nullable: false),
                    token_endpoint_auth_method = table.Column<string>(type: "text", nullable: false),
                    client_secret_hash = table.Column<byte[]>(type: "bytea", nullable: true),
                    client_uri = table.Column<string>(type: "text", nullable: true),
                    logo_uri = table.Column<string>(type: "text", nullable: true),
                    software_id = table.Column<string>(type: "text", nullable: true),
                    software_version = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_used_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_oauth_clients", x => x.client_id);
                });

            migrationBuilder.CreateTable(
                name: "outbox",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    kind = table.Column<string>(type: "text", nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    next_attempt_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_error = table.Column<string>(type: "text", nullable: true),
                    processed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_outbox", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "recordings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    service = table.Column<string>(type: "text", nullable: false),
                    service_id = table.Column<string>(type: "text", nullable: false),
                    title = table.Column<string>(type: "text", nullable: true),
                    artists = table.Column<List<string>>(type: "text[]", nullable: false),
                    duration_seconds = table.Column<double>(type: "double precision", nullable: true),
                    cover_url = table.Column<string>(type: "text", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_recordings", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "account_logins",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider = table.Column<string>(type: "text", nullable: false),
                    subject = table.Column<string>(type: "text", nullable: false),
                    display_name = table.Column<string>(type: "text", nullable: true),
                    username = table.Column<string>(type: "text", nullable: true),
                    email = table.Column<string>(type: "text", nullable: true),
                    picture_url = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_sign_in_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_account_logins", x => x.id);
                    table.ForeignKey(
                        name: "fk_account_logins_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "account_permissions",
                columns: table => new
                {
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    permission = table.Column<string>(type: "text", nullable: false),
                    granted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    granted_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_account_permissions", x => new { x.account_id, x.permission });
                    table.ForeignKey(
                        name: "fk_account_permissions_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "account_settings",
                columns: table => new
                {
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    chords_scale = table.Column<double>(type: "double precision", nullable: false),
                    interface_scale = table.Column<double>(type: "double precision", nullable: false),
                    less_mode = table.Column<bool>(type: "boolean", nullable: false),
                    accidentals = table.Column<string>(type: "text", nullable: false),
                    show_degrees = table.Column<bool>(type: "boolean", nullable: false),
                    chord_superscripts = table.Column<bool>(type: "boolean", nullable: false),
                    theme = table.Column<string>(type: "text", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_account_settings", x => x.account_id);
                    table.CheckConstraint("ck_account_settings_chords_scale", "chords_scale > 0");
                    table.CheckConstraint("ck_account_settings_interface_scale", "interface_scale > 0");
                    table.ForeignKey(
                        name: "fk_account_settings_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "jobs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "text", nullable: false),
                    subject_id = table.Column<string>(type: "text", nullable: true),
                    state = table.Column<string>(type: "text", nullable: false),
                    progress = table.Column<double>(type: "double precision", nullable: true),
                    stage = table.Column<string>(type: "text", nullable: true),
                    input = table.Column<string>(type: "jsonb", nullable: false),
                    result = table.Column<string>(type: "jsonb", nullable: true),
                    error_code = table.Column<string>(type: "text", nullable: true),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    locked_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    finished_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_jobs", x => x.id);
                    table.ForeignKey(
                        name: "fk_jobs_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "music_links",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    service = table.Column<string>(type: "text", nullable: false),
                    credential_kind = table.Column<string>(type: "text", nullable: false),
                    ciphertext = table.Column<byte[]>(type: "bytea", nullable: false),
                    nonce = table.Column<byte[]>(type: "bytea", nullable: false),
                    tag = table.Column<byte[]>(type: "bytea", nullable: false),
                    key_version = table.Column<int>(type: "integer", nullable: false),
                    external_name = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_used_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_music_links", x => x.id);
                    table.ForeignKey(
                        name: "fk_music_links_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "personal_keys",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    key_hash = table.Column<byte[]>(type: "bytea", nullable: false),
                    prefix = table.Column<string>(type: "text", nullable: false),
                    permissions = table.Column<List<string>>(type: "text[]", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_used_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_personal_keys", x => x.id);
                    table.ForeignKey(
                        name: "fk_personal_keys_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "playlists",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    access = table.Column<string>(type: "text", nullable: false),
                    add_policy = table.Column<string>(type: "text", nullable: false),
                    search_first = table.Column<string>(type: "text", nullable: true),
                    system_kind = table.Column<string>(type: "text", nullable: false),
                    origin_service = table.Column<string>(type: "text", nullable: true),
                    origin_playlist_id = table.Column<string>(type: "text", nullable: true),
                    origin_url = table.Column<string>(type: "text", nullable: true),
                    imported_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    last_synced_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_playlists", x => x.id);
                    table.CheckConstraint("ck_playlists_origin", "(origin_service IS NULL) = (origin_playlist_id IS NULL)");
                    table.ForeignKey(
                        name: "fk_playlists_accounts_imported_by_id",
                        column: x => x.imported_by_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_playlists_accounts_owner_id",
                        column: x => x.owner_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<byte[]>(type: "bytea", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    signed_in_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_seen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    user_agent = table.Column<string>(type: "text", nullable: true),
                    ip_address = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sessions", x => x.id);
                    table.ForeignKey(
                        name: "fk_sessions_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "marks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    chords_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_marks", x => x.id);
                    table.ForeignKey(
                        name: "fk_marks_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_marks_chords_chords_id",
                        column: x => x.chords_id,
                        principalTable: "chords",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "oauth_grants",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_id = table.Column<string>(type: "text", nullable: false),
                    permissions = table.Column<List<string>>(type: "text[]", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_used_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_oauth_grants", x => x.id);
                    table.ForeignKey(
                        name: "fk_oauth_grants_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_oauth_grants_oauth_clients_client_id",
                        column: x => x.client_id,
                        principalTable: "oauth_clients",
                        principalColumn: "client_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "analyses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_id = table.Column<string>(type: "text", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    artists = table.Column<List<string>>(type: "text[]", nullable: false),
                    recording_id = table.Column<Guid>(type: "uuid", nullable: true),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: true),
                    duration_seconds = table.Column<double>(type: "double precision", nullable: true),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_analyses", x => x.id);
                    table.ForeignKey(
                        name: "fk_analyses_accounts_owner_id",
                        column: x => x.owner_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_analyses_recordings_recording_id",
                        column: x => x.recording_id,
                        principalTable: "recordings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "chords_recordings",
                columns: table => new
                {
                    chords_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recording_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rank = table.Column<int>(type: "integer", nullable: false),
                    found_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_chords_recordings", x => new { x.chords_id, x.recording_id });
                    table.ForeignKey(
                        name: "fk_chords_recordings_chords_chords_id",
                        column: x => x.chords_id,
                        principalTable: "chords",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_chords_recordings_recordings_recording_id",
                        column: x => x.recording_id,
                        principalTable: "recordings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "job_outputs",
                columns: table => new
                {
                    job_id = table.Column<Guid>(type: "uuid", nullable: false),
                    file_name = table.Column<string>(type: "text", nullable: false),
                    content_type = table.Column<string>(type: "text", nullable: false),
                    data = table.Column<byte[]>(type: "bytea", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_job_outputs", x => x.job_id);
                    table.ForeignKey(
                        name: "fk_job_outputs_jobs_job_id",
                        column: x => x.job_id,
                        principalTable: "jobs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "playlist_import_tombstones",
                columns: table => new
                {
                    playlist_id = table.Column<Guid>(type: "uuid", nullable: false),
                    origin_track_id = table.Column<string>(type: "text", nullable: false),
                    removed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_playlist_import_tombstones", x => new { x.playlist_id, x.origin_track_id });
                    table.ForeignKey(
                        name: "fk_playlist_import_tombstones_playlists_playlist_id",
                        column: x => x.playlist_id,
                        principalTable: "playlists",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "playlist_invitations",
                columns: table => new
                {
                    token = table.Column<string>(type: "text", nullable: false),
                    playlist_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "text", nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    max_uses = table.Column<int>(type: "integer", nullable: true),
                    uses = table.Column<int>(type: "integer", nullable: false),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_playlist_invitations", x => x.token);
                    table.CheckConstraint("ck_playlist_invitations_role", "role IN ('member', 'admin')");
                    table.CheckConstraint("ck_playlist_invitations_uses", "uses >= 0 AND (max_uses IS NULL OR (max_uses > 0 AND uses <= max_uses))");
                    table.ForeignKey(
                        name: "fk_playlist_invitations_accounts_created_by_id",
                        column: x => x.created_by_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_playlist_invitations_playlists_playlist_id",
                        column: x => x.playlist_id,
                        principalTable: "playlists",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "playlist_members",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    playlist_id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "text", nullable: false),
                    show_in_library = table.Column<bool>(type: "boolean", nullable: false),
                    joined_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_playlist_members", x => x.id);
                    table.ForeignKey(
                        name: "fk_playlist_members_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_playlist_members_playlists_playlist_id",
                        column: x => x.playlist_id,
                        principalTable: "playlists",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "playlist_songs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    playlist_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    artists = table.Column<List<string>>(type: "text[]", nullable: false),
                    position = table.Column<double>(type: "double precision", nullable: false),
                    added_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    added_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    origin_track_id = table.Column<string>(type: "text", nullable: true),
                    version = table.Column<long>(type: "bigint", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_playlist_songs", x => x.id);
                    table.ForeignKey(
                        name: "fk_playlist_songs_accounts_added_by_id",
                        column: x => x.added_by_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_playlist_songs_playlists_playlist_id",
                        column: x => x.playlist_id,
                        principalTable: "playlists",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "transpositions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    chords_id = table.Column<Guid>(type: "uuid", nullable: false),
                    playlist_id = table.Column<Guid>(type: "uuid", nullable: true),
                    semitones = table.Column<int>(type: "integer", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_transpositions", x => x.id);
                    table.CheckConstraint("ck_transpositions_semitones", "semitones BETWEEN -11 AND 11");
                    table.ForeignKey(
                        name: "fk_transpositions_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_transpositions_chords_chords_id",
                        column: x => x.chords_id,
                        principalTable: "chords",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_transpositions_playlists_playlist_id",
                        column: x => x.playlist_id,
                        principalTable: "playlists",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sign_in_flows",
                columns: table => new
                {
                    state = table.Column<string>(type: "text", nullable: false),
                    provider = table.Column<string>(type: "text", nullable: false),
                    purpose = table.Column<string>(type: "text", nullable: false),
                    session_id = table.Column<Guid>(type: "uuid", nullable: true),
                    code_verifier = table.Column<string>(type: "text", nullable: true),
                    nonce = table.Column<string>(type: "text", nullable: true),
                    return_path = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    completed_login_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sign_in_flows", x => x.state);
                    table.ForeignKey(
                        name: "fk_sign_in_flows_account_logins_completed_login_id",
                        column: x => x.completed_login_id,
                        principalTable: "account_logins",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_sign_in_flows_sessions_session_id",
                        column: x => x.session_id,
                        principalTable: "sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "oauth_authorization_codes",
                columns: table => new
                {
                    code_hash = table.Column<byte[]>(type: "bytea", nullable: false),
                    grant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    family_id = table.Column<Guid>(type: "uuid", nullable: false),
                    redirect_uri = table.Column<string>(type: "text", nullable: false),
                    code_challenge = table.Column<string>(type: "text", nullable: false),
                    code_challenge_method = table.Column<string>(type: "text", nullable: false),
                    resource = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    consumed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_oauth_authorization_codes", x => x.code_hash);
                    table.ForeignKey(
                        name: "fk_oauth_authorization_codes_o_auth_grants_grant_id",
                        column: x => x.grant_id,
                        principalTable: "oauth_grants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "oauth_tokens",
                columns: table => new
                {
                    token_hash = table.Column<byte[]>(type: "bytea", nullable: false),
                    grant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "text", nullable: false),
                    family_id = table.Column<Guid>(type: "uuid", nullable: false),
                    resource = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    consumed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_oauth_tokens", x => x.token_hash);
                    table.ForeignKey(
                        name: "fk_oauth_tokens_oauth_grants_grant_id",
                        column: x => x.grant_id,
                        principalTable: "oauth_grants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "analysis_contents",
                columns: table => new
                {
                    analysis_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "text", nullable: false),
                    content = table.Column<string>(type: "jsonb", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_analysis_contents", x => new { x.analysis_id, x.kind });
                    table.ForeignKey(
                        name: "fk_analysis_contents_analyses_analysis_id",
                        column: x => x.analysis_id,
                        principalTable: "analyses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "analysis_files",
                columns: table => new
                {
                    analysis_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    blob_key = table.Column<string>(type: "text", nullable: false),
                    content_type = table.Column<string>(type: "text", nullable: false),
                    size = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_analysis_files", x => new { x.analysis_id, x.name });
                    table.ForeignKey(
                        name: "fk_analysis_files_analyses_analysis_id",
                        column: x => x.analysis_id,
                        principalTable: "analyses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "notes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    chords_id = table.Column<Guid>(type: "uuid", nullable: true),
                    song_id = table.Column<Guid>(type: "uuid", nullable: true),
                    text = table.Column<string>(type: "text", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notes", x => x.id);
                    table.CheckConstraint("ck_notes_one_target", "num_nonnulls(chords_id, song_id) = 1");
                    table.ForeignKey(
                        name: "fk_notes_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_notes_chords_chords_id",
                        column: x => x.chords_id,
                        principalTable: "chords",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_notes_playlist_songs_song_id",
                        column: x => x.song_id,
                        principalTable: "playlist_songs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "playlist_song_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    song_id = table.Column<Guid>(type: "uuid", nullable: false),
                    chords_id = table.Column<Guid>(type: "uuid", nullable: true),
                    recording_id = table.Column<Guid>(type: "uuid", nullable: true),
                    position = table.Column<double>(type: "double precision", nullable: false),
                    added_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    added_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_playlist_song_items", x => x.id);
                    table.UniqueConstraint("ak_playlist_song_items_id_song_id", x => new { x.id, x.song_id });
                    table.CheckConstraint("ck_playlist_song_items_one_target", "num_nonnulls(chords_id, recording_id) = 1");
                    table.ForeignKey(
                        name: "fk_playlist_song_items_accounts_added_by_id",
                        column: x => x.added_by_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_playlist_song_items_chords_chords_id",
                        column: x => x.chords_id,
                        principalTable: "chords",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_playlist_song_items_playlist_songs_song_id",
                        column: x => x.song_id,
                        principalTable: "playlist_songs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_playlist_song_items_recordings_recording_id",
                        column: x => x.recording_id,
                        principalTable: "recordings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "recent",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    chords_id = table.Column<Guid>(type: "uuid", nullable: true),
                    song_id = table.Column<Guid>(type: "uuid", nullable: true),
                    opened_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_recent", x => x.id);
                    table.CheckConstraint("ck_recent_target", "num_nonnulls(chords_id, song_id) >= 1");
                    table.ForeignKey(
                        name: "fk_recent_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_recent_chords_chords_id",
                        column: x => x.chords_id,
                        principalTable: "chords",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_recent_playlist_songs_song_id",
                        column: x => x.song_id,
                        principalTable: "playlist_songs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "share_links",
                columns: table => new
                {
                    token = table.Column<string>(type: "text", nullable: false),
                    kind = table.Column<string>(type: "text", nullable: false),
                    playlist_id = table.Column<Guid>(type: "uuid", nullable: true),
                    song_id = table.Column<Guid>(type: "uuid", nullable: true),
                    chords_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_share_links", x => x.token);
                    table.CheckConstraint("ck_share_links_target", "(kind = 'playlist' AND playlist_id IS NOT NULL AND song_id IS NULL AND chords_id IS NULL)\nOR (kind = 'song' AND song_id IS NOT NULL AND playlist_id IS NULL)\nOR (kind = 'chords' AND chords_id IS NOT NULL AND playlist_id IS NULL AND song_id IS NULL)");
                    table.ForeignKey(
                        name: "fk_share_links_accounts_created_by_id",
                        column: x => x.created_by_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_share_links_chords_chords_id",
                        column: x => x.chords_id,
                        principalTable: "chords",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_share_links_playlist_songs_song_id",
                        column: x => x.song_id,
                        principalTable: "playlist_songs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_share_links_playlists_playlist_id",
                        column: x => x.playlist_id,
                        principalTable: "playlists",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "playlist_song_prefs",
                columns: table => new
                {
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    song_id = table.Column<Guid>(type: "uuid", nullable: false),
                    main_item_id = table.Column<Guid>(type: "uuid", nullable: true),
                    recording_item_id = table.Column<Guid>(type: "uuid", nullable: true),
                    version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_playlist_song_prefs", x => new { x.account_id, x.song_id });
                    table.ForeignKey(
                        name: "fk_playlist_song_prefs_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_playlist_song_prefs_playlist_song_items_main_item_id_song_id",
                        columns: x => new { x.main_item_id, x.song_id },
                        principalTable: "playlist_song_items",
                        principalColumns: new[] { "id", "song_id" });
                    table.ForeignKey(
                        name: "fk_playlist_song_prefs_playlist_song_items_recording_item_id_s",
                        columns: x => new { x.recording_item_id, x.song_id },
                        principalTable: "playlist_song_items",
                        principalColumns: new[] { "id", "song_id" });
                    table.ForeignKey(
                        name: "fk_playlist_song_prefs_playlist_songs_song_id",
                        column: x => x.song_id,
                        principalTable: "playlist_songs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_account_logins_account_id",
                table: "account_logins",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "ix_account_logins_provider_subject",
                table: "account_logins",
                columns: new[] { "provider", "subject" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_account_permissions_permission",
                table: "account_permissions",
                column: "permission");

            migrationBuilder.CreateIndex(
                name: "ix_accounts_system_name",
                table: "accounts",
                column: "system_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_analyses_owner_id",
                table: "analyses",
                column: "owner_id");

            migrationBuilder.CreateIndex(
                name: "ix_analyses_recording_id",
                table: "analyses",
                column: "recording_id");

            migrationBuilder.CreateIndex(
                name: "ix_analyses_source_id",
                table: "analyses",
                column: "source_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_audit_log_account_id_at",
                table: "audit_log",
                columns: new[] { "account_id", "at" });

            migrationBuilder.CreateIndex(
                name: "ix_chords_source_id",
                table: "chords",
                column: "source_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_chords_recordings_recording_id",
                table: "chords_recordings",
                column: "recording_id");

            migrationBuilder.CreateIndex(
                name: "ix_job_outputs_expires_at",
                table: "job_outputs",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "ix_jobs_account_id_created_at",
                table: "jobs",
                columns: new[] { "account_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_jobs_kind_subject_id",
                table: "jobs",
                columns: new[] { "kind", "subject_id" });

            migrationBuilder.CreateIndex(
                name: "ix_jobs_state_created_at",
                table: "jobs",
                columns: new[] { "state", "created_at" },
                filter: "state IN ('queued', 'running')");

            migrationBuilder.CreateIndex(
                name: "ix_marks_account_id_chords_id_kind",
                table: "marks",
                columns: new[] { "account_id", "chords_id", "kind" },
                unique: true,
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_marks_chords_id",
                table: "marks",
                column: "chords_id");

            migrationBuilder.CreateIndex(
                name: "ix_marks_version",
                table: "marks",
                column: "version");

            migrationBuilder.CreateIndex(
                name: "ix_music_links_account_id_service",
                table: "music_links",
                columns: new[] { "account_id", "service" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_notes_account_id_chords_id",
                table: "notes",
                columns: new[] { "account_id", "chords_id" },
                unique: true,
                filter: "chords_id IS NOT NULL AND deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_notes_account_id_song_id",
                table: "notes",
                columns: new[] { "account_id", "song_id" },
                unique: true,
                filter: "song_id IS NOT NULL AND deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_notes_chords_id",
                table: "notes",
                column: "chords_id");

            migrationBuilder.CreateIndex(
                name: "ix_notes_song_id",
                table: "notes",
                column: "song_id");

            migrationBuilder.CreateIndex(
                name: "ix_notes_version",
                table: "notes",
                column: "version");

            migrationBuilder.CreateIndex(
                name: "ix_oauth_authorization_codes_expires_at",
                table: "oauth_authorization_codes",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "ix_oauth_authorization_codes_family_id",
                table: "oauth_authorization_codes",
                column: "family_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_oauth_authorization_codes_grant_id",
                table: "oauth_authorization_codes",
                column: "grant_id");

            migrationBuilder.CreateIndex(
                name: "ix_oauth_grants_account_id_client_id",
                table: "oauth_grants",
                columns: new[] { "account_id", "client_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_oauth_grants_client_id",
                table: "oauth_grants",
                column: "client_id");

            migrationBuilder.CreateIndex(
                name: "ix_oauth_tokens_expires_at",
                table: "oauth_tokens",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "ix_oauth_tokens_family_id",
                table: "oauth_tokens",
                column: "family_id");

            migrationBuilder.CreateIndex(
                name: "ix_oauth_tokens_grant_id",
                table: "oauth_tokens",
                column: "grant_id");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_next_attempt_at",
                table: "outbox",
                column: "next_attempt_at",
                filter: "processed_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_personal_keys_account_id",
                table: "personal_keys",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "ix_personal_keys_key_hash",
                table: "personal_keys",
                column: "key_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_playlist_invitations_created_by_id",
                table: "playlist_invitations",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_playlist_invitations_playlist_id",
                table: "playlist_invitations",
                column: "playlist_id");

            migrationBuilder.CreateIndex(
                name: "ix_playlist_members_account_id",
                table: "playlist_members",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "ix_playlist_members_playlist_id",
                table: "playlist_members",
                column: "playlist_id",
                unique: true,
                filter: "role = 'owner' AND deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_playlist_members_playlist_id_account_id",
                table: "playlist_members",
                columns: new[] { "playlist_id", "account_id" },
                unique: true,
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_playlist_members_version",
                table: "playlist_members",
                column: "version");

            migrationBuilder.CreateIndex(
                name: "ix_playlist_song_items_added_by_id",
                table: "playlist_song_items",
                column: "added_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_playlist_song_items_chords_id",
                table: "playlist_song_items",
                column: "chords_id");

            migrationBuilder.CreateIndex(
                name: "ix_playlist_song_items_recording_id",
                table: "playlist_song_items",
                column: "recording_id");

            migrationBuilder.CreateIndex(
                name: "ix_playlist_song_items_song_id_chords_id",
                table: "playlist_song_items",
                columns: new[] { "song_id", "chords_id" },
                unique: true,
                filter: "chords_id IS NOT NULL AND deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_playlist_song_items_song_id_recording_id",
                table: "playlist_song_items",
                columns: new[] { "song_id", "recording_id" },
                unique: true,
                filter: "recording_id IS NOT NULL AND deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_playlist_song_items_version",
                table: "playlist_song_items",
                column: "version");

            migrationBuilder.CreateIndex(
                name: "ix_playlist_song_prefs_main_item_id_song_id",
                table: "playlist_song_prefs",
                columns: new[] { "main_item_id", "song_id" });

            migrationBuilder.CreateIndex(
                name: "ix_playlist_song_prefs_recording_item_id_song_id",
                table: "playlist_song_prefs",
                columns: new[] { "recording_item_id", "song_id" });

            migrationBuilder.CreateIndex(
                name: "ix_playlist_song_prefs_song_id",
                table: "playlist_song_prefs",
                column: "song_id");

            migrationBuilder.CreateIndex(
                name: "ix_playlist_song_prefs_version",
                table: "playlist_song_prefs",
                column: "version");

            migrationBuilder.CreateIndex(
                name: "ix_playlist_songs_added_by_id",
                table: "playlist_songs",
                column: "added_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_playlist_songs_playlist_id_origin_track_id",
                table: "playlist_songs",
                columns: new[] { "playlist_id", "origin_track_id" },
                unique: true,
                filter: "origin_track_id IS NOT NULL AND deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_playlist_songs_playlist_id_position",
                table: "playlist_songs",
                columns: new[] { "playlist_id", "position" });

            migrationBuilder.CreateIndex(
                name: "ix_playlist_songs_version",
                table: "playlist_songs",
                column: "version");

            migrationBuilder.CreateIndex(
                name: "ix_playlists_access",
                table: "playlists",
                column: "access",
                filter: "access = 'public' AND deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_playlists_imported_by_id",
                table: "playlists",
                column: "imported_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_playlists_owner_id",
                table: "playlists",
                column: "owner_id");

            migrationBuilder.CreateIndex(
                name: "ix_playlists_owner_id_system_kind",
                table: "playlists",
                columns: new[] { "owner_id", "system_kind" },
                unique: true,
                filter: "system_kind <> 'none' AND deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_playlists_version",
                table: "playlists",
                column: "version");

            migrationBuilder.CreateIndex(
                name: "ix_recent_account_id_chords_id_song_id",
                table: "recent",
                columns: new[] { "account_id", "chords_id", "song_id" },
                unique: true,
                filter: "deleted_at IS NULL")
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ix_recent_account_id_opened_at",
                table: "recent",
                columns: new[] { "account_id", "opened_at" });

            migrationBuilder.CreateIndex(
                name: "ix_recent_chords_id",
                table: "recent",
                column: "chords_id");

            migrationBuilder.CreateIndex(
                name: "ix_recent_song_id",
                table: "recent",
                column: "song_id");

            migrationBuilder.CreateIndex(
                name: "ix_recent_version",
                table: "recent",
                column: "version");

            migrationBuilder.CreateIndex(
                name: "ix_recordings_service_service_id",
                table: "recordings",
                columns: new[] { "service", "service_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sessions_account_id",
                table: "sessions",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "ix_sessions_expires_at",
                table: "sessions",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "ix_sessions_token_hash",
                table: "sessions",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_share_links_chords_id",
                table: "share_links",
                column: "chords_id");

            migrationBuilder.CreateIndex(
                name: "ix_share_links_created_by_id",
                table: "share_links",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_share_links_kind_playlist_id_song_id_chords_id",
                table: "share_links",
                columns: new[] { "kind", "playlist_id", "song_id", "chords_id" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ix_share_links_playlist_id",
                table: "share_links",
                column: "playlist_id");

            migrationBuilder.CreateIndex(
                name: "ix_share_links_song_id",
                table: "share_links",
                column: "song_id");

            migrationBuilder.CreateIndex(
                name: "ix_sign_in_flows_completed_login_id",
                table: "sign_in_flows",
                column: "completed_login_id");

            migrationBuilder.CreateIndex(
                name: "ix_sign_in_flows_expires_at",
                table: "sign_in_flows",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "ix_sign_in_flows_session_id",
                table: "sign_in_flows",
                column: "session_id");

            migrationBuilder.CreateIndex(
                name: "ix_transpositions_account_id_chords_id_playlist_id",
                table: "transpositions",
                columns: new[] { "account_id", "chords_id", "playlist_id" },
                unique: true,
                filter: "deleted_at IS NULL")
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ix_transpositions_chords_id",
                table: "transpositions",
                column: "chords_id");

            migrationBuilder.CreateIndex(
                name: "ix_transpositions_playlist_id_chords_id",
                table: "transpositions",
                columns: new[] { "playlist_id", "chords_id" });

            migrationBuilder.CreateIndex(
                name: "ix_transpositions_version",
                table: "transpositions",
                column: "version");

            foreach (var table in new[]
                     {
                         "playlists", "playlist_members", "playlist_songs", "playlist_song_items", "playlist_song_prefs",
                         "marks", "transpositions", "notes", "recent",
                     })
            {
                migrationBuilder.AddVersionTrigger(table);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "account_permissions");

            migrationBuilder.DropTable(
                name: "account_settings");

            migrationBuilder.DropTable(
                name: "analysis_contents");

            migrationBuilder.DropTable(
                name: "analysis_files");

            migrationBuilder.DropTable(
                name: "audit_log");

            migrationBuilder.DropTable(
                name: "chords_recordings");

            migrationBuilder.DropTable(
                name: "job_outputs");

            migrationBuilder.DropTable(
                name: "marks");

            migrationBuilder.DropTable(
                name: "music_links");

            migrationBuilder.DropTable(
                name: "notes");

            migrationBuilder.DropTable(
                name: "oauth_authorization_codes");

            migrationBuilder.DropTable(
                name: "oauth_tokens");

            migrationBuilder.DropTable(
                name: "outbox");

            migrationBuilder.DropTable(
                name: "personal_keys");

            migrationBuilder.DropTable(
                name: "playlist_import_tombstones");

            migrationBuilder.DropTable(
                name: "playlist_invitations");

            migrationBuilder.DropTable(
                name: "playlist_members");

            migrationBuilder.DropTable(
                name: "playlist_song_prefs");

            migrationBuilder.DropTable(
                name: "recent");

            migrationBuilder.DropTable(
                name: "share_links");

            migrationBuilder.DropTable(
                name: "sign_in_flows");

            migrationBuilder.DropTable(
                name: "transpositions");

            migrationBuilder.DropTable(
                name: "analyses");

            migrationBuilder.DropTable(
                name: "jobs");

            migrationBuilder.DropTable(
                name: "oauth_grants");

            migrationBuilder.DropTable(
                name: "playlist_song_items");

            migrationBuilder.DropTable(
                name: "account_logins");

            migrationBuilder.DropTable(
                name: "sessions");

            migrationBuilder.DropTable(
                name: "oauth_clients");

            migrationBuilder.DropTable(
                name: "chords");

            migrationBuilder.DropTable(
                name: "playlist_songs");

            migrationBuilder.DropTable(
                name: "recordings");

            migrationBuilder.DropTable(
                name: "playlists");

            migrationBuilder.DropTable(
                name: "accounts");

            migrationBuilder.DropVersionFunction();
        }
    }
}
