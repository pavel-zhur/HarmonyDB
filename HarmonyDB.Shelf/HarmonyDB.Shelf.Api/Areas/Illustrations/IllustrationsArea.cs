using System.Security.Claims;
using HarmonyDB.Shelf.Api.Areas.Chords;
using HarmonyDB.Shelf.Api.Areas.Illustrations.Services;
using HarmonyDB.Shelf.Api.Clients.Illustrations;
using HarmonyDB.Shelf.Api.Common.Jobs;
using HarmonyDB.Shelf.Api.Foundation.Areas;
using HarmonyDB.Shelf.Api.Foundation.Errors;
using HarmonyDB.Shelf.Api.Foundation.Permissions;
using HarmonyDB.Shelf.Model;
using HarmonyDB.Shelf.Model.Illustrations;
using Microsoft.Extensions.Options;

namespace HarmonyDB.Shelf.Api.Areas.Illustrations;

/// <summary>
/// The illustrations of chords: seen by whoever the chords are open to, drawn on the server by a preset as a job by the
/// people with the permission <see cref="BuiltInPermissions.IllustrationsGenerate"/>. The pictures themselves are
/// served here, by their id.
/// </summary>
public sealed class IllustrationsArea : IShelfArea
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<IllustrationsOptions>().Bind(configuration.GetSection(IllustrationsOptions.SectionName))
            .Validate(x => x.DailyImageLimit > 0, $"{IllustrationsOptions.SectionName}:{nameof(IllustrationsOptions.DailyImageLimit)} is not positive.")
            .ValidateOnStart();
        services.AddIllustrationsClient(configuration);
        services.AddChordsReading(configuration);
        services.AddScoped<IllustrationsService>();
        services.AddJobHandler<IllustrationsJob, IllustrationsJobResult>(configuration, IllustrationsService.JobKind);
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/illustrations").WithTags("Illustrations");

        group.MapGet("/presets", (IllustrationsService service, CancellationToken cancellationToken)
            => service.GetPresetsAsync(cancellationToken)).WithName("GetIllustrationPresets");

        group.MapGet("/{chordsId}", (ClaimsPrincipal user, string chordsId, IllustrationsService service, CancellationToken cancellationToken)
            => service.GetAsync(user, chordsId, cancellationToken)).WithName("GetIllustrations");

        // a batch drawn by the preset, as a job; while one is drawn for the chords, nothing more is ordered
        group.MapPost("/{chordsId}", async (ClaimsPrincipal user, string chordsId, DrawIllustrationsRequest request, IllustrationsService service, CancellationToken cancellationToken) =>
            {
                await service.DrawAsync(user, chordsId, request.Preset, cancellationToken);
                return await service.GetAsync(user, chordsId, cancellationToken);
            })
            .RequireSignIn()
            .RequirePermission(BuiltInPermissions.IllustrationsGenerate)
            .WithName("DrawIllustrations");

        group.MapGet("/images/{id:guid}/{size:int}", async (HttpContext context, Guid id, int size, IllustrationsClient illustrations, CancellationToken cancellationToken) =>
            {
                var image = await illustrations.GetImageAsync(id, size, cancellationToken) ?? throw ShelfException.NotFound();
                // a picture never changes
                context.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
                return Results.File(image, "image/jpeg");
            })
            .Produces(StatusCodes.Status200OK, contentType: "image/jpeg")
            .WithName("GetIllustrationImage");
    }
}
