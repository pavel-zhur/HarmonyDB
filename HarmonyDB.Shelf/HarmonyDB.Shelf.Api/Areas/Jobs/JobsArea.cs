using System.Security.Claims;
using HarmonyDB.Shelf.Api.Common.Jobs;
using HarmonyDB.Shelf.Api.Foundation.Areas;
using HarmonyDB.Shelf.Api.Foundation.Errors;
using HarmonyDB.Shelf.Api.Foundation.Permissions;
using HarmonyDB.Shelf.Model.Jobs;

namespace HarmonyDB.Shelf.Api.Areas.Jobs;

/// <summary>
/// The person's background work: what runs and what ran lately, each job's state, and the file a job made. The work
/// is started by the areas it belongs to (illustrations, the book, an import …).
/// </summary>
public sealed class JobsArea : IShelfArea
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddJobs(configuration);
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/jobs").WithTags("Jobs").RequireSignIn();

        // The jobs queued or running and those finished lately, newest first; of one kind, when it is given.
        group.MapGet("", (ClaimsPrincipal user, string? kind, JobQueue jobs, CancellationToken cancellationToken)
            => jobs.ListAsync(user.GetAccountId()!.Value, kind, cancellationToken)).WithName("GetJobs");

        group.MapGet("/{id:guid}", async (ClaimsPrincipal user, Guid id, JobQueue jobs, CancellationToken cancellationToken)
            => await jobs.GetAsync(user.GetAccountId()!.Value, id, cancellationToken)
               ?? throw ShelfException.NotFound(JobsErrorCodes.NoSuchJob)).WithName("GetJob");

        group.MapGet("/{id:guid}/file", async (ClaimsPrincipal user, Guid id, JobQueue jobs, CancellationToken cancellationToken) =>
            await jobs.GetFileAsync(user.GetAccountId()!.Value, id, cancellationToken) is { } file
                ? Results.File(file.Data, file.ContentType, file.FileName)
                : throw ShelfException.NotFound(JobsErrorCodes.NoFile))
            .Produces(StatusCodes.Status200OK, contentType: "application/octet-stream")
            .WithName("GetJobFile");
    }
}
