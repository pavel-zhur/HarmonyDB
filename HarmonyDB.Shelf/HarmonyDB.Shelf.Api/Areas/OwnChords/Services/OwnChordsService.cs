using System.Globalization;
using System.Security.Claims;
using HarmonyDB.Common.Representations.OneShelf;
using HarmonyDB.Shelf.Api.Areas.Chords.Services;
using HarmonyDB.Shelf.Api.Areas.Playlists.Services;
using HarmonyDB.Shelf.Api.Clients.Index;
using HarmonyDB.Shelf.Api.Clients.OwnChords;
using HarmonyDB.Shelf.Api.Common;
using HarmonyDB.Shelf.Api.Common.Jobs;
using HarmonyDB.Shelf.Api.Foundation.Errors;
using HarmonyDB.Shelf.Api.Foundation.Permissions;
using HarmonyDB.Shelf.Database;
using HarmonyDB.Shelf.Database.Model;
using HarmonyDB.Shelf.Model;
using HarmonyDB.Shelf.Model.OwnChords;
using HarmonyDB.Source.Api.Model.V2;
using HarmonyDB.Source.Api.Model.V2.Api;
using Microsoft.EntityFrameworkCore;
using PlaylistModels = HarmonyDB.Shelf.Model.Playlists;

namespace HarmonyDB.Shelf.Api.Areas.OwnChords.Services;

