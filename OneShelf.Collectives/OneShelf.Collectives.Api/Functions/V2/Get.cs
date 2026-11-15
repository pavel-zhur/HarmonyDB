using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using OneShelf.Authorization.Api.Client;
using OneShelf.Collectives.Api.Model;
using OneShelf.Collectives.Api.Model.V2;
using OneShelf.Collectives.Api.Services;
using OneShelf.Collectives.Api.Tools;
using OneShelf.Collectives.Database;
using OneShelf.Collectives.Database.Models;
using OneShelf.Common.Api;
using OneShelf.Common.Api.WithAuthorization;
using OneShelf.Common.Database.Songs.Services;

namespace OneShelf.Collectives.Api.Functions.V2;

public class Get : AuthorizationFunctionBase<GetRequest, GetResponse>
{
    private readonly CollectivesDatabase _collectivesDatabase;
    private readonly SongsOperations _songsOperations;
    private readonly UrlsManager _urlsManager;

    public Get(ILoggerFactory loggerFactory, AuthorizationApiClient authorizationApiClient, CollectivesDatabase collectivesDatabase, SongsOperations songsOperations, UrlsManager urlsManager, SecurityContext securityContext) 
        : base(loggerFactory, authorizationApiClient, securityContext)
    {
        _collectivesDatabase = collectivesDatabase;
        _songsOperations = songsOperations;
        _urlsManager = urlsManager;
    }

    [Function(CollectivesApiUrls.V2Get, AuthorizationLevel.Anonymous, "post")]
    public async Task<IActionResult> Run(HttpRequest req, [FromBody] GetRequest request) => await RunHandler(req, request);

    protected override async Task<GetResponse> Execute(HttpRequest httpRequest, GetRequest request)
    {
        var collective = await _collectivesDatabase.GetCollective(request.CollectiveId);

        if (collective?.LatestVisibility is CollectiveVisibility.Deleted or null)
            throw new($"Not found: {request.CollectiveId}, {request.Identity.Id}.");

        return new()
        {
            Version = collective.ToModelVersion(_urlsManager.Generate(collective)),
        };
    }
}