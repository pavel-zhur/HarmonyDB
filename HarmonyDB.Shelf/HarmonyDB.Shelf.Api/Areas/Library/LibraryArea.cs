using System.Security.Claims;
using System.Threading.RateLimiting;
using HarmonyDB.Shelf.Api.Areas.Library.Services;
using HarmonyDB.Shelf.Api.Areas.Playlists.Services;
using HarmonyDB.Shelf.Api.Foundation.Areas;
using HarmonyDB.Shelf.Api.Foundation.Network;
using HarmonyDB.Shelf.Api.Foundation.Permissions;
using HarmonyDB.Shelf.Model.Playlists;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace HarmonyDB.Shelf.Api.Areas.Library;

/// <summary>
/// The person's library — everything from their playlists shown in it, plus the chords they marked or keep a key or a
/// personal note on — as a list of songs (filtered, sorted, paged, searched as they type); and what they opened lately.
/// </summary>
public sealed class LibraryArea : IShelfArea
{
    public const string OfflineRateLimit = "offline";

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        LibraryServices.Add(services, configuration);
        services.AddOptions<LibraryOptions>().Bind(configuration.GetSection(LibraryOptions.SectionName));
        services.AddScoped<OfflineService>();

        // per person: the endpoints are the signed-in person's
        services.AddShelfRateLimit(OfflineRateLimit, context =>
        {
            var settings = context.RequestServices.GetRequiredService<IOptionsMonitor<LibraryOptions>>().CurrentValue;
            return RateLimitPartition.GetFixedWindowLimiter(
                context.User.GetAccountId()?.ToString() ?? string.Empty,
                _ => new FixedWindowRateLimiterOptions { PermitLimit = settings.OfflineRateLimitPermits, Window = settings.OfflineRateLimitWindow, QueueLimit = 0 });
        });
    }

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

        // the chords of the library that have an illustration, by title: ?offset=&limit= (at most 200, 100 by default)
        group.MapGet("/illustrated", (ClaimsPrincipal user, int? offset, int? limit, IllustratedService illustrated, CancellationToken cancellationToken)
            => illustrated.ListAsync(user, offset ?? 0, limit ?? 100, cancellationToken)).WithName("GetIllustratedLibrary");

        // the library kept on a device, to open without a connection: the chords' ids (again only when the library
        // changed since the watermark sent), then their content, at most 50 a call. Media (recordings, separate
        // instruments, the files the content names, pictures) are not part of it.
        group.MapGet("/offline/chords", (ClaimsPrincipal user, long? since, OfflineService offline, CancellationToken cancellationToken)
                => offline.ListAsync(user, since, cancellationToken))
            .RequireRateLimiting(OfflineRateLimit)
            .WithName("GetOfflineChords")
            .WithDescription("The ids of the chords of the person's library, for a device that keeps it; with since, only whether anything changed. Media are not part of the offline library.");

        group.MapPost("/offline/contents", (ClaimsPrincipal user, OfflineContentsRequest request, OfflineService offline, CancellationToken cancellationToken)
                => offline.GetContentsAsync(user, request, cancellationToken))
            .RequireRateLimiting(OfflineRateLimit)
            .WithName("GetOfflineContents")
            .WithDescription("The headers and content of up to 50 chords of the person's library. Media (recordings, separate instruments, files, pictures) are not part of the offline library.");
    }
}
