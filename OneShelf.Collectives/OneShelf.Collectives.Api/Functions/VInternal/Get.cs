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
using OneShelf.Common.Api.WithAuthorization;
using OneShelf.Common.Database.Songs.Services;

namespace OneShelf.Collectives.Api.Functions.VInternal;

public class Get : FunctionBase<GetRequest, GetResponse>
{
    private readonly CollectivesCosmosDatabase _collectivesCosmosDatabase;
    private readonly UrlsManager _urlsManager;

    public Get(ILoggerFactory loggerFactory,
        CollectivesCosmosDatabase collectivesCosmosDatabase, UrlsManager urlsManager)
        : base(loggerFactory)
    {
        _collectivesCosmosDatabase = collectivesCosmosDatabase;
        _urlsManager = urlsManager;
    }

    [Function(CollectivesApiUrls.Get)]
    public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequest req, [FromBody] GetRequest request)
        => await RunHandler(request);

    protected override async Task<GetResponse> Execute(GetRequest request)
    {
        var collective = await _collectivesCosmosDatabase.GetCollective(request.CollectiveId ?? _urlsManager.GetCollectiveId(request.CollectiveUri!));

        if (collective?.LatestVisibility is CollectiveVisibility.Deleted or null)
        {
            throw new($"The collective {request.CollectiveId} is not found.");
        }

        return new()
        {
            Version = collective.ToModelVersion(_urlsManager.Generate(collective)),
        };
    }
}