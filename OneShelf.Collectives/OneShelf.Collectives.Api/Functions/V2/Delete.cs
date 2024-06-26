using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OneShelf.Authorization.Api.Client;
using OneShelf.Collectives.Api.Model;
using OneShelf.Collectives.Api.Model.V2;
using OneShelf.Collectives.Api.Services;
using OneShelf.Collectives.Database;
using OneShelf.Common.Api.WithAuthorization;
using OneShelf.Common.Database.Songs.Model.Enums;
using OneShelf.Common.Database.Songs.Services;

namespace OneShelf.Collectives.Api.Functions.V2;

public class Delete : AuthorizationFunctionBase<DeleteRequest, DeleteResponse>
{
    private readonly CollectivesCosmosDatabase _collectivesCosmosDatabase;
    private readonly SongsOperations _songsOperations;
    private readonly ILogger<Delete> _logger;

    public Delete(ILoggerFactory loggerFactory, AuthorizationApiClient authorizationApiClient, CollectivesCosmosDatabase collectivesCosmosDatabase, SongsOperations songsOperations, SecurityContext securityContext) 
        : base(loggerFactory, authorizationApiClient, securityContext)
    {
        _collectivesCosmosDatabase = collectivesCosmosDatabase;
        _songsOperations = songsOperations;
        _logger = loggerFactory.CreateLogger<Delete>();
    }

    [Function(CollectivesApiUrls.V2Delete)]
    public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequest req, [FromBody] DeleteRequest request) => await RunHandler(req, request);

    protected override async Task<DeleteResponse> Execute(HttpRequest httpRequest, DeleteRequest request)
    {
        var collective = await _collectivesCosmosDatabase.GetCollective(request.CollectiveId);
        if (request.Identity.Id != collective?.CreatedByUserId)
            throw new("Only the author may make changes to their collectives.");

        if (collective.Versions.Last().Visibility == Database.Models.CollectiveVisibility.Deleted)
            throw new("The collective is already deleted.");

        collective.Versions.Add(collective.Versions.Last() with
        {
            CreatedOn = DateTime.Now,
            Visibility = Database.Models.CollectiveVisibility.Deleted,
        });
        collective.LatestVisibility = Database.Models.CollectiveVisibility.Deleted;
        
        var version = await _songsOperations.SongsDatabase.Versions
            .Where(x => x.Song.TenantId == SecurityContext.TenantId)
            .Include(x => x.Song)
            .ThenInclude(x => x.Versions)
            .Include(x => x.Likes)
            .Include(x => x.Comments)
            .SingleOrDefaultAsync(x => x.CollectiveId == request.CollectiveId);

        await _collectivesCosmosDatabase.UpsertCollective(collective);

        if (version == null)
        {
            _logger.LogWarning("A version for the collective {id} is not found.", request.CollectiveId);
            return new();
        }

        if (version.Song.Versions.Count == 1)
        {
            version.Song.Status = SongStatus.Archived;
            await _songsOperations.SongsDatabase.SaveChangesAsyncX();
        }

        _songsOperations.SongsDatabase.Likes.RemoveRange(version.Likes);
        _songsOperations.SongsDatabase.Comments.RemoveRange(version.Comments);
        await _songsOperations.SongsDatabase.SaveChangesAsyncX();

        _songsOperations.SongsDatabase.Versions.Remove(version);
        await _songsOperations.SongsDatabase.SaveChangesAsyncX();

        return new();
    }
}