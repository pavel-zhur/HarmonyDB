using HarmonyDB.Index.Analysis.Models.Interfaces;
using HarmonyDB.Index.Analysis.Services;
using HarmonyDB.Index.Api.Services;
using HarmonyDB.Index.BusinessLogic.Models;
using HarmonyDB.Index.BusinessLogic.Services;
using HarmonyDB.Index.BusinessLogic.Services.Caches;
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
    private readonly TonalitiesCache _tonalitiesCache;
    private readonly IndexHeadersCache _indexHeadersCache;
    private readonly InputParser _inputParser;
    private readonly ProgressionsSearch _progressionsSearch;
    private readonly FullTextSearchCache _fullTextSearchCache;

    public IndexFunctions(ILogger<IndexFunctions> logger, DownstreamApiClient downstreamApiClient, ProgressionsCache progressionsCache, LoopsStatisticsCache loopsStatisticsCache, SecurityContext securityContext, IndexHeadersCache indexHeadersCache, InputParser inputParser, ProgressionsSearch progressionsSearch, FullTextSearchCache fullTextSearchCache, TonalitiesCache tonalitiesCache)
    {
        _logger = logger;
        _downstreamApiClient = downstreamApiClient;
        _progressionsCache = progressionsCache;
        _loopsStatisticsCache = loopsStatisticsCache;
        _indexHeadersCache = indexHeadersCache;
        _inputParser = inputParser;
        _progressionsSearch = progressionsSearch;
        _fullTextSearchCache = fullTextSearchCache;
        _tonalitiesCache = tonalitiesCache;

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

    [Function(nameof(VDevSaveIndexHeaders))]
    public async Task<IActionResult> VDevSaveIndexHeaders([HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequest req, CancellationToken cancellationToken)
    {
        await _indexHeadersCache.Save(new IndexHeaders
        {
            Headers = (await Task.WhenAll(_downstreamApiClient.GetDownstreamSourceIndices(x => x.AreSongsProvidedForIndexResults)
                    .Select(async i =>
                    {
                        List<IndexHeader> headers = new();
                        var iteration = 0;
                        GetIndexHeadersRequest request = new();
                        while (true)
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            var result = await _downstreamApiClient.VInternalGetIndexHeaders(i, request, cancellationToken);
                            headers.AddRange(result.Headers);

                            _logger.LogInformation("Iteration {iteration} {token}", iteration++, request.NextToken);

                            if (result.NextToken == null) break;

                            request.NextToken = result.NextToken;
                        }

                        return headers;
                    })))
                .SelectMany(x => x)
                .ToDictionary(x => x.ExternalId)
        }.Serialize());

        return new OkResult();
    }

    [Function(nameof(VDevGetProgressionsCacheItemsCount))]
    public async Task<IActionResult> VDevGetProgressionsCacheItemsCount([HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequest req)
    {
        return new OkObjectResult((await _progressionsCache.Get()).Count);
    }

    [Function(nameof(VDevProgressionsCacheCopy))]
    public async Task<IActionResult> VDevProgressionsCacheCopy([HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequest req)
    {
        await _progressionsCache.Copy();
        return new OkResult();
    }

    [Function(nameof(VDevLoopsStatisticsCacheCopy))]
    public async Task<IActionResult> VDevLoopsStatisticsCacheCopy([HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequest req)
    {
        await _loopsStatisticsCache.Copy();
        return new OkResult();
    }

    [Function(nameof(VDevIndexHeadersCacheCopy))]
    public async Task<IActionResult> VDevIndexHeadersCacheCopy([HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequest req)
    {
        await _indexHeadersCache.Copy();
        return new OkResult();
    }

    [Function(nameof(VDevGetIndexHeadersItemsCount))]
    public async Task<IActionResult> VDevGetIndexHeadersItemsCount([HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequest req)
    {
        return new OkObjectResult((await _indexHeadersCache.Get()).Headers.Count);
    }

    [Function(nameof(VDevGetLoopStatisticsCacheItemsCount))]
    public async Task<IActionResult> VDevGetLoopStatisticsCacheItemsCount([HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequest req)
    {
        return new OkObjectResult((await _loopsStatisticsCache.Get()).Count);
    }

    [Function(nameof(VDevGetTonalitiesIndexCacheItemsCount))]
    public async Task<IActionResult> VDevGetTonalitiesIndexCacheItemsCount([HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequest req)
    {
        var tonalitiesIndex = await _tonalitiesCache.Get();
        return new OkObjectResult(new
        {
            Loops = tonalitiesIndex.Loops.Count,
            Songs = tonalitiesIndex.Songs.Count,
            Links = tonalitiesIndex.LoopLinks.Count,
        });
    }

    [Function(nameof(VDevGetLoopStatisticsCacheTop1000))]
    public async Task<IActionResult> VDevGetLoopStatisticsCacheTop1000([HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequest req)
    {
        return new OkObjectResult((await _loopsStatisticsCache.Get()).Take(1000).ToList());
    }

    [Function(nameof(VDevRebuildLoopStatisticsCache))]
    public async Task<IActionResult> VDevRebuildLoopStatisticsCache([HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequest req)
    {
        await _loopsStatisticsCache.Rebuild();
        return new OkObjectResult((await _loopsStatisticsCache.Get()).Count);
    }

    [Function(nameof(VDevFindAndCount))]
    public async Task<IActionResult> VDevFindAndCount([HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequest req, string searchQuery)
    {
        return new OkObjectResult(_progressionsSearch.Search((await _progressionsCache.Get()).Values, _inputParser.Parse(searchQuery)).Count);
    }

    [Function(nameof(VDevFindHeaderAndCount))]
    public async Task<IActionResult> VDevFindHeaderAndCount([HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequest req, string searchQuery)
    {
        var fullTextSearch = await _fullTextSearchCache.Get();
        var found = fullTextSearch.Find(searchQuery);
        return new OkObjectResult(new
        {
            found.Count,
            Top100 = found.Take(100).ToList(),
        });
    }
}