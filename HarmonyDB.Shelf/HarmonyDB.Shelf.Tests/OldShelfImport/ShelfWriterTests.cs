using HarmonyDB.Shelf.Database;
using HarmonyDB.Shelf.Import;
using HarmonyDB.Shelf.Import.Old;
using HarmonyDB.Shelf.Import.Planning;
using HarmonyDB.Shelf.Import.Writing;
using HarmonyDB.Shelf.Tests.Auth;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using static HarmonyDB.Shelf.Tests.OldShelfImport.OldShelfBuilder;

namespace HarmonyDB.Shelf.Tests.OldShelfImport;

/// <summary>
/// The import written twice: the second run duplicates nothing, updates what changed in the old shelf, and brings back
/// nothing a person removed meanwhile.
/// </summary>
public sealed class ShelfWriterTests : IAsyncLifetime
{
    private readonly string _name = $"shelf_test_{Guid.NewGuid():N}";
    private string _connectionString = string.Empty;

    [DatabaseFact]
    public async Task ASecondRunDuplicatesNothingAndBringsNothingRemovedBack()
    {
        var old = new OldShelfBuilder();
        var song = old.Song(SharedTenant, 1001, "song", "a", "b");
        var other = old.Song(SharedTenant, 1002, "other", "c");
        old.Like(Alice, song, old.VersionOf(song, "a"), 3, transpose: 2);
        old.Like(Alice, other, old.VersionOf(other, "c"), 1);
        old.Comment(Alice, song, null, "a note");
        old.Visit(Alice, "c");

        var first = await WriteAsync(old.Plan());
        Assert.Equal(2, first.Get("playlist", Report.Outcomes.Created)); // the templates (none here) and the club

        // a person removes a song and a heart in the shelf; the old shelf meanwhile gets a new like and a new key
        await using (var db = Context())
        {
            var removed = await db.PlaylistSongs.SingleAsync(x => x.Title == "other");
            removed.DeletedAt = DateTimeOffset.UtcNow;
            var heart = await db.Marks.SingleAsync(x => x.Chords.SourceId == "id:c");
            heart.DeletedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
        }

        old.Like(Alice, song, old.VersionOf(song, "b"), 2);
        var changed = old.Plan() with
        {
            Keys = old.Plan().Keys.Select(x => x with { Semitones = 4 }).ToList(),
        };
        var second = await WriteAsync(changed);

        await using (var db = Context())
        {
            Assert.Equal(2, await db.Accounts.CountAsync());
            Assert.Equal(1, await db.PlaylistSongs.CountAsync());          // "other" stays removed
            Assert.Equal(2, await db.PlaylistSongItems.CountAsync(x => x.Song.DeletedAt == null));
            Assert.Equal(new[] { "id:a", "id:b" }, await db.Marks.Select(x => x.Chords.SourceId).OrderBy(x => x).ToListAsync()); // c stays removed, b added
            Assert.Equal(4, (await db.Transpositions.SingleAsync()).Semitones); // updated: nobody changed it
            Assert.Equal(1, await db.Notes.CountAsync());
            Assert.Equal(1, await db.Recent.CountAsync());
            Assert.Equal(2, await db.PlaylistMembers.CountAsync(x => x.Playlist.Title == "Club"));
        }

        Assert.Equal(0, second.Get("song", Report.Outcomes.Created));
        Assert.Equal(1, second.Get("song", Report.Outcomes.LeftByPerson));
        Assert.Equal(1, second.Get("heart", Report.Outcomes.LeftByPerson));
        Assert.Equal(1, second.Get("key mine", Report.Outcomes.Updated));
    }

    [DatabaseFact]
    public async Task TheOldAddressIsKeptForEveryChordsButOwnChords()
    {
        var old = new OldShelfBuilder();
        var song = old.Song(SharedTenant, 1001, "song", "a");
        // own chords: their address may open them to whoever holds it — also where a plain version names it
        old.Version(song, "own", Keeper, collectiveType: OldCollectiveType.Public, collectiveId: Guid.NewGuid());
        old.Song(SharedTenant, 1002, "other", "own");
        var plan = old.Plan();
        Assert.Equal(new Dictionary<string, string> { [Id("a")] = Uri("a") }, plan.Addresses);

        var first = await WriteAsync(plan);
        await using (var db = Context())
        {
            var addresses = await db.Chords.ToDictionaryAsync(x => x.SourceId, x => x.OldAddress);
            Assert.Equal(Uri("a"), addresses[Id("a")]);
            Assert.Null(addresses[Id("own")]);
        }

        Assert.Equal(1, first.Get("chords old address", Report.Outcomes.Updated));
        Assert.Equal(0, (await WriteAsync(plan)).Get("chords old address", Report.Outcomes.Updated));
    }

