using System.Security.Claims;
using HarmonyDB.Shelf.Api.Areas.Library.Services;
using HarmonyDB.Shelf.Api.Areas.Playlists.Services;
using HarmonyDB.Shelf.Api.Foundation.Areas;
using HarmonyDB.Shelf.Api.Foundation.Permissions;
using HarmonyDB.Shelf.Model.Playlists;
using Microsoft.AspNetCore.Mvc;

namespace HarmonyDB.Shelf.Api.Areas.Library;

/// <summary>
/// The person's library — everything from their playlists shown in it, plus the chords they marked or keep a key or a
/// personal note on — as a list of songs (filtered, sorted, paged, searched as they type); and what they opened lately.
/// </summary>
public sealed class LibraryArea : IShelfArea
{
    public void AddServices(IServiceCollection services, IConfiguration configuration) => LibraryServices.Add(services, configuration);

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/library").WithTags("Library").RequireSignIn();

        group.MapGet("", (ClaimsPrincipal user, string? q, string? sort, string? who, string? mark, int? offset, int? limit, SongLists lists, CancellationToken cancellationToken)
            => lists.LibraryAsync(user, new(q, SongListQuery.ParseSort(sort), who, mark, offset ?? 0, limit ?? 200), cancellationToken)).WithName("GetLibrary");

        group.MapGet("/recent", (ClaimsPrincipal user, int? limit, RecentService recent, CancellationToken cancellationToken)
            => recent.ListAsync(user, limit ?? 50, cancellationToken)).WithName("GetRecent");

        // where chords are among the person's own playlists, with their "here" keys: ?chords=<id>&chords=<id> (at most 200)
        group.MapGet("/places", (ClaimsPrincipal user, [FromQuery] string[] chords, PlacesService places, CancellationToken cancellationToken)
            => places.GetAsync(user.GetAccountId()!.Value, chords, cancellationToken)).WithName("GetChordsPlaces");
    }
}
