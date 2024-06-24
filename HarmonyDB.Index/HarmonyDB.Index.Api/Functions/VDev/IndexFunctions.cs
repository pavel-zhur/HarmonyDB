using HarmonyDB.Index.BusinessLogic.Models;
using HarmonyDB.Index.BusinessLogic.Services;
using HarmonyDB.Index.DownstreamApi.Client;
using HarmonyDB.Source.Api.Model.V1;
using HarmonyDB.Source.Api.Model.VInternal;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using OneShelf.Common;
using OneShelf.Common.Api.WithAuthorization;

namespace HarmonyDB.Index.Api.Functions.VDev;

public class IndexFunctions
{
    private readonly ILogger<IndexFunctions> _logger;
    private readonly DownstreamApiClient _downstreamApiClient;
    private readonly ProgressionsCache _progressionsCache;
    private readonly LoopsStatisticsCache _loopsStatisticsCache;
    private readonly SongHeadersCache _songHeadersCache;

    public IndexFunctions(ILogger<IndexFunctions> logger, DownstreamApiClient downstreamApiClient, ProgressionsCache progressionsCache, LoopsStatisticsCache loopsStatisticsCache, SecurityContext securityContext, SongHeadersCache songHeadersCache)
    {
        _logger = logger;
        _downstreamApiClient = downstreamApiClient;
        _progressionsCache = progressionsCache;
        _loopsStatisticsCache = loopsStatisticsCache;
        _songHeadersCache = songHeadersCache;

        securityContext.InitService();
    }

    [Function(nameof(VDevSaveProgressionsIndex))]
    public async Task<IActionResult> VDevSaveProgressionsIndex([HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequest req, CancellationToken cancellationToken)
    {
        await _progressionsCache.Save((await Task.WhenAll(_downstreamApiClient.GetDownstreamSourceIndices(x => x.AreProgressionsProvidedForIndexing)
                .Select(async i =>
                {
                    Dictionary<string, byte[]> progressions = new();
                    var iteration = 0;
                    GetProgressionsIndexRequest request = new();
                    while (true)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        var result = await _downstreamApiClient.VInternalGetProgressionsIndex(i, request, cancellationToken);
                        foreach (var progression in result.Progressions)
                        {
                            progressions[progression.Key] = progression.Value;
                        }

                        _logger.LogInformation("Iteration {iteration}", iteration++);

                        if (result.NextToken == null) break;

                        request.NextToken = result.NextToken;
                    }

                    return progressions;
                })))
            .SelectMany(x => x)
            .ToDictionary(x => x.Key, x => x.Value));

        return new OkResult();
    }

    [Function(nameof(VDevSaveSongHeaders))]
    public async Task<IActionResult> VDevSaveSongHeaders([HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequest req, CancellationToken cancellationToken)
    {
        await _songHeadersCache.Save(new SongHeaders
        {
            Headers = (await Task.WhenAll(_downstreamApiClient.GetDownstreamSourceIndices(x => x.AreSongsProvidedForIndexResults)
                    .Select(async i =>
                    {
                        List<IndexHeader> headers = new();
                        var iteration = 0;
                        GetSongHeadersRequest request = new();
                        while (true)
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            var result = await _downstreamApiClient.VInternalGetSongHeaders(i, request, cancellationToken);
                            headers.AddRange(result.Headers);

                            _logger.LogInformation("Iteration {iteration}", iteration++);

                            if (result.NextToken == null) break;

                            request.NextToken = result.NextToken;
                        }

                        return headers;
                    })))
                .SelectMany(x => x)
                .ToList()
        }.Serialize());

        return new OkResult();
    }

    [Function(nameof(VDevGetProgressionsCacheItemsCount))]
    public async Task<IActionResult> VDevGetProgressionsCacheItemsCount([HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequest req)
    {
        return new OkObjectResult((await _progressionsCache.Get()).Count);
    }

    [Function(nameof(VDevGetLoopStatisticsCacheItemsCount))]
    public async Task<IActionResult> VDevGetLoopStatisticsCacheItemsCount([HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequest req)
    {
        return new OkObjectResult((await _loopsStatisticsCache.Get()).Count);
    }

    [Function(nameof(VDevRebuildLoopStatisticsCache))]
    public async Task<IActionResult> VDevRebuildLoopStatisticsCache([HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequest req)
    {
        await _loopsStatisticsCache.Rebuild();
        return new OkObjectResult((await _loopsStatisticsCache.Get()).Count);
    }
}