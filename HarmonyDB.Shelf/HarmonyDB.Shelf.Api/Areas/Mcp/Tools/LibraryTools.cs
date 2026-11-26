using System.ComponentModel;
using System.Security.Claims;
using HarmonyDB.Shelf.Api.Areas.Chords.Services;
using HarmonyDB.Shelf.Api.Areas.Library.Services;
using HarmonyDB.Shelf.Api.Areas.OwnChords.Services;
using HarmonyDB.Shelf.Api.Areas.Playlists.Services;
using HarmonyDB.Shelf.Api.Areas.Recordings.Services;
using HarmonyDB.Shelf.Api.Foundation.Errors;
using HarmonyDB.Shelf.Api.Foundation.Permissions;
using HarmonyDB.Shelf.Model.Marks;
using HarmonyDB.Shelf.Model.Playlists;

namespace HarmonyDB.Shelf.Api.Areas.Mcp.Tools;

/// <summary>
/// Reading the person's library: playlists, their songs and variants, marks, keys, notes, recent, own chords.
/// A handler's parameters: the services and the caller first, then the tool's arguments — the optional ones with
/// their defaults (an argument without a default is required).
/// </summary>
public static class LibraryTools
{
    public const string Model =
        "A playlist holds songs; a song has variants — chords items (letters A, B, C) and recordings — each with an " +
        "itemId. The person puts marks (emoji kinds) on chords, keeps keys (semitones: 'mine' anywhere, or 'here' in one " +
        "playlist) and notes (personal on chords, or on a song, seen by the playlist's members).";

    public static readonly string MarkKindsText = string.Join(", ", MarkKinds.All.Select(x => x.Key));

    private const int NoteLength = 300;

