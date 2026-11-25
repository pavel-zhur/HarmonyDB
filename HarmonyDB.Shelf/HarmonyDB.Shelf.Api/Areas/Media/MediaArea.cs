using System.Security.Claims;
using System.Threading.RateLimiting;
using HarmonyDB.Shelf.Api.Areas.Chords;
using HarmonyDB.Shelf.Api.Areas.Media.Services;
using HarmonyDB.Shelf.Api.Clients.Audio;
using HarmonyDB.Shelf.Api.Foundation.Areas;
using HarmonyDB.Shelf.Api.Foundation.Permissions;
using HarmonyDB.Shelf.Model.Media;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace HarmonyDB.Shelf.Api.Areas.Media;

/// <summary>
/// Sound and video for everyone, guests too: short-lived signed addresses, given for a music service's track or for
/// the material of an item of chords open to the asker (new addresses are limited per client address), and the
/// addresses themselves, streamed from the music service with Range by the service account or anonymously.
/// </summary>
public sealed class MediaArea : IShelfArea
{
    public const string AddressesRateLimit = "media-addresses";

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<MediaOptions>().Bind(configuration.GetSection(MediaOptions.SectionName)).ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<MediaOptions>, MediaOptionsValidator>());
        services.AddChordsReading(configuration);
        services.AddAudioMusicClients(configuration);
        services.AddSingleton<MediaSigner>();
        services.AddScoped<MediaService>();

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(AddressesRateLimit, context =>
            {
                var settings = context.RequestServices.GetRequiredService<IOptionsMonitor<MediaOptions>>().CurrentValue;
                return RateLimitPartition.GetFixedWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? string.Empty,
                    _ => new FixedWindowRateLimiterOptions { PermitLimit = settings.RateLimitPermits, Window = settings.RateLimitWindow, QueueLimit = 0 });
            });
        });
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/media").WithTags("Media");

        group.MapPost("/tracks", (TrackMediaRequest request, MediaService media) => media.SignTrack(request))
            .AllowGuests()
            .RequireRateLimiting(AddressesRateLimit)
            .WithName("GetTrackMedia");

        group.MapPost("/chords", (ClaimsPrincipal user, ChordsMediaRequest request, MediaService media, CancellationToken cancellationToken)
                => media.SignChordsAsync(user, request.Id, cancellationToken))
            .AllowGuests()
            .RequireRateLimiting(AddressesRateLimit)
            .WithName("GetChordsMedia");

        // The address itself: played by the browser's audio and video elements, not by the app's client.
        group.MapMethods("/{token}", [HttpMethods.Get, HttpMethods.Head], async (HttpContext context, string token, MediaService media) =>
            {
                await media.StreamAsync(context, token);
                return Results.Empty;
            })
            .ExcludeFromDescription();
    }
}
