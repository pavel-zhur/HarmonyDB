using System.Security.Claims;
using HarmonyDB.Shelf.Api.Areas.Chords;
using HarmonyDB.Shelf.Api.Foundation.Areas;
using HarmonyDB.Shelf.Api.Foundation.Errors;
using HarmonyDB.Shelf.Api.Foundation.Permissions;
using HarmonyDB.Shelf.Model;

namespace HarmonyDB.Shelf.Api.Areas.AnalysisTools;

/// <summary>
/// The index's analysis tools, for an item of chords by its id (<c>/api/analysis</c>): detected keys with their
/// confidence, loops, items with the same progression. Open to guests. Starting a recognition of a recording is
/// "soon".
/// </summary>
public sealed class AnalysisToolsArea : IShelfArea
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddChordsReading(configuration);
        services.AddScoped<Services.AnalysisTools>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/analysis").WithTags("Analysis");

        group.MapGet("/chords/{id}", (ClaimsPrincipal user, string id, Services.AnalysisTools tools, CancellationToken cancellationToken)
            => tools.GetAsync(user, id, cancellationToken)).WithName("GetChordsAnalysis");

        // the progression as chord names, space-separated: "Am F C G"
        group.MapGet("/similar", (ClaimsPrincipal user, string progression, int? page, Services.AnalysisTools tools, CancellationToken cancellationToken)
            => tools.GetSimilarAsync(user, progression, page ?? 1, cancellationToken)).WithName("GetSimilarChords");

        // recognizing chords from a recording, a link, an own file or the microphone
        group.MapPost("/recognitions", IResult () => throw new ShelfException(StatusCodes.Status501NotImplemented, ErrorCodes.NotYet))
            .RequireSignIn()
            .WithName("StartRecognition");
    }
}
