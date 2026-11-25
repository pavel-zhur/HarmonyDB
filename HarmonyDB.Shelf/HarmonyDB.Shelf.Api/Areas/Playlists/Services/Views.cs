using System.Security.Claims;
using HarmonyDB.Shelf.Api.Areas.Chords.Services;
using HarmonyDB.Shelf.Api.Areas.Illustrations.Services;
using HarmonyDB.Shelf.Api.Clients.Illustrations;
using HarmonyDB.Shelf.Database;
using HarmonyDB.Shelf.Database.Model;
using HarmonyDB.Shelf.Model.Chords;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using M = HarmonyDB.Shelf.Model.Playlists;

namespace HarmonyDB.Shelf.Api.Areas.Playlists.Services;

/// <summary>
/// The API's views of the rows: people, chords, recordings, playlists' summaries.
/// </summary>
public sealed class Views(ShelfDbContext db, ChordsReader reader, ChordsLookup lookup, IllustrationsClient illustrations, IMemoryCache cache)
{
    public const int FirstMembersShown = 6;

    public static TTo Map<TTo>(Enum value) where TTo : struct, Enum => Enum.Parse<TTo>(value.ToString());

    public async Task<Dictionary<Guid, M.PersonRef>> PeopleAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken)
    {
        var list = ids.Distinct().ToList();
        if (list.Count == 0)
            return [];

        return await db.Accounts.Where(x => list.Contains(x.Id))
            .Select(x => new M.PersonRef { Id = x.Id, Name = x.DisplayName, PictureUrl = x.PictureUrl, Gone = x.DeletedAt != null })
            .ToDictionaryAsync(x => x.Id, cancellationToken);
    }

    /// <summary>
    /// The chords as their sources give them to the caller now (open with the header and label for the caller, not open,
    /// unavailable), by their ids; asked through the chords' reader, which keeps the answers a while per item, keys and
    /// account.
    /// </summary>
    public async Task<Dictionary<string, ChordsAnswer>> ChordsAsync(ClaimsPrincipal user, IEnumerable<string> ids, CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, ChordsAnswer>(StringComparer.Ordinal);
        foreach (var chunk in ids.Distinct(StringComparer.Ordinal).Chunk(ChordsReader.MaxIds))
        {
            foreach (var answer in await reader.GetHeadersAsync(user, chunk, cancellationToken))
                result[answer.Id] = answer;
        }

        return result;
    }

    public static M.RecordingRef Recording(Recording x) => new()
    {
        Id = x.Id,
        Service = Map<Model.Me.MusicService>(x.Service),
        ServiceId = x.ServiceId,
        Title = x.Title,
        Artists = x.Artists,
        DurationSeconds = x.DurationSeconds,
        CoverUrl = x.CoverUrl,
    };

    public static M.SongRef Song(PlaylistSong x, IReadOnlyDictionary<Guid, M.PersonRef> people) => new()
    {
        Id = x.Id,
        PlaylistId = x.PlaylistId,
        Title = x.Title,
        Artists = x.Artists,
        Position = x.Position,
        AddedBy = x.AddedById is { } by ? people.GetValueOrDefault(by) : null,
        AddedAt = x.AddedAt,
    };

    /// <summary>
    /// The summaries of the playlists for the caller (<paramref name="viewer"/>: null for a guest), in the order given.
    /// </summary>
    public async Task<List<M.PlaylistSummary>> SummariesAsync(Guid? viewer, IReadOnlyList<Playlist> playlists, CancellationToken cancellationToken)
    {
        var ids = playlists.Select(x => x.Id).ToList();
        var songs = await db.PlaylistSongs.Where(x => ids.Contains(x.PlaylistId))
            .GroupBy(x => x.PlaylistId).Select(x => new { x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);
        var members = await db.PlaylistMembers.Where(x => ids.Contains(x.PlaylistId))
            .OrderBy(x => x.Role == PlaylistRole.Owner ? 0 : 1).ThenBy(x => x.JoinedAt).ThenBy(x => x.Id)
            .Select(x => new { x.PlaylistId, x.AccountId, x.Role, x.ShowInLibrary })
            .ToListAsync(cancellationToken);
        var byPlaylist = members.ToLookup(x => x.PlaylistId);
        var people = await PeopleAsync(
            playlists.Select(x => x.OwnerId).Concat(byPlaylist.SelectMany(x => x.Take(FirstMembersShown).Select(m => m.AccountId))),
            cancellationToken);
        var follows = viewer == null
            ? []
            : await db.PlaylistFollows.Where(x => x.AccountId == viewer && ids.Contains(x.PlaylistId)).ToDictionaryAsync(x => x.PlaylistId, x => x.ShowInLibrary, cancellationToken);
        var covers = await PlaylistCoversAsync(viewer, ids, cancellationToken);

        return playlists.Select(p =>
        {
            var mine = viewer == null ? null : byPlaylist[p.Id].FirstOrDefault(x => x.AccountId == viewer);
            return new M.PlaylistSummary
            {
                Id = p.Id,
                Title = p.Title,
                Description = p.Description,
                Access = Map<M.PlaylistAccess>(p.Access),
                AddPolicy = Map<M.PlaylistAddPolicy>(p.AddPolicy),
                SearchFirst = p.SearchFirst is { } first ? Map<Model.Me.MusicService>(first) : null,
                SystemKind = Map<M.PlaylistSystemKind>(p.SystemKind),
                Owner = people[p.OwnerId],
                MyRole = mine == null ? null : Map<M.PlaylistRole>(mine.Role),
                // a follow counts while the playlist is not private
                Following = mine == null && p.Access != PlaylistAccess.Private && follows.ContainsKey(p.Id),
                ShowInLibrary = mine?.ShowInLibrary ?? (p.Access != PlaylistAccess.Private && follows.TryGetValue(p.Id, out var shown) ? shown : (bool?)null),
                Covers = covers[p.Id].ToList(),
                SongCount = songs.GetValueOrDefault(p.Id),
                MemberCount = byPlaylist[p.Id].Count(),
                FirstMembers = byPlaylist[p.Id].Take(FirstMembersShown).Select(x => people[x.AccountId]).ToList(),
                // the origin, and its sync, are the importer's alone
                Origin = p.OriginService is { } service && viewer != null && p.ImportedById == viewer
                    ? new M.PlaylistOrigin
                    {
                        Service = Map<Model.Me.MusicService>(service),
                        Url = p.OriginUrl!,
                        CanSync = mine != null,
                        LastSyncedAt = p.LastSyncedAt,
                    }
                    : null,
                CreatedAt = p.CreatedAt,
                UpdatedAt = p.UpdatedAt,
            };
        }).ToList();
    }

    public async Task<M.PlaylistSummary> SummaryAsync(Guid? viewer, Playlist playlist, CancellationToken cancellationToken)
        => (await SummariesAsync(viewer, [playlist], cancellationToken))[0];


    public const int PlaylistCovers = 4;

    /// <summary>
    /// How many of a playlist's first songs are looked at for its covers.
    /// </summary>
    public const int PlaylistCoverSongs = 12;

    /// <summary>
    /// The side of a cover's picture, of the illustrations' sizes: a small one, for rows and tiles.
    /// </summary>
    public const int CoverSize = 256;

    private static readonly TimeSpan CoverKeep = TimeSpan.FromMinutes(5);

    /// <summary>
    /// The covers of the playlists for the person: those of their first songs that have one (<see cref="SongCoversAsync"/>),
    /// in order, at most <see cref="PlaylistCovers"/>. The first songs of all the playlists are read in one query.
    /// </summary>
    public async Task<ILookup<Guid, string>> PlaylistCoversAsync(Guid? viewer, IReadOnlyCollection<Guid> playlistIds, CancellationToken cancellationToken)
    {
        if (playlistIds.Count == 0)
            return Array.Empty<(Guid, string)>().ToLookup(x => x.Item1, x => x.Item2);

        var ids = playlistIds.Distinct().ToArray();
        var firsts = await db.Database.SqlQuery<FirstSong>($"""
            SELECT p.id AS playlist_id, s.id AS song_id, s.position AS position
            FROM unnest({ids}) AS p(id)
            CROSS JOIN LATERAL (
                SELECT s.id, s.position FROM playlist_songs s
                WHERE s.playlist_id = p.id AND s.deleted_at IS NULL
                ORDER BY s.position, s.id
                LIMIT {PlaylistCoverSongs}) s
            """).ToListAsync(cancellationToken);
        var covers = await SongCoversAsync(viewer, firsts.Select(x => x.SongId).ToList(), cancellationToken);
        return firsts
            .OrderBy(x => x.Position).ThenBy(x => x.SongId)
            .Where(x => covers.ContainsKey(x.SongId))
            .GroupBy(x => x.PlaylistId)
            .SelectMany(x => x.Take(PlaylistCovers).Select(s => (x.Key, Cover: covers[s.SongId])))
            .ToLookup(x => x.Key, x => x.Cover);
    }

    /// <summary>
    /// The covers of songs for the person, illustration first:
    /// <list type="number">
    /// <item>an illustration of the person's main variant of the song, else of its first variant that has one — of
    /// chords open to the person;</item>
    /// <item>else the cover of the recording the person chose for the song, else of its first recording that has one;</item>
    /// <item>else none.</item>
    /// </list>
    /// The illustrations are asked of their service in one call for all the songs, and kept a few minutes.
    /// </summary>
    public async Task<Dictionary<Guid, string>> SongCoversAsync(Guid? viewer, IReadOnlyCollection<Guid> songIds, CancellationToken cancellationToken)
    {
        if (songIds.Count == 0)
            return [];

        var ids = songIds.Distinct().ToList();
        var items = await db.PlaylistSongItems
            .Where(x => ids.Contains(x.SongId))
            .OrderBy(x => x.Position).ThenBy(x => x.Id)
            .Select(x => new { x.Id, x.SongId, SourceId = x.Chords == null ? null : x.Chords.SourceId, RecordingCover = x.Recording == null ? null : x.Recording.CoverUrl })
            .ToListAsync(cancellationToken);
        var preferences = viewer == null
            ? []
            : await db.PlaylistSongPreferences.Where(x => x.AccountId == viewer && ids.Contains(x.SongId))
                .Select(x => new { x.SongId, x.MainItemId, x.RecordingItemId })
                .ToDictionaryAsync(x => x.SongId, cancellationToken);

        // the illustrations of the chords open to the person
        var sourceIds = items.Where(x => x.SourceId != null).Select(x => x.SourceId!).Distinct(StringComparer.Ordinal).ToList();
        var person = viewer is { } id ? await lookup.PersonAsync(id, cancellationToken) : new ClaimsPrincipal(new ClaimsIdentity());
        var open = (await ChordsAsync(person, sourceIds, cancellationToken)).Values.Where(x => x.State == ChordsState.Open).Select(x => x.Id).ToList();
        var pictures = await IllustrationCoversAsync(open, cancellationToken);

        var result = new Dictionary<Guid, string>();
        foreach (var song in items.GroupBy(x => x.SongId))
        {
            var preference = preferences.GetValueOrDefault(song.Key);
            var illustrated = song.Where(x => x.SourceId != null && pictures.ContainsKey(x.SourceId))
                .OrderBy(x => x.Id == preference?.MainItemId ? 0 : 1)
                .FirstOrDefault();
            var recorded = song.Where(x => x.RecordingCover != null)
                .OrderBy(x => x.Id == preference?.RecordingItemId ? 0 : 1)
                .FirstOrDefault();
            var cover = illustrated != null
                ? $"{IllustrationsService.ImagesPath}/{pictures[illustrated.SourceId!]}/{CoverSize}"
                : recorded?.RecordingCover;
            if (cover != null)
                result[song.Key] = cover;
        }

        return result;
    }

    /// <summary>
    /// The illustration to show as the cover of each of the chords that have one; asked of the illustrations' service
    /// for those not kept, in one call, and kept a few minutes (also that there is none).
    /// </summary>
    private async Task<Dictionary<string, Guid>> IllustrationCoversAsync(IReadOnlyCollection<string> sourceIds, CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, Guid>(StringComparer.Ordinal);
        var missing = new List<string>();
        foreach (var id in sourceIds)
        {
            if (cache.TryGetValue(("illustration-cover", id), out Guid? kept))
            {
                if (kept is { } picture)
                    result[id] = picture;
            }
            else
            {
                missing.Add(id);
            }
        }

        if (missing.Count == 0)
            return result;

        var found = await illustrations.GetCoversAsync(missing, cancellationToken);
        foreach (var id in missing)
        {
            Guid? picture = found.TryGetValue(id, out var x) ? x : null;
            cache.Set(("illustration-cover", id), picture, CoverKeep);
            if (picture is { } p)
                result[id] = p;
        }

        return result;
    }

    public sealed class FirstSong
    {
        public Guid PlaylistId { get; init; }

        public Guid SongId { get; init; }

        public double Position { get; init; }
    }
}
