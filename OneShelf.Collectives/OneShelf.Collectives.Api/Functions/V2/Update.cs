using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OneShelf.Authorization.Api.Client;
using OneShelf.Collectives.Api.Model;
using OneShelf.Collectives.Api.Model.V2;
using OneShelf.Collectives.Api.Model.V2.Sub;
using OneShelf.Collectives.Api.Services;
using OneShelf.Collectives.Api.Tools;
using OneShelf.Collectives.Database;
using OneShelf.Common.Api.WithAuthorization;
using OneShelf.Common.Database.Songs.Model.Enums;
using OneShelf.Common.Database.Songs.Services;

namespace OneShelf.Collectives.Api.Functions.V2;

public class Update : AuthorizationFunctionBase<UpdateRequest, UpdateResponse>
{
    private readonly CollectivesCosmosDatabase _collectivesCosmosDatabase;
    private readonly SongsOperations _songsOperations;
    private readonly UrlsManager _urlsManager;

    public Update(ILoggerFactory loggerFactory, AuthorizationApiClient authorizationApiClient, CollectivesCosmosDatabase collectivesCosmosDatabase, SongsOperations songsOperations, UrlsManager urlsManager) 
        : base(loggerFactory, authorizationApiClient)
    {
        _collectivesCosmosDatabase = collectivesCosmosDatabase;
        _songsOperations = songsOperations;
        _urlsManager = urlsManager;
    }

    [Function(CollectivesApiUrls.V2Update)]
    public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequest req, [FromBody] UpdateRequest request) => await RunHandler(req, request);

    protected override async Task<UpdateResponse> Execute(HttpRequest httpRequest, UpdateRequest request)
    {
        var collective = await _collectivesCosmosDatabase.GetCollective(request.CollectiveId);
        if (collective == null)
        {
            throw new($"The collective {request.CollectiveId} is not found.");
        }

        if (collective.CreatedByUserId != request.Identity.Id)
        {
            throw new("The collective may only be modified by their author.");
        }

        if (collective.Versions.Last().Visibility == Database.Models.CollectiveVisibility.Deleted)
        {
            throw new($"The collective {request.CollectiveId} is deleted.");
        }

        var version = await _songsOperations.SongsDatabase.Versions
            .Where(x => x.Song.TenantId == TenantId)
            .Include(x => x.Song)
            .ThenInclude(x => x.Versions)
            .Include(x => x.Song)
            .ThenInclude(x => x.Artists)
            .SingleOrDefaultAsync(x => x.CollectiveId == request.CollectiveId);
        if (version == null)
        {
            throw new($"The version for the collective {request.CollectiveId} is not found.");
        }

        var databaseVersion = request.Collective.ToDatabaseVersion(Random.Shared.Next(100000, 999999));
        collective.Versions.Add(databaseVersion);
        collective.LatestVisibility = databaseVersion.Visibility;
        
        if (version.Song.Versions.Count == 1)
        {
            var artists = await _songsOperations.FindOrCreateArtists(TenantId, request.Collective.Authors);
            version.Song.Artists = artists;
            version.Song.Title = request.Collective.Title.Trim().ToLowerInvariant();
        }

        version.Uri = new(_urlsManager.Generate(request.CollectiveId, databaseVersion.SearchTag, collective.Versions.Count));
        version.CollectiveSearchTag = databaseVersion.SearchTag;
        version.CollectiveType = request.Collective.Visibility switch
        {
            CollectiveVisibility.Private => VersionCollectiveType.Private,
            CollectiveVisibility.Club => VersionCollectiveType.Public,
            CollectiveVisibility.Public => VersionCollectiveType.Public,
            _ => throw new ArgumentOutOfRangeException()
        };

        if (version.Song.Versions.All(x => x.CollectiveType.HasValue))
        {
            version.Song.Status = version.Song.Versions.Append(version).Any(x => x.CollectiveType == VersionCollectiveType.Public)
                ? SongStatus.Live
                : SongStatus.Draft;
        }

        await _collectivesCosmosDatabase.UpsertCollective(collective);
        await _songsOperations.SongsDatabase.SaveChangesAsyncX();

        return new()
        {
            Version = collective.ToModelVersion(_urlsManager.Generate(collective)),
        };
    }
}