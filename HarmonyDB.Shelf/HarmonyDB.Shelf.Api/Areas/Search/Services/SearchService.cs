using System.Security.Claims;
using HarmonyDB.Shelf.Api.Areas.Chords.Services;
using HarmonyDB.Shelf.Api.Areas.Music.Services;
using HarmonyDB.Shelf.Api.Clients.Index;
using HarmonyDB.Shelf.Api.Foundation.Errors;
using HarmonyDB.Shelf.Api.Foundation.Permissions;
using HarmonyDB.Shelf.Database;
using HarmonyDB.Shelf.Database.Model;
using HarmonyDB.Shelf.Model.Music;
using HarmonyDB.Shelf.Model.Search;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using MusicService = HarmonyDB.Shelf.Model.Me.MusicService;

namespace HarmonyDB.Shelf.Api.Areas.Search.Services;

/// <summary>
/// The one search: songs of the playlists the person sees, chords from the sources, artists and recordings of the music
/// services.
/// Chords are never grouped into songs: a found item stands on its own, unless it is already in a song of a playlist
/// the person sees — then that song stands in its place.
/// It is asked as one types, so it does not go out to the slow searches without need: those parts wait for a few
/// characters, what was found is kept for a few minutes, and a request the app gives up on (a newer one replaced it)
/// stops its searches.
/// </summary>
public sealed class SearchService(ShelfDbContext db, IndexClient index, SourceCallers callers, MusicCatalog music, IMemoryCache cache)
{
    public const int MaxQueryLength = 200;
    public const int ChordsMinLength = 2;
    public const int MusicMinLength = 3;

    private const int SongsByTitle = 30;
    private const int TracksPerService = 10;
    private const int ArtistsPerService = 5;

    private static readonly TimeSpan KeepChords = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan KeepMusic = TimeSpan.FromMinutes(30);

