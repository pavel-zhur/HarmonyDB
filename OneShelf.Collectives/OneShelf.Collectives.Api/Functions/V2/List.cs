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

public class List : AuthorizationFunctionBase<ListRequest, ListResponse>
{
    private readonly CollectivesDatabase _collectivesDatabase;
    private readonly SongsOperations _songsOperations;
    private readonly UrlsManager _urlsManager;

    public List(ILoggerFactory loggerFactory, AuthorizationApiClient authorizationApiClient, CollectivesDatabase collectivesDatabase, SongsOperations songsOperations, UrlsManager urlsManager, SecurityContext securityContext) 
        : base(loggerFactory, authorizationApiClient, securityContext)
    {
        _collectivesDatabase = collectivesDatabase;
        _songsOperations = songsOperations;
        _urlsManager = urlsManager;
    }

    [Function(CollectivesApiUrls.V2List)]
    public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequest req, [FromBody] ListRequest request) => await RunHandler(req, request);

    protected override async Task<ListResponse> Execute(HttpRequest httpRequest, ListRequest request)
    {
        var collectives = await _collectivesDatabase.GetCollectives(request.Identity.Id);

        return new()
        {
            Versions = collectives
                .Where(x => x.LatestVisibility != CollectiveVisibility.Deleted)
                .Select(x => x.ToModelVersion(_urlsManager.Generate(x)))
                .ToList(),
        };
    }
}