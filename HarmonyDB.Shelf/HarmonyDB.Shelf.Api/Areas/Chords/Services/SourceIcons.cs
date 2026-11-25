using HarmonyDB.Shelf.Api.Clients.Index;
using HarmonyDB.Shelf.Api.Foundation.Errors;
using HarmonyDB.Shelf.Model;
using HarmonyDB.Shelf.Model.Chords;
using HarmonyDB.Source.Api.Client.V2;
using Microsoft.Extensions.Caching.Memory;

namespace HarmonyDB.Shelf.Api.Areas.Chords.Services;

/// <summary>
/// The sources' pictures by their opaque names, through the index's relay; kept for an hour.
/// </summary>
public sealed class SourceIcons(IndexClient index, IMemoryCache cache)
{
    private static readonly TimeSpan Keep = TimeSpan.FromHours(1);

    public async Task<SourceIcon> GetAsync(string name, CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(("source-icon", name), out SourceIcon? kept))
            return kept!;

        SourceIcon? icon;
        try
        {
            icon = await index.Sources.GetIcon(name, cancellationToken);
        }
        catch (SourceUnavailableException)
        {
            throw new ShelfException(StatusCodes.Status503ServiceUnavailable, ErrorCodes.Unavailable);
        }

        if (icon == null)
            throw ShelfException.NotFound(ChordsErrorCodes.NoSuchIcon);

        cache.Set(("source-icon", name), icon, Keep);
        return icon;
    }
}
