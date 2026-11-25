using HarmonyDB.Shelf.Api.Areas.App.Services;
using HarmonyDB.Shelf.Api.Foundation.Areas;
using HarmonyDB.Shelf.Database;
using HarmonyDB.Shelf.Model.App;
using Microsoft.AspNetCore.Http.HttpResults;

namespace HarmonyDB.Shelf.Api.Areas.App;

/// <summary>
/// The app itself: its health, its version, and what the frontend needs at start. Open to everyone.
/// </summary>
public sealed class AppArea : IShelfArea
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddHealthChecks().AddDbContextCheck<ShelfDbContext>("database");
        services.AddSingleton<VersionService>();
        services.AddSingleton<PublicConfigurationService>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/app").WithTags("App");

        group.MapGet("/health", GetHealth).WithName("GetHealth").Produces<HealthResponse>(StatusCodes.Status503ServiceUnavailable);
        group.MapGet("/version", (VersionService service) => TypedResults.Ok(service.Get())).WithName("GetVersion");
        group.MapGet("/configuration", (PublicConfigurationService service) => TypedResults.Ok(service.Get())).WithName("GetPublicConfiguration");
    }

    /// <summary>
    /// 200 while healthy (or degraded), 503 when not.
    /// </summary>
    private static async Task<Results<Ok<HealthResponse>, JsonHttpResult<HealthResponse>>> GetHealth(
        Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckService healthChecks,
        CancellationToken cancellationToken)
    {
        var report = await healthChecks.CheckHealthAsync(cancellationToken);
        var response = new HealthResponse { Status = Enum.Parse<HealthStatus>(report.Status.ToString()) };
        return response.Status == HealthStatus.Unhealthy
            ? TypedResults.Json(response, statusCode: StatusCodes.Status503ServiceUnavailable)
            : TypedResults.Ok(response);
    }
}
