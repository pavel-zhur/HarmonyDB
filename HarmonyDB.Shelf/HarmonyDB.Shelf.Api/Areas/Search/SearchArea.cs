using System.Security.Claims;
using HarmonyDB.Shelf.Api.Areas.Chords;
using HarmonyDB.Shelf.Api.Areas.Music;
using HarmonyDB.Shelf.Api.Areas.Search.Services;
using HarmonyDB.Shelf.Api.Foundation.Areas;
using HarmonyDB.Shelf.Api.Foundation.Errors;
using HarmonyDB.Shelf.Model.Search;

namespace HarmonyDB.Shelf.Api.Areas.Search;

/// <summary>
/// The one search (<c>/api/search</c>): songs of the playlists the person sees, chords from the sources, artists and
/// recordings of the music services. Open to guests.
/// </summary>
public sealed class SearchArea : IShelfArea
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddChordsReading(configuration);
        services.AddMusicCatalog(configuration);
        services.AddScoped<SearchService>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        // the parts as "songs,chords,music"; all when none are given
        endpoints.MapGet("/api/search", (ClaimsPrincipal user, string? q, string? parts, SearchService search, CancellationToken cancellationToken)
                => search.SearchAsync(user, q, ParseParts(parts), cancellationToken))
            .WithTags("Search")
            .WithName("Search");
    }

    private static IReadOnlySet<SearchPart> ParseParts(string? parts)
        => string.IsNullOrWhiteSpace(parts)
            ? Enum.GetValues<SearchPart>().ToHashSet()
            : parts.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(x => Enum.TryParse<SearchPart>(x, true, out var part) && Enum.IsDefined(part) && !int.TryParse(x, out _)
                    ? part
                    : throw ShelfException.BadRequest())
                .ToHashSet();
}
