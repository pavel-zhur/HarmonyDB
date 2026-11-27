using System.Security.Claims;
using HarmonyDB.Shelf.Api.Areas.Illustrations.Services;
using HarmonyDB.Shelf.Api.Areas.Playlists.Services;
using HarmonyDB.Shelf.Api.Foundation.Permissions;
using M = HarmonyDB.Shelf.Model.Playlists;

namespace HarmonyDB.Shelf.Api.Areas.Library.Services;

/// <summary>
/// The chords of the person's library that have an illustration. The illustrations are the illustrations service's:
/// it is asked for the library's chords in batches (and its answers kept a few minutes, as for the covers); those with
/// one are given by title, as the shelf keeps them (no source is asked).
/// </summary>
public sealed class IllustratedService(SongLists lists, Views views)
{
    public const int MaxLimit = 200;

    public async Task<M.IllustratedChordsList> ListAsync(ClaimsPrincipal user, int offset, int limit, CancellationToken cancellationToken)
    {
        offset = Math.Max(0, offset);
        limit = Math.Clamp(limit, 1, MaxLimit);

        var chords = await lists.LibraryChordsAsync(user.GetAccountId()!.Value, cancellationToken);
        var pictures = await views.IllustrationCoversAsync(chords.Select(x => x.Chords.SourceId).ToList(), cancellationToken);
        var open = chords.Select(x => x.Chords)
            .Where(x => pictures.ContainsKey(x.SourceId))
            .OrderBy(x => SongLists.Normalize(x.Title), StringComparer.Ordinal)
            .ThenBy(x => SongLists.Normalize(x.Artists.FirstOrDefault()), StringComparer.Ordinal)
            .ThenBy(x => x.SourceId, StringComparer.Ordinal)
            .ToList();

        return new()
        {
            Total = open.Count,
            Offset = offset,
            Items = open.Skip(offset).Take(limit).Select(x => new M.IllustratedChords
            {
                Chords = x.SourceId,
                Title = x.Title,
                Artists = x.Artists,
                PictureUrl = $"{IllustrationsService.ImagesPath}/{pictures[x.SourceId]}/{Views.CoverSize}",
            }).ToList(),
        };
    }
}
