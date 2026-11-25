using System.Security.Claims;
using HarmonyDB.Shelf.Api.Foundation.Errors;
using HarmonyDB.Shelf.Api.Foundation.Permissions;
using HarmonyDB.Shelf.Database;
using HarmonyDB.Shelf.Model.Me;
using HarmonyDB.Shelf.Model.Music;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace HarmonyDB.Shelf.Api.Areas.Music.Services;

/// <summary>
/// The music blocks of the home page that come from the person's own playlists: the artists they play, and tracks
/// similar to one of their playlists. (Genres and moods, and the chart, are the services' own pages.)
/// The playlists counted are those whose songs the person shows in their library.
/// </summary>
public sealed class MusicHome(ShelfDbContext db, MusicCatalog catalog, IMemoryCache cache)
{
    private const int ArtistsShown = 12;
    private const int SeedTracks = 25;
    private const int ParallelSearches = 4;

    private static readonly TimeSpan KeepArtists = TimeSpan.FromHours(1);
    private static readonly TimeSpan KeepArtistsByName = TimeSpan.FromDays(1);
    private static readonly TimeSpan KeepSimilar = TimeSpan.FromHours(6);

    /// <summary>
    /// The artists most often among the songs of the person's playlists, found on the service by their names.
    /// A guest has none.
    /// </summary>
    public async Task<MusicHomeArtists> GetArtistsAsync(ClaimsPrincipal user, MusicService service, CancellationToken cancellationToken)
    {
        if (user.GetAccountId() is not { } accountId)
            return new() { Artists = [] };

        // kept for a while per person; the artists by name, longer and for everyone
        return (await cache.GetOrCreateAsync(("music-home-artists", service, accountId), async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = KeepArtists;
            var names = (await MySongs(accountId).Select(x => x.Artists).ToListAsync(cancellationToken))
                .SelectMany(x => x)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .GroupBy(Normalize)
                .OrderByDescending(x => x.Count())
                .Take(ArtistsShown)
                .Select(x => x.First())
                .ToList();

            // a few searches at a time
            using var gate = new SemaphoreSlim(ParallelSearches);
            var found = await Task.WhenAll(names.Select(async name =>
            {
                await gate.WaitAsync(cancellationToken);
                try
                {
                    return await FindArtistAsync(service, name, cancellationToken);
                }
                finally
                {
                    gate.Release();
                }
            }));

            return new MusicHomeArtists { Artists = found.OfType<MusicArtist>().DistinctBy(x => x.Id).ToList() };
        }))!;
    }

    /// <summary>
    /// Tracks like those of a playlist of the person's (the one given, or the one changed last that has recordings on
    /// this service): Yandex recommends for its tracks; on YouTube, a radio from its first track.
    /// </summary>
    public async Task<MusicHomeSimilar> GetSimilarAsync(ClaimsPrincipal user, MusicService service, Guid? playlistId, CancellationToken cancellationToken)
    {
        if (user.GetAccountId() is not { } accountId)
            return new() { Tracks = [] };

        var databaseService = MusicShapes.ToDatabase(service);
        var playlists = db.PlaylistMembers.AsNoTracking()
            .Where(x => x.AccountId == accountId && x.ShowInLibrary)
            .Select(x => x.Playlist);
        if (playlistId != null)
        {
            playlists = playlists.Where(x => x.Id == playlistId);
            if (!await playlists.AnyAsync(cancellationToken))
                throw ShelfException.NotFound();
        }

        var candidates = await playlists
            .OrderByDescending(x => x.UpdatedAt)
            .Select(x => new { x.Id, x.Title })
            .Take(playlistId == null ? 5 : 1)
            .ToListAsync(cancellationToken);

        // the recordings on the service that the songs hold, else those found for their chords
        foreach (var playlist in candidates)
        {
            var seeds = await db.PlaylistSongItems.AsNoTracking()
                .Where(i => i.Song.PlaylistId == playlist.Id && i.Recording != null && i.Recording.Service == databaseService)
                .OrderBy(i => i.Song.Position)
                .Select(i => i.Recording!.ServiceId)
                .Take(SeedTracks)
                .ToListAsync(cancellationToken);
            if (seeds.Count == 0)
            {
                seeds = await db.ChordsRecordings.AsNoTracking()
                    .Where(r => r.Recording.Service == databaseService
                                && db.PlaylistSongItems.Any(i => i.Song.PlaylistId == playlist.Id && i.ChordsId == r.ChordsId))
                    .OrderBy(r => r.Rank)
                    .Select(r => r.Recording.ServiceId)
                    .Take(SeedTracks)
                    .ToListAsync(cancellationToken);
            }

            if (seeds.Count == 0)
                continue;

            seeds = seeds.Distinct().ToList();
            var tracks = await cache.GetOrCreateAsync(("music-home-similar", service, string.Join(',', seeds)), async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = KeepSimilar;
                return service == MusicService.Yandex
                    ? await catalog.RecommendAsync(seeds, cancellationToken)
                    : await catalog.GetRadioTracksAsync(service, seeds[0], cancellationToken);
            });

            return new()
            {
                PlaylistId = playlist.Id,
                PlaylistTitle = playlist.Title,
                Tracks = (await catalog.Tracks(user, tracks!, cancellationToken)).Tracks,
            };
        }

        return new() { Tracks = [] };
    }

    private IQueryable<Database.Model.PlaylistSong> MySongs(Guid accountId)
        => db.PlaylistMembers.AsNoTracking()
            .Where(x => x.AccountId == accountId && x.ShowInLibrary)
            .SelectMany(x => x.Playlist.Songs);

    private async Task<MusicArtist?> FindArtistAsync(MusicService service, string name, CancellationToken cancellationToken)
        => await cache.GetOrCreateAsync(("music-artist-by-name", service, Normalize(name)), async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = KeepArtistsByName;
            var (_, artists) = await catalog.SearchAsync(service, name, cancellationToken);
            return artists.FirstOrDefault(x => x.Name != null && Normalize(x.Name) == Normalize(name));
        });

    private static string Normalize(string name) => name.Trim().ToLowerInvariant().Replace('ё', 'е');
}