    [DatabaseFact]
    public async Task EntriesHoldTheirVersionsAddressAndPrivacyAndVisitsTheirNames()
    {
        var old = new OldShelfBuilder();
        var song = old.Song(SharedTenant, 1001, "song", "a");
        old.Tenant(8, 1000, $"Personal {Alice}");
        var personal = old.Song(8, 1001, "mine", "b");
        old.Version(personal, "own", Alice, collectiveType: OldCollectiveType.Private, collectiveId: Guid.NewGuid());
        old.Visit(Alice, "a");
        old.VisitById(Alice, "lc-SEARCHED", "Found by search");

        var plan = old.Plan();
        // a visit by an id is taken as it is: nothing asked of the sources
        Assert.Contains(plan.Recent, x => x.SourceId == "lc-SEARCHED" && x.Title == "Found by search" && x.Source == "Local");
        await WriteAsync(plan);

        await using var db = Context();
        var entries = await db.PlaylistSongItems.Select(x => new { x.Chords!.SourceId, x.Address, x.Private }).ToListAsync();
        Assert.Contains(entries, x => x.SourceId == "id:a" && x.Address == Uri("a") && !x.Private);
        Assert.Contains(entries, x => x.SourceId == "id:own" && x.Address == Uri("own") && x.Private);
        var recent = await db.Recent.Include(x => x.Chords).ToListAsync();
        Assert.Contains(recent, x => x.Chords!.SourceId == "id:a" && x.Title == "Title of a" && x.SourceTitle == "A source");
        Assert.Contains(recent, x => x.Chords!.SourceId == "lc-SEARCHED" && x.Title == "Found by search");
        Assert.NotEqual(0, song);
    }

    [DatabaseFact]
    public async Task APersonsOwnKeyAndNoteAreNeverChangedAndTimesAreTheOldOnes()
    {
        var old = new OldShelfBuilder();
        var song = old.Song(SharedTenant, 1001, "song", "a");
        old.Like(Alice, song, old.VersionOf(song, "a"), 3, transpose: 2);
        old.Comment(Alice, song, null, "from the old shelf");

        // the person is in the new shelf already, with their own key and note on those chords
        await WriteAsync(old.Plan() with { Keys = [], Notes = [], Marks = [] });
        await using (var db = Context())
        {
            var account = (await db.AccountLogins.SingleAsync(x => x.Subject == Alice.ToString())).AccountId;
            var chords = await db.Chords.SingleAsync(x => x.SourceId == "id:a");
            var songId = (await db.PlaylistSongs.SingleAsync()).Id;
            db.Transpositions.Add(new() { Id = Guid.NewGuid(), AccountId = account, ChordsId = chords.Id, Semitones = -5, UpdatedAt = DateTimeOffset.UtcNow });
            db.Notes.Add(new() { Id = Guid.NewGuid(), AccountId = account, SongId = songId, Text = "my own", UpdatedAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync();
        }

        var plan = old.Plan();
        await WriteAsync(plan);
        await WriteAsync(plan with { Keys = plan.Keys.Select(x => x with { Semitones = 4 }).ToList() });

        await using (var db = Context())
        {
            Assert.Equal(-5, (await db.Transpositions.SingleAsync()).Semitones);
            Assert.Equal("my own", (await db.Notes.SingleAsync()).Text);
            Assert.Equal(plan.Marks.Single().At, (await db.Marks.SingleAsync()).CreatedAt);
        }
    }

    private async Task<Report> WriteAsync(ImportPlan plan)
    {
        var report = new Report();
        await using var db = Context();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var ledger = await Ledger.OpenAsync(db, default);
        var writer = new ShelfWriter(db, ledger, report, DateTimeOffset.UtcNow);
        var accounts = await writer.WritePeopleAsync(plan.People, default);
        await writer.WriteAsync(plan, accounts, default);
        await transaction.CommitAsync();
        return report;
    }

    private ShelfDbContext Context() => new(new DbContextOptionsBuilder<ShelfDbContext>().UseShelfDatabase(_connectionString).Options);

    public async Task InitializeAsync()
    {
        if (TestDatabase.Server == null)
            return;

        await using (var connection = new NpgsqlConnection(TestDatabase.Server))
        {
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand($"CREATE DATABASE {_name}", connection);
            await command.ExecuteNonQueryAsync();
        }

        _connectionString = new NpgsqlConnectionStringBuilder(TestDatabase.Server) { Database = _name }.ConnectionString;
        await using var db = Context();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        if (TestDatabase.Server == null)
            return;

        NpgsqlConnection.ClearAllPools();
        await using var connection = new NpgsqlConnection(TestDatabase.Server);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"DROP DATABASE {_name} WITH (FORCE)", connection);
        await command.ExecuteNonQueryAsync();
    }
}
