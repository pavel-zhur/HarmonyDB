using System.Security.Claims;
using System.Threading.RateLimiting;
using HarmonyDB.Shelf.Api.Areas.Chords;
using HarmonyDB.Shelf.Api.Areas.Media;
using HarmonyDB.Shelf.Api.Areas.Media.Services;
using HarmonyDB.Shelf.Api.Areas.Playlists.Services;
using HarmonyDB.Shelf.Api.Areas.Share.Services;
using HarmonyDB.Shelf.Api.Foundation.Areas;
using HarmonyDB.Shelf.Api.Foundation.Network;
using HarmonyDB.Shelf.Api.Foundation.Permissions;
using HarmonyDB.Shelf.Model.Share;
using Microsoft.Extensions.Options;

namespace HarmonyDB.Shelf.Api.Areas.Share;

/// <summary>
/// Links to a song of a playlist, to chords, to a playlist of <c>link</c> access; and what a link opens, for guests too:
/// a link of chords also gives their content and their media (as the link's carried access opens them, when it carries
/// its sharer's; the media addresses under the media's limit per client address).
/// </summary>
public sealed class ShareArea : IShelfArea
{
    public const string OpenRateLimit = "share-open";

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        LibraryServices.Add(services, configuration);
        services.AddOptions<ShareOptions>().Bind(configuration.GetSection(ShareOptions.SectionName));

        // per client address: a link and the chords it opens count together
        services.AddShelfRateLimit(OpenRateLimit, context =>
        {
            var settings = context.RequestServices.GetRequiredService<IOptionsMonitor<ShareOptions>>().CurrentValue;
            return RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? string.Empty,
                _ => new FixedWindowRateLimiterOptions { PermitLimit = settings.OpenRateLimitPermits, Window = settings.OpenRateLimitWindow, QueueLimit = 0 });
        });
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/share").WithTags("Share");

        group.MapPost("", (ClaimsPrincipal user, CreateShareRequest request, ShareService share, CancellationToken cancellationToken)
            => share.CreateAsync(user, request, cancellationToken)).RequireSignIn().WithName("CreateShareLink");

        // the caller's own link that carries their access to the chords, if there is one
        group.MapGet("/mine", (ClaimsPrincipal user, string? chords, ShareService share, CancellationToken cancellationToken)
            => share.MineAsync(user, chords, cancellationToken)).RequireSignIn().WithName("GetMyCarryingShareLink");

        group.MapGet("/{token}", (ClaimsPrincipal user, string token, ShareService share, CancellationToken cancellationToken)
            => share.OpenAsync(user, token, cancellationToken)).RequireRateLimiting(OpenRateLimit).WithName("OpenShareLink");

        group.MapDelete("/{token}", async (ClaimsPrincipal user, string token, ShareService share, CancellationToken cancellationToken) =>
        {
            await share.RemoveAsync(user, token, cancellationToken);
            return TypedResults.NoContent();
        }).RequireSignIn().WithName("RemoveShareLink");

        // The kinds are given as "sheet,timeline"; the sheet when none is given.
        group.MapGet("/{token}/chords", (ClaimsPrincipal user, string token, string? kinds, ShareService share, CancellationToken cancellationToken)
            => share.ContentAsync(user, token, ChordsArea.ParseKinds(kinds), cancellationToken)).RequireRateLimiting(OpenRateLimit).WithName("GetSharedChords");

        group.MapPost("/{token}/media", async (ClaimsPrincipal user, string token, ShareService share, MediaService media, CancellationToken cancellationToken) =>
            {
                var (id, caller) = await share.ChordsCallerAsync(user, token, cancellationToken);
                return await media.SignChordsAsync(caller, id, cancellationToken);
            })
            .AllowGuests()
            .RequireRateLimiting(MediaArea.AddressesRateLimit)
            .WithName("GetSharedChordsMedia");
    }
}
