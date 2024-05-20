using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using OneShelf.Authorization.Api.Client;
using OneShelf.Collectives.Api.Model;
using OneShelf.Collectives.Api.Model.V2;
using OneShelf.Collectives.Api.Services;
using OneShelf.Collectives.Api.Tools;
using OneShelf.Collectives.Database;
using OneShelf.Collectives.Database.Models;
using OneShelf.Common.Api.WithAuthorization;
using OneShelf.Common.Database.Songs.Services;

namespace OneShelf.Collectives.Api.Functions.V2;

public class Get : AuthorizationFunctionBase<GetRequest, GetResponse>
{
    private readonly CollectivesCosmosDatabase _collectivesCosmosDatabase;
    private readonly SongsOperations _songsOperations;
    private readonly UrlsManager _urlsManager;

    public Get(ILoggerFactory loggerFactory, AuthorizationApiClient authorizationApiClient, CollectivesCosmosDatabase collectivesCosmosDatabase, SongsOperations songsOperations, UrlsManager urlsManager) 
        : base(loggerFactory, authorizationApiClient)
    {
        _collectivesCosmosDatabase = collectivesCosmosDatabase;
        _songsOperations = songsOperations;
        _urlsManager = urlsManager;
    }

    [Function(CollectivesApiUrls.V2Get)]
    public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequest req, [FromBody] GetRequest request) => await RunHandler(req, request);

    protected override async Task<GetResponse> Execute(HttpRequest httpRequest, GetRequest request)
    {
        var collective = await _collectivesCosmosDatabase.GetCollective(request.CollectiveId);

        if (collective?.LatestVisibility is CollectiveVisibility.Deleted or null)
            throw new($"Not found: {request.CollectiveId}, {request.Identity.Id}.");

        return new()
        {
            Version = collective.ToModelVersion(_urlsManager.Generate(collective)),
        };
    }
}