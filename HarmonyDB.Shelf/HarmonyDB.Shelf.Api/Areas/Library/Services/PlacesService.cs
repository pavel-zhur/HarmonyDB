using HarmonyDB.Shelf.Api.Common;
using HarmonyDB.Shelf.Api.Foundation.Errors;
using HarmonyDB.Shelf.Database;
using HarmonyDB.Shelf.Database.Model;
using Microsoft.EntityFrameworkCore;
using M = HarmonyDB.Shelf.Model.Playlists;
using Views = HarmonyDB.Shelf.Api.Areas.Playlists.Services.Views;

namespace HarmonyDB.Shelf.Api.Areas.Library.Services;

/// <summary>
/// Where chords are among the person's own playlists, with their "here" keys for them: three queries, whatever the
/// number of chords and playlists.
/// </summary>
public sealed class PlacesService(ShelfDbContext db, SystemPlaylists systemPlaylists)
{
    public const int MaxChords = 200;

    public async Task<M.ChordsPlaces> GetAsync(Guid me, IReadOnlyList<string> chords, CancellationToken cancellationToken)
    {
        var ids = chords.Distinct(StringComparer.Ordinal).ToList();
        if (ids.Count > MaxChords || ids.Any(string.IsNullOrWhiteSpace))
            throw ShelfException.BadRequest();

        // the system playlists are made when first needed, as the list of the person's playlists makes them
        await systemPlaylists.EnsureAllAsync(me, cancellationToken);
        var playlists = await db.PlaylistMembers.Where(x => x.AccountId == me && x.Playlist.DeletedAt == null)
            .OrderBy(x => x.Playlist.SystemKind == PlaylistSystemKind.None ? 1 : 0).ThenBy(x => x.Playlist.SystemKind).ThenBy(x => x.JoinedAt)
            .Select(x => new
            {
                x.PlaylistId,
                x.Playlist.Title,
                x.Playlist.SystemKind,
                CanAdd = x.Role == PlaylistRole.Owner || x.Role == PlaylistRole.Admin || x.Playlist.AddPolicy == PlaylistAddPolicy.AllMembers,
            })
            .ToListAsync(cancellationToken);

        var places = await db.PlaylistSongItems.SeenBy(me)
            .Where(i => i.Chords != null && ids.Contains(i.Chords.SourceId) && i.Song.DeletedAt == null && i.Song.Playlist.DeletedAt == null
                        && i.Song.Playlist.Members.Any(m => m.AccountId == me && m.DeletedAt == null))
            .Select(i => new M.ChordsPlace { Chords = i.Chords!.SourceId, PlaylistId = i.Song.PlaylistId, SongId = i.SongId })
            .ToListAsync(cancellationToken);

        var keys = await db.Transpositions
            .Where(x => x.AccountId == me && x.PlaylistId != null && ids.Contains(x.Chords.SourceId) && x.Playlist!.DeletedAt == null)
            .Select(x => new M.ChordsHereKey { Chords = x.Chords.SourceId, PlaylistId = x.PlaylistId!.Value, Semitones = x.Semitones })
            .ToListAsync(cancellationToken);

        return new()
        {
            Playlists = playlists.Select(x => new M.MyPlaylist
            {
                Id = x.PlaylistId,
                Title = x.Title,
                SystemKind = Views.Map<M.PlaylistSystemKind>(x.SystemKind),
                CanAdd = x.CanAdd,
            }).ToList(),
            Places = places,
            Keys = keys,
        };
    }
}
