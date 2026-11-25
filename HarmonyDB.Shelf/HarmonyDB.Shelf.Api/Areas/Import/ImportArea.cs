using System.Security.Claims;
using HarmonyDB.Shelf.Api.Areas.Chords;
using HarmonyDB.Shelf.Api.Areas.Import.Services;
using HarmonyDB.Shelf.Api.Common;
using HarmonyDB.Shelf.Api.Common.Jobs;
using HarmonyDB.Shelf.Api.Foundation.Areas;
using HarmonyDB.Shelf.Api.Foundation.Permissions;
using HarmonyDB.Shelf.Model.Import;

namespace HarmonyDB.Shelf.Api.Areas.Import;

/// <summary>
/// Importing chords: a batch of links to pages the sources know, as a job whose result says what became of each link
/// (<see cref="ImportResult"/>); the chords go to the person's playlist of imported chords. The other ways to import
/// (files, photos, the bot) are "soon" in the app and have no endpoints.
/// </summary>
public sealed class ImportArea : IShelfArea
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddChordsReading(configuration);
        services.AddShelfCommon();
        services.AddScoped<ImportService>();
        services.AddJobHandler<ImportJob, ImportResult>(configuration, ImportService.JobKind);
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/import").WithTags("Import").RequireSignIn();

        group.MapPost("/links", (ClaimsPrincipal user, ImportLinksRequest request, ImportService service, CancellationToken cancellationToken)
            => service.StartAsync(user, request, cancellationToken)).WithName("ImportLinks");
    }
}
