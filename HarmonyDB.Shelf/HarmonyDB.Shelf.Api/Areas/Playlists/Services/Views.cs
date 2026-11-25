using System.Security.Claims;
using HarmonyDB.Shelf.Api.Areas.Chords.Services;
using HarmonyDB.Shelf.Database;
using HarmonyDB.Shelf.Database.Model;
using HarmonyDB.Shelf.Model.Chords;
using Microsoft.EntityFrameworkCore;
using M = HarmonyDB.Shelf.Model.Playlists;

namespace HarmonyDB.Shelf.Api.Areas.Playlists.Services;

/// <summary>
/// The API's views of the rows: people, chords, recordings, playlists' summaries.
/// </summary>
public sealed class Views(ShelfDbContext db, ChordsReader reader)
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
                ShowInLibrary = mine?.ShowInLibrary,
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
}
