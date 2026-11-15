using Azure.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using OneShelf.Authorization.Api.Client;
using OneShelf.Authorization.Api.Model;
using OneShelf.Collectives.Api.Model;
using OneShelf.Collectives.Api.Model.VInternal;
using OneShelf.Collectives.Api.Services;
using OneShelf.Collectives.Api.Tools;
using OneShelf.Collectives.Database;
using OneShelf.Collectives.Database.Models;
using OneShelf.Common.Api;
using OneShelf.Common.Database.Songs.Services;

namespace OneShelf.Collectives.Api.Functions.VInternal;

public class ListAll : FunctionBase<ListAllRequest, ListAllResponse>
{
    private readonly CollectivesDatabase _collectivesDatabase;
    private readonly SongsOperations _songsOperations;
    private readonly UrlsManager _urlsManager;

    public ListAll(ILoggerFactory loggerFactory,
        CollectivesDatabase collectivesDatabase, SongsOperations songsOperations, UrlsManager urlsManager)
        : base(loggerFactory)
    {
        _collectivesDatabase = collectivesDatabase;
        _songsOperations = songsOperations;
        _urlsManager = urlsManager;
    }

    [Function(CollectivesApiUrls.ListAll)]
    public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequest req, [FromBody] ListAllRequest request) => await RunHandler(request);

    protected override async Task<ListAllResponse> Execute(ListAllRequest request)
    {
        var collectives = await _collectivesDatabase.GetCollectives();

        return new()
        {
            Versions = collectives
                .Where(x => x.LatestVisibility != CollectiveVisibility.Deleted)
                .Select(x => x.ToModelVersion(_urlsManager.Generate(x)))
                .ToList(),
        };
    }
}