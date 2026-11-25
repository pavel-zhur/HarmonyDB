using System.Data;
using System.Text.Json;
using Npgsql;

namespace HarmonyDB.Shelf.Import.Old;

/// <summary>
/// Reads the old shelf's database, never writing: every read is in a read-only transaction, so the server itself refuses
/// a write. The songs and the own chords are in two schemas read by two roles; each is read as one snapshot.
/// </summary>
public sealed class OldShelfReader(string songsConnectionString, string ownChordsConnectionString, string illustrationsConnectionString)
{
    public async Task<OldShelf> ReadAsync(CancellationToken cancellationToken)
    {
        await using var songs = await OpenReadOnlyAsync(songsConnectionString, cancellationToken);
        await using var songsTransaction = await songs.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
        await ReadOnlyAsync(songs, songsTransaction, cancellationToken);

        var tenants = await ListAsync(songs, songsTransaction, """
            select "Id", "PrivateDescription", "Tags", "ArePdfsAllowed", "LatestUsedIndex" from songs."Tenants" order by "Id"
            """, r => new OldTenant(r.GetInt32(0), r.IsDBNull(1) ? null : r.GetString(1), r.GetFieldValue<string[]>(2), r.GetBoolean(3), r.GetInt32(4)), cancellationToken);

        var users = await ListAsync(songs, songsTransaction, """
            select "Id", "TenantId", "Title", "IsAuthorizedToUseIllustrations", "CreatedOn" from songs."Users" order by "Id"
            """, r => new OldUser(r.GetInt64(0), r.GetInt32(1), r.GetString(2), r.GetBoolean(3), r.GetDateTime(4)), cancellationToken);

        var songRows = await ListAsync(songs, songsTransaction, """
            select s."Id", s."TenantId", s."Index", s."Title", s."Status", s."CreatedByUserId", s."CreatedOn",
                   coalesce((select array_agg(a."Name" order by a."Id") from songs."ArtistSong" x join songs."Artists" a on a."Id" = x."ArtistsId" where x."SongsId" = s."Id"), array[]::text[])
            from songs."Songs" s order by s."Id"
            """, r => new OldSong(r.GetInt32(0), r.GetInt32(1), r.GetInt32(2), r.GetString(3), r.GetFieldValue<string[]>(7), r.GetString(4), r.GetInt64(5), r.GetDateTime(6)), cancellationToken);

        var versions = await ListAsync(songs, songsTransaction, """
            select "Id", "SongId", "Uri", "UserId", "CreatedOn", "PublishedSettings", "CollectiveType", "CollectiveId", "IsDefaultTemplate"
            from songs."Versions" order by "Id"
            """, r => new OldVersion(r.GetInt32(0), r.GetInt32(1), r.GetString(2), r.GetInt64(3), r.GetDateTime(4),
                r.IsDBNull(5) ? null : r.GetString(5), r.IsDBNull(6) ? null : r.GetString(6), r.IsDBNull(7) ? null : r.GetGuid(7), r.GetBoolean(8)), cancellationToken);

        var likes = await ListAsync(songs, songsTransaction, """
            select "Id", "UserId", "SongId", "VersionId", "Level", "Transpose", "CreatedOn", "LikeCategoryId" from songs."Likes" order by "Id"
            """, r => new OldLike(r.GetInt32(0), r.GetInt64(1), r.GetInt32(2), r.IsDBNull(3) ? null : r.GetInt32(3), r.GetByte(4),
                r.IsDBNull(5) ? null : r.GetInt32(5), r.GetDateTime(6), r.IsDBNull(7) ? null : r.GetInt32(7)), cancellationToken);

        var lists = await ListAsync(songs, songsTransaction, """
            select "Id", "TenantId", "UserId", "Name", "Order", "Access" from songs."LikeCategories" order by "Id"
            """, r => new OldList(r.GetInt32(0), r.GetInt32(1), r.GetInt64(2), r.GetString(3), r.GetInt32(4), r.GetString(5)), cancellationToken);

        var comments = await ListAsync(songs, songsTransaction, """
            select "Id", "UserId", "SongId", "VersionId", "Text", "CreatedOn" from songs."Comments" where "Text" is not null order by "Id"
            """, r => new OldComment(r.GetInt32(0), r.GetInt64(1), r.GetInt32(2), r.IsDBNull(3) ? null : r.GetInt32(3), r.GetString(4), r.GetDateTime(5)), cancellationToken);

        var visits = await ListAsync(songs, songsTransaction, """
            select "UserId", "Uri", case when "Uri" is null then "ExternalId" end, max("ViewedOn")
            from songs."VisitedChords"
            where "Uri" is not null or "ExternalId" is not null
            group by 1, 2, 3 order by 1, 4
            """, r => new OldVisit(r.GetInt64(0), r.IsDBNull(1) ? null : r.GetString(1), r.IsDBNull(2) ? null : r.GetString(2), r.GetDateTime(3)), cancellationToken);

        await songsTransaction.CommitAsync(cancellationToken);

        await using var own = await OpenReadOnlyAsync(ownChordsConnectionString, cancellationToken);
        await using var ownTransaction = await own.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
        await ReadOnlyAsync(own, ownTransaction, cancellationToken);
        var ownChords = await ListAsync(own, ownTransaction, """
            select "Id", "CreatedByUserId", "LatestVisibility", "Versions"::text from collectives."Collectives" order by "Id"
            """, r => ToOwnChords(r.GetGuid(0), r.GetInt64(1), r.GetInt32(2), r.GetString(3)), cancellationToken);
        await ownTransaction.CommitAsync(cancellationToken);

        return new()
        {
            Tenants = tenants,
            Users = users,
            Songs = songRows,
            Versions = versions,
            Likes = likes,
            Lists = lists,
            Comments = comments,
            Visits = visits,
            OwnChords = ownChords,
        };
    }

