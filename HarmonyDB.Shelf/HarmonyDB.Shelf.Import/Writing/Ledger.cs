using HarmonyDB.Shelf.Database;
using HarmonyDB.Shelf.Import.Planning;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using NpgsqlTypes;

namespace HarmonyDB.Shelf.Import.Writing;

/// <summary>
/// The import's own memory, in a schema of its own in the shelf's database (not a part of the shelf's model): what it
/// made (or found made) for each thing of the old shelf, by the thing's key, with the value it wrote; and the addresses
/// as the relay last resolved them. A later run reads it to update what it made and to leave alone what a person has
/// since removed or changed.
/// </summary>
public sealed class Ledger
{
    public const string Schema = "import_old_shelf";

    /// <summary>
    /// The entry whose value is when the last run read the addresses.
    /// </summary>
    public const string LastRunKey = "run:last";

    private readonly ShelfDbContext _db;
    private readonly Dictionary<string, Entry> _entries;
    private readonly Dictionary<string, Entry> _changed = new(StringComparer.Ordinal);

    private Ledger(ShelfDbContext db, Dictionary<string, Entry> entries)
    {
        _db = db;
        _entries = entries;
    }

    /// <summary>
    /// What was made for a thing: the row's id, and the value written into it (a title, a key, a mark's kind …).
    /// <see cref="Made"/> false: the row was there already, made by a person; it is never changed by the import.
    /// </summary>
    public sealed record Entry(Guid? Id, string? Value, bool Made = true);

    /// <summary>
    /// Makes the schema if it is not there, and reads the ledger. In the caller's transaction.
    /// </summary>
    public static async Task<Ledger> OpenAsync(ShelfDbContext db, CancellationToken cancellationToken)
    {
        await CreateAsync(db, cancellationToken);
        return await ReadAsync(db, cancellationToken);
    }

    /// <summary>
    /// Reads the ledger, which must be there (for the audit, which writes nothing).
    /// </summary>
    public static async Task<Ledger> ReadAsync(ShelfDbContext db, CancellationToken cancellationToken)
    {
        var entries = new Dictionary<string, Entry>(StringComparer.Ordinal);
        await using var command = Command(db, $"SELECT key, id, value, made FROM {Schema}.things");
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            entries[reader.GetString(0)] = new(reader.IsDBNull(1) ? null : reader.GetGuid(1), reader.IsDBNull(2) ? null : reader.GetString(2), reader.GetBoolean(3));

        return new(db, entries);
    }

    private static async Task CreateAsync(ShelfDbContext db, CancellationToken cancellationToken)
    {
        await db.Database.ExecuteSqlRawAsync($"""
            CREATE SCHEMA IF NOT EXISTS {Schema};
            CREATE TABLE IF NOT EXISTS {Schema}.things (
                key text PRIMARY KEY,
                id uuid,
                value text,
                at timestamptz NOT NULL);
            ALTER TABLE {Schema}.things ADD COLUMN IF NOT EXISTS made boolean NOT NULL DEFAULT TRUE;
            CREATE TABLE IF NOT EXISTS {Schema}.addresses (
                kind text NOT NULL,
                address text NOT NULL,
                source_id text,
                state text NOT NULL,
                checked_at timestamptz NOT NULL,
                PRIMARY KEY (kind, address));
            """, cancellationToken);
    }

    public Entry? Get(string key) => _entries.GetValueOrDefault(key);

    public void Set(string key, Guid? id, string? value, bool made = true)
    {
        var entry = new Entry(id, value, made);
        _entries[key] = entry;
        _changed[key] = entry;
    }

    public async Task SaveAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (_changed.Count == 0)
            return;

        var keys = _changed.Keys.ToArray();
        await using var command = Command(_db, $"""
            INSERT INTO {Schema}.things (key, id, value, made, at)
            SELECT x.key, x.id, x.value, x.made, @at FROM unnest(@keys, @ids, @values, @made) AS x(key, id, value, made)
            ON CONFLICT (key) DO UPDATE SET id = excluded.id, value = excluded.value, made = excluded.made, at = excluded.at
            """);
        command.Parameters.Add(new NpgsqlParameter("keys", NpgsqlDbType.Array | NpgsqlDbType.Text) { Value = keys });
        command.Parameters.Add(new NpgsqlParameter("ids", NpgsqlDbType.Array | NpgsqlDbType.Uuid) { Value = keys.Select(x => (object?)_changed[x].Id ?? DBNull.Value).ToArray() });
        command.Parameters.Add(new NpgsqlParameter("values", NpgsqlDbType.Array | NpgsqlDbType.Text) { Value = keys.Select(x => (object?)_changed[x].Value ?? DBNull.Value).ToArray() });
        command.Parameters.Add(new NpgsqlParameter("made", NpgsqlDbType.Array | NpgsqlDbType.Boolean) { Value = keys.Select(x => _changed[x].Made).ToArray() });
        command.Parameters.AddWithValue("at", now);
        await command.ExecuteNonQueryAsync(cancellationToken);
        _changed.Clear();
    }

    /// <summary>
    /// The addresses (kind "uri") as last resolved.
    /// </summary>
    /// <param name="checkedBy">Only the answers kept by then; null: all.</param>
    public static async Task<Dictionary<(string kind, string address), ResolvedAddress>> ReadAddressesAsync(ShelfDbContext db, CancellationToken cancellationToken, DateTimeOffset? checkedBy = null)
    {
        var result = new Dictionary<(string, string), ResolvedAddress>();
        await using var command = Command(db, $"SELECT kind, address, source_id, state FROM {Schema}.addresses WHERE @by IS NULL OR checked_at <= @by");
        command.Parameters.Add(new NpgsqlParameter("by", NpgsqlDbType.TimestampTz) { Value = (object?)checkedBy ?? DBNull.Value });
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            result[(reader.GetString(0), reader.GetString(1))] = new(reader.IsDBNull(2) ? null : reader.GetString(2), reader.GetString(3));
        return result;
    }

    public static async Task WriteAddressesAsync(ShelfDbContext db, string kind, IReadOnlyDictionary<string, ResolvedAddress> addresses, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (addresses.Count == 0)
            return;

        var keys = addresses.Keys.ToArray();
        await using var command = Command(db, $"""
            INSERT INTO {Schema}.addresses (kind, address, source_id, state, checked_at)
            SELECT @kind, x.address, x.source_id, x.state, @at FROM unnest(@addresses, @ids, @states) AS x(address, source_id, state)
            ON CONFLICT (kind, address) DO UPDATE SET source_id = excluded.source_id, state = excluded.state, checked_at = excluded.checked_at
            """);
        command.Parameters.AddWithValue("kind", kind);
        command.Parameters.Add(new NpgsqlParameter("addresses", NpgsqlDbType.Array | NpgsqlDbType.Text) { Value = keys });
        command.Parameters.Add(new NpgsqlParameter("ids", NpgsqlDbType.Array | NpgsqlDbType.Text) { Value = keys.Select(x => (object?)addresses[x].SourceId ?? DBNull.Value).ToArray() });
        command.Parameters.Add(new NpgsqlParameter("states", NpgsqlDbType.Array | NpgsqlDbType.Text) { Value = keys.Select(x => addresses[x].State).ToArray() });
        command.Parameters.AddWithValue("at", now);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static NpgsqlCommand Command(ShelfDbContext db, string sql)
    {
        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        var transaction = (NpgsqlTransaction?)db.Database.CurrentTransaction?.GetDbTransaction();
        return new NpgsqlCommand(sql, connection, transaction);
    }
}
