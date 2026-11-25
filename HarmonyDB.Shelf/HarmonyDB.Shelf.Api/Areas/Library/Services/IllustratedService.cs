using System.Security.Claims;
using HarmonyDB.Shelf.Api.Areas.Illustrations.Services;
using HarmonyDB.Shelf.Api.Areas.Playlists.Services;
using HarmonyDB.Shelf.Api.Foundation.Permissions;
using HarmonyDB.Shelf.Model.Chords;
using M = HarmonyDB.Shelf.Model.Playlists;

namespace HarmonyDB.Shelf.Api.Areas.Library.Services;

/// <summary>
/// The chords of the person's library that have an illustration. The illustrations are the illustrations service's:
/// it is asked for the library's chords in batches (and its answers kept a few minutes, as for the covers); of those
/// with one, the chords open to the person are given, by title.
/// </summary>
public sealed class IllustratedService(SongLists lists, Views views)
{
    public const int MaxLimit = 200;

    public async Task<M.IllustratedChordsList> ListAsync(ClaimsPrincipal user, int offset, int limit, CancellationToken cancellationToken)
    {
        offset = Math.Max(0, offset);
        limit = Math.Clamp(limit, 1, MaxLimit);

        var chords = await lists.LibraryChordsAsync(user.GetAccountId()!.Value, cancellationToken);
        var pictures = await views.IllustrationCoversAsync(chords.Select(x => x.SourceId).ToList(), cancellationToken);
        var open = (await views.ChordsAsync(user, pictures.Keys, cancellationToken)).Values
            .Where(x => x.State == ChordsState.Open)
            .OrderBy(x => SongLists.Normalize(x.Header!.Title), StringComparer.Ordinal)
            .ThenBy(x => SongLists.Normalize(x.Header!.Artists.FirstOrDefault()), StringComparer.Ordinal)
            .ThenBy(x => x.Id, StringComparer.Ordinal)
            .ToList();

        return new()
        {
            Total = open.Count,
            Offset = offset,
            Items = open.Skip(offset).Take(limit).Select(x => new M.IllustratedChords
            {
                Chords = x.Id,
                Title = x.Header!.Title,
                Artists = x.Header.Artists,
                PictureUrl = $"{IllustrationsService.ImagesPath}/{pictures[x.Id]}/{Views.CoverSize}",
            }).ToList(),
        };
    }
}