    public async Task<SearchResponse> SearchAsync(ClaimsPrincipal user, string? query, IReadOnlySet<SearchPart> parts, CancellationToken cancellationToken)
    {
        var text = string.Join(' ', (query ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (text.Length > MaxQueryLength)
            throw ShelfException.BadRequest();

        var accountId = user.GetAccountId();

        // the slow parts go out together
        var chordsTask = parts.Contains(SearchPart.Chords) && text.Length >= ChordsMinLength
            ? SearchChordsAsync(user, text, cancellationToken)
            : null;
        var musicTask = parts.Contains(SearchPart.Music) && text.Length >= MusicMinLength
            ? SearchMusicAsync(text, cancellationToken)
            : null;

        var found = chordsTask == null ? null : await chordsTask;

        IReadOnlyList<SearchSong>? songs = null;
        if (parts.Contains(SearchPart.Songs) || found != null)
        {
            var byChords = found == null ? [] : await SongsOfChordsAsync(accountId, found.Headers.Select(x => x.Id).ToList(), cancellationToken);
            var byTitle = parts.Contains(SearchPart.Songs) && text.Length > 0 ? await SongsByTitleAsync(accountId, text, cancellationToken) : [];
            songs = byChords.Concat(byTitle.Where(x => byChords.All(y => y.SongId != x.SongId))).ToList();
        }

        var inSongs = songs?.SelectMany(x => x.FoundChords).ToHashSet(StringComparer.Ordinal) ?? [];
        var (tracks, artists, musicFailures) = musicTask == null ? default : await musicTask;

        // marked once the database is free: the searches above share it
        if (tracks != null)
            tracks = (await music.Tracks(user, tracks, cancellationToken)).Tracks;

        return new()
        {
            Query = text,
            Songs = parts.Contains(SearchPart.Songs) || songs?.Count > 0 ? songs : null,
            Chords = found?.Headers.Where(x => !inSongs.Contains(x.Id)).Select(ChordsReader.ToHeader).ToList(),
            ChordsFailures = found?.Failures.Select(x => new SearchSourceFailure
            {
                Source = x.Source,
                Reason = x.Reason == HarmonyDB.Source.Api.Model.V2.Api.SearchFailureReason.Timeout ? Model.Search.SearchFailureReason.Timeout : Model.Search.SearchFailureReason.Unavailable,
            }).ToList(),
            Artists = artists,
            Recordings = tracks,
            MusicFailures = musicFailures,
        };
    }

    /// <summary>
    /// The playlists whose songs the search shows the person: those they are in, or follow, and show in their library,
    /// and the public ones they neither are in nor follow. A guest sees the public ones.
    /// </summary>
    public static IQueryable<Guid> SearchedPlaylists(ShelfDbContext db, Guid? accountId)
        => db.Playlists
            .Where(p => accountId != null && p.Members.Any(m => m.AccountId == accountId)
                ? p.Members.Any(m => m.AccountId == accountId && m.ShowInLibrary)
                : accountId != null && p.Access != PlaylistAccess.Private && db.PlaylistFollows.Any(f => f.PlaylistId == p.Id && f.AccountId == accountId)
                    ? db.PlaylistFollows.Any(f => f.PlaylistId == p.Id && f.AccountId == accountId && f.ShowInLibrary)
                    : p.Access == PlaylistAccess.Public)
            .Select(p => p.Id);

    private async Task<ChordsFound> SearchChordsAsync(ClaimsPrincipal user, string text, CancellationToken cancellationToken)
    {
        var caller = callers.For(user);
        var key = ("search-chords", text.ToLowerInvariant(), string.Join('\n', caller.Keys), caller.AccountId);
        if (cache.TryGetValue(key, out ChordsFound? kept))
            return kept!;

        HarmonyDB.Source.Api.Model.V2.Api.SearchResponse response;
        try
        {
            response = await index.Sources.Search(caller, text, cancellationToken);
        }
        catch (ShelfException exception) when (exception.StatusCode == StatusCodes.Status503ServiceUnavailable)
        {
            // the index is not there: the chords part failed, the others still answer
            return new([], [new() { Source = "index", Reason = HarmonyDB.Source.Api.Model.V2.Api.SearchFailureReason.Unavailable }]);
        }

        var result = new ChordsFound(response.Headers, response.Failures);

        // a search with a source missing is asked again next time
        if (response.Failures.Count == 0)
            cache.Set(key, result, KeepChords);

        return result;
    }

    private async Task<(IReadOnlyList<MusicTrack>?, IReadOnlyList<MusicArtist>?, IReadOnlyList<MusicService>?)> SearchMusicAsync(
        string text, CancellationToken cancellationToken)
    {
        async Task<(MusicService Service, IReadOnlyList<MusicTrack> Tracks, IReadOnlyList<MusicArtist> Artists)?> One(MusicService service)
        {
            var key = ("search-music", service, text.ToLowerInvariant());
            if (cache.TryGetValue(key, out (IReadOnlyList<MusicTrack>, IReadOnlyList<MusicArtist>) kept))
                return (service, kept.Item1, kept.Item2);

            try
            {
                var (tracks, artists) = await music.SearchAsync(service, text, cancellationToken);
                var result = (tracks.Take(TracksPerService).ToList() as IReadOnlyList<MusicTrack>, artists.Take(ArtistsPerService).ToList() as IReadOnlyList<MusicArtist>);
                cache.Set(key, result, KeepMusic);
                return (service, result.Item1, result.Item2);
            }
            catch (ShelfException)
            {
                return null;
            }
        }

        var services = new[] { MusicService.Yandex, MusicService.Youtube };
        var answers = await Task.WhenAll(services.Select(One));
        var failures = services.Where((_, i) => answers[i] == null).ToList();
        var ok = answers.Where(x => x != null).Select(x => x!.Value).ToList();

        return (ok.SelectMany(x => x.Tracks).ToList(), ok.SelectMany(x => x.Artists).ToList(), failures);
    }

    private async Task<List<SearchSong>> SongsOfChordsAsync(Guid? accountId, IReadOnlyList<string> found, CancellationToken cancellationToken)
    {
        if (found.Count == 0)
            return [];

        var playlists = SearchedPlaylists(db, accountId);
        var rows = await db.PlaylistSongItems.AsNoTracking()
            .Where(i => i.Chords != null && found.Contains(i.Chords.SourceId) && playlists.Contains(i.Song.PlaylistId))
            .Select(i => new
            {
                i.SongId,
                i.Song.PlaylistId,
                PlaylistTitle = i.Song.Playlist.Title,
                i.Song.Title,
                i.Song.Artists,
                i.Chords!.SourceId,
            })
            .Take(500)
            .ToListAsync(cancellationToken);

        var order = found.Select((id, i) => (id, i)).ToDictionary(x => x.id, x => x.i, StringComparer.Ordinal);
        return rows
            .GroupBy(x => x.SongId)
            .Select(g => (first: g.Min(x => order[x.SourceId]), song: new SearchSong
            {
                PlaylistId = g.First().PlaylistId,
                PlaylistTitle = g.First().PlaylistTitle,
                SongId = g.Key,
                Title = g.First().Title,
                Artists = g.First().Artists,
                FoundChords = g.OrderBy(x => order[x.SourceId]).Select(x => x.SourceId).Distinct().ToList(),
            }))
            .OrderBy(x => x.first)
            .Select(x => x.song)
            .ToList();
    }

    private async Task<List<SearchSong>> SongsByTitleAsync(Guid? accountId, string text, CancellationToken cancellationToken)
    {
        var playlists = SearchedPlaylists(db, accountId);
        var songs = db.PlaylistSongs.AsNoTracking().Where(s => playlists.Contains(s.PlaylistId));
        foreach (var word in text.Split(' ').Take(8))
        {
            var pattern = $"%{word.Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_")}%";
            songs = songs.Where(s => EF.Functions.ILike(s.Title, pattern) || s.Artists.Any(a => EF.Functions.ILike(a, pattern)));
        }

        return await songs
            .OrderBy(s => s.Title)
            .ThenBy(s => s.Id)
            .Take(SongsByTitle)
            .Select(s => new SearchSong
            {
                PlaylistId = s.PlaylistId,
                PlaylistTitle = s.Playlist.Title,
                SongId = s.Id,
                Title = s.Title,
                Artists = s.Artists,
                FoundChords = new List<string>(),
            })
            .ToListAsync(cancellationToken);
    }

    private sealed record ChordsFound(IReadOnlyList<HarmonyDB.Source.Api.Model.V2.Header> Headers, IReadOnlyList<HarmonyDB.Source.Api.Model.V2.Api.SearchFailure> Failures);
}