    /// <summary>
    /// The keys illustrations are kept under (the old shelf's addresses, and the new shelf's own ids), and the addresses
    /// the new shelf's ids are aliased to.
    /// </summary>
    public async Task<(IReadOnlyList<string> illustrated, IReadOnlySet<string> aliased)> ReadIllustratedAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenReadOnlyAsync(illustrationsConnectionString, cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
        await ReadOnlyAsync(connection, transaction, cancellationToken);
        var illustrated = await ListAsync(connection, transaction, """select distinct "Url" from illustrations."IllustrationsPrompts" order by 1""", r => r.GetString(0), cancellationToken);
        var aliased = await ListAsync(connection, transaction, """select distinct "Url" from illustrations."ShelfAliases" """, r => r.GetString(0), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return (illustrated, aliased.ToHashSet(StringComparer.Ordinal));
    }

    /// <summary>
    /// The visibility the own chords' database keeps for deleted ones.
    /// </summary>
    private const int DeletedVisibility = 3;

    private static OldOwnChords ToOwnChords(Guid id, long author, int visibility, string versionsJson)
    {
        using var document = JsonDocument.Parse(versionsJson);
        var versions = document.RootElement.EnumerateArray().ToList();
        var latest = versions[^1];
        return new(
            id,
            author,
            visibility == DeletedVisibility,
            latest.GetProperty("title").GetString()!,
            latest.GetProperty("authors").EnumerateArray().Select(x => x.GetString()!).ToList(),
            versions[0].GetProperty("createdOn").GetDateTimeOffset().UtcDateTime);
    }

    private static async Task<NpgsqlConnection> OpenReadOnlyAsync(string connectionString, CancellationToken cancellationToken)
    {
        var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private static async Task ReadOnlyAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("SET TRANSACTION READ ONLY", connection, transaction);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<List<T>> ListAsync<T>(NpgsqlConnection connection, NpgsqlTransaction transaction, string sql, Func<NpgsqlDataReader, T> map, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var list = new List<T>();
        while (await reader.ReadAsync(cancellationToken))
            list.Add(map(reader));
        return list;
    }
}
