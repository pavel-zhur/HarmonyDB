using System.Security.Claims;
using HarmonyDB.Shelf.Api.Areas.Playlists.Services;
using HarmonyDB.Shelf.Api.Areas.Sync.Services;
using HarmonyDB.Shelf.Api.Foundation.Areas;
using HarmonyDB.Shelf.Api.Foundation.Permissions;
using HarmonyDB.Shelf.Model.Sync;

namespace HarmonyDB.Shelf.Api.Areas.Sync;

/// <summary>
/// The small changes the browser applies at once and queues (marks, keys, notes, what was opened), sent in a batch,
/// answered with what changed since the browser's watermark; and undo, for every change of the API.
/// </summary>
public sealed class SyncArea : IShelfArea
{
    public void AddServices(IServiceCollection services, IConfiguration configuration) => LibraryServices.Add(services, configuration);

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/sync").WithTags("Sync").RequireSignIn();

        group.MapPost("", (ClaimsPrincipal user, SyncRequest request, SyncService sync, CancellationToken cancellationToken)
            => sync.SyncAsync(user, request, cancellationToken)).WithName("Sync");

        group.MapPost("/undo", async (ClaimsPrincipal user, UndoRequest request, UndoTokens undo, CancellationToken cancellationToken) =>
        {
            await undo.UndoAsync(user.GetAccountId()!.Value, request.Token, cancellationToken);
            return Results.NoContent();
        }).WithName("Undo");
    }
}
