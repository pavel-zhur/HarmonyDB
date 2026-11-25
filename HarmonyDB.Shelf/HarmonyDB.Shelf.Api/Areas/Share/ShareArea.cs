using System.Security.Claims;
using HarmonyDB.Shelf.Api.Areas.Playlists.Services;
using HarmonyDB.Shelf.Api.Areas.Share.Services;
using HarmonyDB.Shelf.Api.Foundation.Areas;
using HarmonyDB.Shelf.Api.Foundation.Permissions;
using HarmonyDB.Shelf.Model.Share;

namespace HarmonyDB.Shelf.Api.Areas.Share;

/// <summary>
/// Links to a song of a playlist, to chords, to a playlist of <c>link</c> access; and what a link opens, for guests too.
/// </summary>
public sealed class ShareArea : IShelfArea
{
    public void AddServices(IServiceCollection services, IConfiguration configuration) => LibraryServices.Add(services, configuration);

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/share").WithTags("Share");

        group.MapPost("", (ClaimsPrincipal user, CreateShareRequest request, ShareService share, CancellationToken cancellationToken)
            => share.CreateAsync(user, request, cancellationToken)).RequireSignIn().WithName("CreateShareLink");

        group.MapGet("/{token}", (ClaimsPrincipal user, string token, ShareService share, CancellationToken cancellationToken)
            => share.OpenAsync(user, token, cancellationToken)).WithName("OpenShareLink");
    }
}
