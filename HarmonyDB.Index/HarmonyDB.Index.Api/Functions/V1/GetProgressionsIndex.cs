using HarmonyDB.Index.DownstreamApi.Client;
using HarmonyDB.Source.Api.Model;
using HarmonyDB.Source.Api.Model.VInternal;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using OneShelf.Common.Api;

namespace HarmonyDB.Index.Api.Functions.V1;

public class GetProgressionsIndex : FunctionBase<GetProgressionsIndexRequest, GetProgressionsIndexResponse>
{
    private readonly DownstreamApiClient _downstreamApiClient;

    public GetProgressionsIndex(ILoggerFactory loggerFactory, DownstreamApiClient downstreamApiClient)
        : base(loggerFactory)
    {
        _downstreamApiClient = downstreamApiClient;
    }

    [Function(SourceApiUrls.VInternalGetProgressionsIndex)]
    public Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequest req, [FromBody] GetProgressionsIndexRequest request)
        => RunHandler(request);

    protected override async Task<GetProgressionsIndexResponse> Execute(GetProgressionsIndexRequest request)
    {
        var results = (await Task.WhenAll(_downstreamApiClient.GetDownstreamSourceIndices(x => x.AreProgressionsProvidedForIndexing)
            .Select(_downstreamApiClient.VInternalGetProgressionsIndex)))
            .SelectMany(x => x.Progressions)
            .ToDictionary(x => x.Key, x => x.Value);

        return new()
        {
            Progressions = results,
        };
    }
}