/// <summary>
/// A person's own chords, kept by the own chords' store with their versions. Those written here belong to the
/// person's account; those written in the old shelf belong to its Telegram user, and are the person's here when their
/// account has that Telegram sign-in. As chords (in playlists, on their page, marked …) they are the own chords'
/// source's items: their id there is asked of the sources by their address, the shelf knows nothing of its form. New own
/// chords go to the person's playlist of own chords, and, if asked, become a variant of a song or new songs elsewhere.
/// </summary>
public sealed class OwnChordsService(
    ShelfDbContext db,
    OwnChordsClient store,
    IndexClient index,
    SourceCallers callers,
    SystemPlaylists systemPlaylists,
    SongService songs,
    PlaylistGate gate,
    JobQueue jobs)
{
    public const int MaxTitle = 300;
    public const int MaxArtists = 20;
    public const int MaxContents = 200_000;
    public const int MaxDescription = 20_000;
    public const int MaxKey = 10;
    public const int MaxBpm = 1000;
    public const int MaxVersionComment = 1000;

    public async Task<OwnChordsList> ListAsync(ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var stored = await store.ListAsync(await AuthorAsync(user, cancellationToken), cancellationToken);
        var ids = await ChordsIdsAsync(callers.For(user), stored.Select(x => x.Uri).ToList(), cancellationToken);
        return new() { Items = stored.Select((x, i) => ToItem(x, ids[i])).ToList() };
    }

    public async Task<OwnChordsDetails> GetAsync(ClaimsPrincipal user, Guid id, CancellationToken cancellationToken)
    {
        var stored = await store.GetAsync(await AuthorAsync(user, cancellationToken), id, cancellationToken)
                     ?? throw ShelfException.NotFound(OwnChordsErrorCodes.NoSuchOwnChords);
        return await DetailsAsync(user, stored, cancellationToken);
    }

    public async Task<OwnChordsDetails> CreateAsync(ClaimsPrincipal user, CreateOwnChordsRequest request, CancellationToken cancellationToken)
    {
        var text = Normalize(request.Text);
        if ((request.PlaylistId == null) != (request.SongId == null))
            throw ShelfException.BadRequest(OwnChordsErrorCodes.SongNeedsPlaylist);

        var me = user.GetAccountId()!.Value;

        // where they are to go is checked first: nothing is saved that cannot be placed
        if (request is { PlaylistId: { } toPlaylist, SongId: { } toSong })
            (await gate.SongForChangeAsync(me, toPlaylist, toSong, cancellationToken)).View.RequireAdd();
        foreach (var playlistId in (request.AlsoInPlaylists ?? []).Distinct())
            (await gate.ForChangeAsync(me, playlistId, cancellationToken)).RequireAdd();

        var stored = await store.InsertAsync(me, ToStored(text), cancellationToken);
        var placing = new Placing(stored.Uri, callers.For(user).Keys, text.Title, text.Artists, request.PlaylistId, request.SongId, request.AlsoInPlaylists ?? []);
        var chordsId = (await ChordsIdsAsync(callers.For(user), [stored.Uri], cancellationToken)).Single();
        if (chordsId == null)
        {
            // the sources did not answer now: the chords are placed by a job that asks them again
            var job = await jobs.EnqueueAsync(me, PlacingJobKind, stored.CollectiveId.ToString(), placing, cancellationToken);
            return Details(stored, null) with { PlacingJobId = job.Id };
        }

        await PlaceAsync(me, chordsId, placing, cancellationToken);
        return Details(stored, chordsId);
    }

    public const string PlacingJobKind = "own_chords_placing";

    /// <summary>
    /// Puts new own chords into the person's playlist of own chords, and where else they were asked to go. Where they are
    /// already (a placing done again after it was interrupted), nothing is added.
    /// </summary>
    internal async Task PlaceAsync(Guid me, string chordsId, Placing placing, CancellationToken cancellationToken)
    {
        var text = (placing.Title, placing.Artists);
        var mine = await systemPlaylists.GetOrCreateAsync(me, PlaylistSystemKind.OwnChords, cancellationToken);
        await UnlessThereAsync(() => songs.AddAsync(me, mine.Id, NewSong(chordsId, text), cancellationToken));
        if (placing is { PlaylistId: { } songPlaylistId, SongId: { } songId })
            await UnlessThereAsync(() => songs.AddVariantAsync(me, songPlaylistId, songId, new() { Chords = chordsId }, cancellationToken));
        foreach (var playlistId in placing.AlsoInPlaylists.Distinct().Where(x => x != mine.Id))
            await UnlessThereAsync(() => songs.AddAsync(me, playlistId, NewSong(chordsId, text), cancellationToken));
    }

    private static async Task UnlessThereAsync(Func<Task> add)
    {
        try
        {
            await add();
        }
        catch (ShelfException exception) when (exception.Code == PlaylistModels.PlaylistErrorCodes.AlreadyInPlaylist)
        {
        }
    }

    /// <summary>
    /// The chords' id of own chords by their address, asked of the sources for the caller; null when they did not answer.
    /// </summary>
    internal async Task<string?> ChordsIdAsync(Caller caller, Uri uri, CancellationToken cancellationToken)
        => (await ChordsIdsAsync(caller, [uri], cancellationToken)).Single();

    /// <param name="Uri">The address of the own chords.</param>
    /// <param name="Keys">The person's custom keys when they saved them.</param>
    internal sealed record Placing(
        Uri Uri,
        IReadOnlyList<string> Keys,
        string Title,
        IReadOnlyList<string> Artists,
        Guid? PlaylistId,
        Guid? SongId,
        IReadOnlyList<Guid> AlsoInPlaylists);

    /// <summary>
    /// A new version.
    /// </summary>
    public async Task<OwnChordsDetails> UpdateAsync(ClaimsPrincipal user, Guid id, UpdateOwnChordsRequest request, CancellationToken cancellationToken)
    {
        var text = Normalize(request.Text);
        var stored = await store.UpdateAsync(await AuthorAsync(user, cancellationToken), id, ToStored(text), cancellationToken)
                     ?? throw ShelfException.NotFound(OwnChordsErrorCodes.NoSuchOwnChords);
        return await DetailsAsync(user, stored, cancellationToken);
    }

    /// <summary>
    /// Deletes them; they leave the person's playlist of own chords. Elsewhere they stay as chords no longer open.
    /// </summary>
    public async Task DeleteAsync(ClaimsPrincipal user, Guid id, CancellationToken cancellationToken)
    {
        var author = await AuthorAsync(user, cancellationToken);
        var stored = await store.GetAsync(author, id, cancellationToken) ?? throw ShelfException.NotFound(OwnChordsErrorCodes.NoSuchOwnChords);
        var chordsId = (await ChordsIdsAsync(callers.For(user), [stored.Uri], cancellationToken)).Single()
                       ?? throw new ShelfException(StatusCodes.Status503ServiceUnavailable, ErrorCodes.Unavailable);
        if (!await store.DeleteAsync(author, id, cancellationToken))
            throw ShelfException.NotFound(OwnChordsErrorCodes.NoSuchOwnChords);

        var me = author.AccountId;
        var variants = await db.PlaylistSongItems
            .Where(x => x.Chords!.SourceId == chordsId
                        && x.Song.Playlist.OwnerId == me
                        && x.Song.Playlist.SystemKind == PlaylistSystemKind.OwnChords
                        && x.Song.DeletedAt == null)
            .Select(x => new { x.Id, x.SongId, x.Song.PlaylistId })
            .ToListAsync(cancellationToken);
        foreach (var variant in variants)
            await songs.RemoveVariantAsync(me, variant.PlaylistId, variant.SongId, variant.Id, cancellationToken);
    }

    /// <summary>
    /// How the text will look once saved.
    /// </summary>
    public async Task<OwnChordsPreview> PreviewAsync(string contents, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(contents) || contents.Length > MaxContents)
            throw ShelfException.BadRequest(OwnChordsErrorCodes.ContentsRequired, new Dictionary<string, string> { ["max"] = MaxContents.ToString() });

        var sheet = await store.PreviewAsync(contents, cancellationToken);
        return new() { Content = new() { Sheet = sheet } };
    }

    /// <summary>
    /// The text of own chords made of other chords open to the person (their own variant of them, or a correction of an
    /// analysis by hand): the sheet in the editor's markup. What the markup cannot carry (changes of key, tempo and time
    /// signature, the times, the bars) is left out: the copy is ordinary own chords.
    /// </summary>
    public async Task<OwnChordsText> DraftAsync(ClaimsPrincipal user, string chordsId, CancellationToken cancellationToken)
    {
        var response = await index.Sources.GetContent(callers.For(user), chordsId, [ContentKind.Sheet], cancellationToken);
        switch (response.State)
        {
            case ItemState.NotOpen:
                throw ShelfException.NotFound(OwnChordsErrorCodes.ChordsNotOpen);
            case ItemState.Unavailable:
                throw new ShelfException(StatusCodes.Status503ServiceUnavailable, ErrorCodes.Unavailable);
        }

        var header = response.Header!;
        return new()
        {
            Title = header.Title ?? string.Empty,
            Artists = header.Artists,
            Contents = response.Content!.Sheet?.Root.AsOneShelf(new()) ?? string.Empty,
            Description = null,
            Key = header.Attributes.Key,
            Bpm = header.Attributes.Bpm is { } bpm ? (int)Math.Round(bpm) : null,
            Visibility = OwnChordsVisibility.Link,
        };
    }

    /// <summary>
    /// The person as an author of own chords: their account and the Telegram users their Telegram sign-ins are.
    /// </summary>
    private async Task<OwnChordsAuthor> AuthorAsync(ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var me = user.GetAccountId()!.Value;
        var subjects = await db.AccountLogins.Where(x => x.AccountId == me && x.Provider == TelegramProvider)
            .Select(x => x.Subject).ToListAsync(cancellationToken);
        return new(me, subjects.Select(x => long.Parse(x, CultureInfo.InvariantCulture)).ToList());
    }

    private const string TelegramProvider = "telegram";

    /// <summary>
    /// The chords' ids of own chords by their addresses, asked of the sources as the person: null for one whose source
    /// did not answer.
    /// </summary>
    private async Task<IReadOnlyList<string?>> ChordsIdsAsync(Caller caller, IReadOnlyList<Uri> uris, CancellationToken cancellationToken)
    {
        if (uris.Count == 0)
            return [];

        var resolved = await index.Sources.Resolve(caller, uris, cancellationToken);
        return resolved.Items.Select(x => x.State == ResolveState.Resolved ? x.Id : null).ToList();
    }

    private async Task<OwnChordsDetails> DetailsAsync(ClaimsPrincipal user, StoredOwnChords stored, CancellationToken cancellationToken)
        => Details(stored, (await ChordsIdsAsync(callers.For(user), [stored.Uri], cancellationToken)).Single());

    private static OwnChordsDetails Details(StoredOwnChords stored, string? chordsId) => new()
    {
        Item = ToItem(stored, chordsId),
        History = (stored.History ?? []).Select(x => new OwnChordsVersion
        {
            Version = x.VersionNumber,
            CreatedAt = x.CreatedOn,
            Text = ToText(x.Collective),
        }).ToList(),
    };

    private static OwnChordsItem ToItem(StoredOwnChords stored, string? chordsId) => new()
    {
        Id = stored.CollectiveId,
        ChordsId = chordsId,
        Text = ToText(stored.Collective),
        Version = stored.VersionNumber,
        CreatedAt = stored.CreatedOn,
        UpdatedAt = stored.UpdatedOn,
        FromOldShelf = stored.AuthorTelegramUserId != null,
    };

    private static PlaylistModels.AddSongRequest NewSong(string chordsId, (string Title, IReadOnlyList<string> Artists) text) => new()
    {
        Chords = chordsId,
        Title = text.Title,
        Artists = text.Artists,
    };

    private static OwnChordsText ToText(StoredText stored) => new()
    {
        Title = stored.Title,
        Artists = stored.Authors,
        Contents = stored.Contents,
        Description = stored.Description,
        Key = stored.Tonality,
        Bpm = stored.Bpm,
        Visibility = stored.Visibility switch
        {
            StoredVisibility.Private => OwnChordsVisibility.Private,
            StoredVisibility.Club => OwnChordsVisibility.Link,
            StoredVisibility.Public => OwnChordsVisibility.Public,
            _ => throw new ArgumentOutOfRangeException(nameof(stored), stored.Visibility, null),
        },
        VersionComment = stored.VersionComment,
    };

    private static StoredText ToStored(OwnChordsText text) => new(
        text.Artists,
        text.Title,
        text.Contents,
        text.Visibility switch
        {
            OwnChordsVisibility.Private => StoredVisibility.Private,
            OwnChordsVisibility.Link => StoredVisibility.Club,
            OwnChordsVisibility.Public => StoredVisibility.Public,
            _ => throw ShelfException.BadRequest(OwnChordsErrorCodes.Invalid, new Dictionary<string, string> { ["field"] = "visibility" }),
        },
        text.Description,
        text.Key,
        text.Bpm,
        text.VersionComment);

    /// <summary>
    /// The text as it is kept: the title and the artists trimmed, the artists without empty or repeated names, the
    /// optional fields null when empty; refused when something needed is missing or too long.
    /// </summary>
    internal static OwnChordsText Normalize(OwnChordsText text)
    {
        var title = text.Title?.Trim();
        if (string.IsNullOrEmpty(title) || title.Length > MaxTitle)
            throw ShelfException.BadRequest(OwnChordsErrorCodes.TitleRequired, new Dictionary<string, string> { ["max"] = MaxTitle.ToString() });

        var artists = (text.Artists ?? []).Select(x => x?.Trim()).Where(x => !string.IsNullOrEmpty(x)).Distinct(StringComparer.Ordinal).ToList();
        if (artists.Count == 0 || artists.Count > MaxArtists || artists.Any(x => x!.Length > MaxTitle))
            throw ShelfException.BadRequest(OwnChordsErrorCodes.ArtistsRequired, new Dictionary<string, string> { ["max"] = MaxArtists.ToString() });

        if (string.IsNullOrWhiteSpace(text.Contents) || text.Contents.Length > MaxContents)
            throw ShelfException.BadRequest(OwnChordsErrorCodes.ContentsRequired, new Dictionary<string, string> { ["max"] = MaxContents.ToString() });

        if (text.Bpm is < 1 or > MaxBpm)
            throw Invalid("bpm", MaxBpm);

        return text with
        {
            Title = title,
            Artists = artists!,
            Description = Optional(text.Description, "description", MaxDescription),
            Key = Optional(text.Key, "key", MaxKey),
            VersionComment = Optional(text.VersionComment, "versionComment", MaxVersionComment),
        };

        static string? Optional(string? value, string field, int max)
        {
            var trimmed = value?.Trim();
            if (trimmed?.Length > max)
                throw Invalid(field, max);
            return string.IsNullOrEmpty(trimmed) ? null : trimmed;
        }

        static ShelfException Invalid(string field, int max)
            => ShelfException.BadRequest(OwnChordsErrorCodes.Invalid, new Dictionary<string, string> { ["field"] = field, ["max"] = max.ToString() });
    }
}
