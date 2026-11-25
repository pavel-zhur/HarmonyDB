using System.Security.Claims;
using HarmonyDB.Shelf.Api.Areas.Chords;
using HarmonyDB.Shelf.Api.Areas.OwnChords.Services;
using HarmonyDB.Shelf.Api.Clients.OwnChords;
using HarmonyDB.Shelf.Api.Common;
using HarmonyDB.Shelf.Api.Common.Jobs;
using HarmonyDB.Shelf.Api.Foundation.Areas;
using HarmonyDB.Shelf.Api.Foundation.Permissions;
using HarmonyDB.Shelf.Model.OwnChords;

namespace HarmonyDB.Shelf.Api.Areas.OwnChords;

/// <summary>
/// The person's own chords: written in the app (a new song, or a new variant of a song), changed with every version
/// kept, deleted; how a text will look before it is saved; the text of other chords to start from (one's own variant,
/// or a correction of an analysis by hand). Every endpoint is the person's own session's.
/// </summary>
public sealed class OwnChordsArea : IShelfArea
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOwnChordsClient(configuration);
        services.AddChordsReading(configuration);
        services.AddShelfCommon();
        services.AddScoped<OwnChordsService>();
        services.AddOptions<OwnChordsPlacingOptions>().Bind(configuration.GetSection(OwnChordsPlacingOptions.SectionName))
            .Validate(x => x.Pause > TimeSpan.Zero && x.GiveUpAfter > TimeSpan.Zero, $"{OwnChordsPlacingOptions.SectionName}: the pause or the time to give up after is not positive.")
            .ValidateOnStart();
        services.AddJobHandler<OwnChordsPlacingJob, OwnChordsPlacingResult>(configuration, OwnChordsService.PlacingJobKind);
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/own-chords").WithTags("OwnChords").RequireSignIn();

        group.MapGet("", (ClaimsPrincipal user, OwnChordsService service, CancellationToken cancellationToken)
            => service.ListAsync(user, cancellationToken)).WithName("GetOwnChordsList");

        group.MapPost("", (ClaimsPrincipal user, CreateOwnChordsRequest request, OwnChordsService service, CancellationToken cancellationToken)
            => service.CreateAsync(user, request, cancellationToken)).WithName("CreateOwnChords");

        group.MapGet("/{id:guid}", (ClaimsPrincipal user, Guid id, OwnChordsService service, CancellationToken cancellationToken)
            => service.GetAsync(user, id, cancellationToken)).WithName("GetOwnChords");

        // a new version
        group.MapPut("/{id:guid}", (ClaimsPrincipal user, Guid id, UpdateOwnChordsRequest request, OwnChordsService service, CancellationToken cancellationToken)
            => service.UpdateAsync(user, id, request, cancellationToken)).WithName("UpdateOwnChords");

        group.MapDelete("/{id:guid}", async (ClaimsPrincipal user, Guid id, OwnChordsService service, CancellationToken cancellationToken) =>
        {
            await service.DeleteAsync(user, id, cancellationToken);
            return TypedResults.NoContent();
        }).WithName("DeleteOwnChords");

        group.MapPost("/preview", (OwnChordsPreviewRequest request, OwnChordsService service, CancellationToken cancellationToken)
            => service.PreviewAsync(request.Contents, cancellationToken)).WithName("PreviewOwnChords");

        // the text of these chords to start own chords from
        group.MapGet("/draft/{chordsId}", (ClaimsPrincipal user, string chordsId, OwnChordsService service, CancellationToken cancellationToken)
            => service.DraftAsync(user, chordsId, cancellationToken)).WithName("GetOwnChordsDraft");
    }
}