    public static IEnumerable<McpTool> All() =>
    [
        new("list_playlists",
            "The person's playlists (scope mine, default: their own, shared with them with their role, and the app's own " +
            "ones) or public playlists (scope public, found by title words).",
            McpToolEffect.Read,
            async (
                ClaimsPrincipal user, PlaylistService playlists,
                [Description("mine (default) or public.")] PlaylistScope? scope = null,
                [Description("Public: title words.")] string? query = null,
                int? offset = null,
                [Description("1-25, default 20.")] int? limit = null,
                CancellationToken cancellationToken = default) =>
            {
                var (skip, take) = Slim.Paging(offset, limit, 20);
                var all = scope == PlaylistScope.Public
                    ? await playlists.ListPublicAsync(user.GetAccountId(), query, skip + take, cancellationToken)
                    : await playlists.ListMineAsync(user.GetAccountId()!.Value, cancellationToken);
                return Slim.Paged(all, skip, take, Slim.Playlist);
            },
            BuiltInPermissions.McpLibraryRead),

        new("get_playlist",
            "A playlist's settings, owner, members (accountId, name, role), what this person may do, and open invitations.",
            McpToolEffect.Read,
            async (ClaimsPrincipal user, PlaylistService playlists, Guid playlistId, CancellationToken cancellationToken = default)
                => Slim.Playlist(await playlists.GetAsync(user.GetAccountId(), playlistId, null, cancellationToken)),
            BuiltInPermissions.McpLibraryRead),

        new("list_playlist_songs",
            "The songs of a playlist: songId, title, artists, chordsId of the main variant, marks, counts of variants and " +
            "recordings. Filter by title words or a mark kind; sort: order (default), popular, artist, title, newSongs, " +
            "newChords, myMarks.",
            McpToolEffect.Read,
            async (
                ClaimsPrincipal user, SongLists lists, Views views,
                Guid playlistId,
                [Description("Title or artist words.")] string? query = null,
                [Description("A mark kind, by anyone (who='me': by this person).")] string? mark = null,
                [Description("'me': marked by this person.")] string? who = null,
                SongSort? sort = null,
                int? offset = null,
                [Description("1-25, default 20.")] int? limit = null,
                CancellationToken cancellationToken = default) =>
            {
                var (skip, take) = Slim.Paging(offset, limit, 20);
                var list = await lists.PlaylistAsync(user, playlistId, null, new(query, sort, who, mark, skip, take), cancellationToken);
                return await Slim.SongsAsync(user, views, list, cancellationToken);
            },
            BuiltInPermissions.McpLibraryRead),

        new("search_my_library",
            "The person's library: songs of the playlists they show there, and chords they marked or keep a key or note on. " +
            "Filter by title words or a mark kind; sort: popular (default), artist, title, newSongs, newChords, myMarks.",
            McpToolEffect.Read,
            async (
                ClaimsPrincipal user, SongLists lists, Views views,
                [Description("Title or artist words.")] string? query = null,
                [Description("A mark kind (who='me': by this person).")] string? mark = null,
                [Description("'me': marked by this person.")] string? who = null,
                SongSort? sort = null,
                int? offset = null,
                [Description("1-25, default 20.")] int? limit = null,
                CancellationToken cancellationToken = default) =>
            {
                var (skip, take) = Slim.Paging(offset, limit, 20);
                var list = await lists.LibraryAsync(user, new(query, sort, who, mark, skip, take), cancellationToken);
                return await Slim.SongsAsync(user, views, list, cancellationToken);
            },
            BuiltInPermissions.McpLibraryRead),

        new("get_song",
            "A song of a playlist: its chords variants (itemId, letter, chords) and recordings (itemId, service, trackId), " +
            "this person's main variant and recording, marks by kind, keys, notes (shortened). With candidates=true also " +
            "recordings found for its chords that it does not have yet.",
            McpToolEffect.Read,
            async (
                ClaimsPrincipal user, SongPages pages, SongRecordingsService recordings,
                Guid playlistId,
                Guid songId,
                [Description("Also recording candidates.")] bool? candidates = null,
                CancellationToken cancellationToken = default) =>
            {
                var me = user.GetAccountId();
                var page = await pages.GetAsync(user, playlistId, songId, null, cancellationToken);
                var names = page.People.ToDictionary(x => x.Id, x => x.Name);
                var found = candidates == true ? (await recordings.ListAsync(user, playlistId, songId, null, cancellationToken)).Candidates : null;
                return new
                {
                    song = new { page.Song.Id, page.Song.PlaylistId, page.Song.Title, artists = string.Join(", ", page.Song.Artists) },
                    page.CanEdit,
                    variants = page.Variants.Select(x => new { x.ItemId, letter = (char)('A' + x.Index), chords = Slim.Chords(x.Chords) }).ToList(),
                    recordings = page.Recordings.Select(x => new { x.ItemId, x.Recording.Service, trackId = x.Recording.ServiceId, x.Recording.Title, artists = string.Join(", ", x.Recording.Artists) }).ToList(),
                    myMainItemId = page.MyMainItemId,
                    myRecordingItemId = page.MyRecordingItemId,
                    marks = page.Marks.GroupBy(x => x.Kind).Select(g => new { kind = g.Key, count = g.Count(), mine = g.Any(x => x.AccountId == me) }).ToList(),
                    keys = page.Keys.Select(x => new { x.Chords, x.Semitones, x.Here, mine = x.AccountId == me }).ToList(),
                    notes = page.Notes.Take(10).Select(x => new { by = names.GetValueOrDefault(x.AccountId), mine = x.AccountId == me, text = Slim.Short(x.Text, NoteLength) }).ToList(),
                    myNotes = page.MyNotes.Select(x => new { x.Chords, text = Slim.Short(x.Text, NoteLength) }).ToList(),
                    candidates = found?.Take(10).Select(x => new { x.Service, trackId = x.Id, x.Title, artists = string.Join(", ", x.Artists.Select(a => a.Name)) }).ToList(),
                };
            },
            BuiltInPermissions.McpLibraryRead),

        new("get_my_marks_keys_notes",
            "This person's marks, keys ('mine') and personal notes on chords, by chordsIds (at most 50). Mark kinds: " + MarkKindsText + ".",
            McpToolEffect.Read,
            async (ClaimsPrincipal user, ChordsReader reader, [Description("chordsIds.")] string[] chordsIds, CancellationToken cancellationToken = default) =>
            {
                if (chordsIds is not { Length: > 0 and <= 50 })
                    throw ShelfException.BadRequest();
                var seen = await reader.GetHeadersWithAnnotationsAsync(user, chordsIds, cancellationToken);
                return new
                {
                    marks = seen.Marks.Select(x => new { x.Chords, x.Kind }).ToList(),
                    keys = seen.Keys.Select(x => new { x.Chords, x.Semitones }).ToList(),
                    notes = seen.MyNotes.Select(x => new { x.Chords, text = Slim.Short(x.Text, NoteLength) }).ToList(),
                };
            },
            BuiltInPermissions.McpLibraryRead),

        new("list_recent",
            "What this person opened lately, newest first: chords, and the song they were opened in.",
            McpToolEffect.Read,
            async (
                ClaimsPrincipal user, RecentService recent,
                int? offset = null,
                [Description("1-25, default 10.")] int? limit = null,
                CancellationToken cancellationToken = default) =>
            {
                var (skip, take) = Slim.Paging(offset, limit);
                var all = await recent.ListAsync(user, skip + take, cancellationToken);
                return Slim.Paged(all, skip, take, x => new
                {
                    chords = x.Chords == null ? null : Slim.Chords(x.Chords),
                    song = x.Song == null ? null : new { x.Song.Id, x.Song.PlaylistId, x.Song.Title },
                    x.OpenedAt,
                });
            },
            BuiltInPermissions.McpLibraryRead),

        new("list_my_own_chords",
            "The chords this person wrote (own chords), latest changed first: ownChordsId, chordsId, title, artists, key, " +
            "visibility, version. get_my_own_chords gives the text.",
            McpToolEffect.Read,
            async (
                ClaimsPrincipal user, OwnChordsService ownChords,
                int? offset = null,
                [Description("1-25, default 10.")] int? limit = null,
                CancellationToken cancellationToken = default)
                => Slim.Paged((await ownChords.ListAsync(user, cancellationToken)).Items, offset, limit, x => new
                {
                    ownChordsId = x.Id,
                    x.ChordsId,
                    x.Text.Title,
                    artists = string.Join(", ", x.Text.Artists),
                    x.Text.Key,
                    x.Text.Visibility,
                    x.Version,
                    x.UpdatedAt,
                }),
            BuiltInPermissions.McpLibraryRead),

        new("get_my_own_chords",
            "One of this person's own chords: its current version's text and fields (or an older version by number), and " +
            "the list of versions; editable false for those written in the old shelf, which are not changed or deleted here. " +
            IndexTools.SheetFormat,
            McpToolEffect.Read,
            async (
                ClaimsPrincipal user, OwnChordsService ownChords,
                Guid ownChordsId,
                [Description("A version number; the current if not given.")] int? version = null,
                CancellationToken cancellationToken = default) =>
            {
                var details = await ownChords.GetAsync(user, ownChordsId, cancellationToken);
                var text = version == null
                    ? details.Item.Text
                    : details.History.SingleOrDefault(x => x.Version == version)?.Text ?? throw ShelfException.NotFound();
                return new
                {
                    ownChordsId = details.Item.Id,
                    details.Item.ChordsId,
                    version = version ?? details.Item.Version,
                    details.Item.Editable,
                    text,
                    versions = details.History.Select(x => new { x.Version, x.CreatedAt, comment = Slim.Short(x.Text.VersionComment, 100) }).ToList(),
                };
            },
            BuiltInPermissions.McpLibraryRead),
    ];
}

/// <summary>
/// Which playlists <c>list_playlists</c> gives.
/// </summary>
public enum PlaylistScope
{
    Mine,
    Public,
}
