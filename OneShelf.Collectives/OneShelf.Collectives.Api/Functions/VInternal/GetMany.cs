using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using OneShelf.Collectives.Api.Model;
using OneShelf.Collectives.Api.Model.VInternal;
using OneShelf.Collectives.Api.Services;
using OneShelf.Collectives.Api.Tools;
using OneShelf.Collectives.Database;
using OneShelf.Collectives.Database.Models;
using OneShelf.Common.Api;

namespace OneShelf.Collectives.Api.Functions.VInternal;

/// <summary>
/// The collectives of the ids asked; those that do not exist or are deleted are left out.
/// </summary>
public class GetMany : FunctionBase<GetManyRequest, GetManyResponse>
{
    private readonly CollectivesDatabase _collectivesDatabase;
    private readonly UrlsManager _urlsManager;

    public GetMany(ILoggerFactory loggerFactory, CollectivesDatabase collectivesDatabase, UrlsManager urlsManager)
        : base(loggerFactory)
    {
        _collectivesDatabase = collectivesDatabase;
        _urlsManager = urlsManager;
    }

    [Function(CollectivesApiUrls.GetMany, AuthorizationLevel.Function, "post")]
    public async Task<IActionResult> Run(HttpRequest req, [FromBody] GetManyRequest request)
        => await RunHandler(request);

    protected override async Task<GetManyResponse> Execute(GetManyRequest request)
    {
        var collectives = await _collectivesDatabase.GetCollectives(request.CollectiveIds.Distinct().ToList());

        return new()
        {
            Versions = collectives
                .Where(x => x.LatestVisibility != CollectiveVisibility.Deleted)
                .Select(x => x.ToModelVersion(_urlsManager.Generate(x)))
                .ToList(),
        };
    }
}
