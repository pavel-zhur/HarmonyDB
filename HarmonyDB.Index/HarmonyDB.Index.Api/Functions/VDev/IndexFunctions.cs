using HarmonyDB.Index.Analysis.Models.Interfaces;
using HarmonyDB.Index.Analysis.Services;
using HarmonyDB.Index.Api.Services;
using HarmonyDB.Index.BusinessLogic.Caches;
using HarmonyDB.Index.BusinessLogic.Models;
using HarmonyDB.Index.DownstreamApi.Client;
using HarmonyDB.Source.Api.Model.V1;
using HarmonyDB.Source.Api.Model.VInternal;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using OneShelf.Common;
using OneShelf.Common.Api;
using OneShelf.Common.Api.WithAuthorization;

namespace HarmonyDB.Index.Api.Functions.VDev;

public class IndexFunctions
{
    private readonly ILogger<IndexFunctions> _logger;
    private readonly DownstreamApiClient _downstreamApiClient;
    private readonly ProgressionsCache _progressionsCache;
    private readonly TonalitiesCache _tonalitiesCache;
    private readonly IndexHeadersCache _indexHeadersCache;
    private readonly InputParser _inputParser;
    private readonly ProgressionsSearch _progressionsSearch;
    private readonly FullTextSearchCache _fullTextSearchCache;
    private readonly StructuresCache _structuresCache;

    public IndexFunctions(ILogger<IndexFunctions> logger, DownstreamApiClient downstreamApiClient, ProgressionsCache progressionsCache, SecurityContext securityContext, IndexHeadersCache indexHeadersCache, InputParser inputParser, ProgressionsSearch progressionsSearch, FullTextSearchCache fullTextSearchCache, TonalitiesCache tonalitiesCache, StructuresCache structuresCache)
    {
        _logger = logger;
        _downstreamApiClient = downstreamApiClient;
        _progressionsCache = progressionsCache;
        _indexHeadersCache = indexHeadersCache;
        _inputParser = inputParser;
        _progressionsSearch = progressionsSearch;
        _fullTextSearchCache = fullTextSearchCache;
        _tonalitiesCache = tonalitiesCache;
        _structuresCache = structuresCache;

        securityContext.InitService();
    }

    [Function(nameof(VDevSaveProgressionsIndex), AuthorizationLevel.Function, "get")]
    public async Task<IActionResult> VDevSaveProgressionsIndex(HttpRequest req, CancellationToken cancellationToken)
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

    [Function(nameof(VDevSaveIndexHeaders), AuthorizationLevel.Function, "get")]
    public async Task<IActionResult> VDevSaveIndexHeaders(HttpRequest req, CancellationToken cancellationToken)
    {
        await _indexHeadersCache.Save(IndexHeaders.Serialize((await Task.WhenAll(_downstreamApiClient.GetDownstreamSourceIndices(x => x.AreSongsProvidedForIndexResults)
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
                .Values));

        return new OkResult();
    }

    [Function(nameof(VDevGetProgressionsCacheItemsCount), AuthorizationLevel.Function, "get")]
    public async Task<IActionResult> VDevGetProgressionsCacheItemsCount(HttpRequest req)
    {
        return new OkObjectResult((await _progressionsCache.Get()).Count);
    }

    [Function(nameof(VDevProgressionsCacheCopy), AuthorizationLevel.Function, "get")]
    public async Task<IActionResult> VDevProgressionsCacheCopy(HttpRequest req)
    {
        await _progressionsCache.Copy();
        return new OkResult();
    }

    [Function(nameof(VDevStructuresCacheCopy), AuthorizationLevel.Function, "get")]
    public async Task<IActionResult> VDevStructuresCacheCopy(HttpRequest req)
    {
        await _structuresCache.Copy();
        return new OkResult();
    }

    [Function(nameof(VDevTonalitiesCacheCopy), AuthorizationLevel.Function, "get")]
    public async Task<IActionResult> VDevTonalitiesCacheCopy(HttpRequest req)
    {
        await _tonalitiesCache.Copy();
        return new OkResult();
    }

    [Function(nameof(VDevIndexHeadersCacheCopy), AuthorizationLevel.Function, "get")]
    public async Task<IActionResult> VDevIndexHeadersCacheCopy(HttpRequest req)
    {
        await _indexHeadersCache.Copy();
        return new OkResult();
    }

    [Function(nameof(VDevGetIndexHeadersItemsCount), AuthorizationLevel.Function, "get")]
    public async Task<IActionResult> VDevGetIndexHeadersItemsCount(HttpRequest req)
    {
        return new OkObjectResult((await _indexHeadersCache.Get()).Count);
    }

    [Function(nameof(VDevGetStructuresCacheLinksCount), AuthorizationLevel.Function, "get")]
    public async Task<IActionResult> VDevGetStructuresCacheLinksCount(HttpRequest req)
    {
        return new OkObjectResult((await _structuresCache.Get()).Links.Length);
    }

    [Function(nameof(VDevGetTonalitiesIndexCacheItemsCount), AuthorizationLevel.Function, "get")]
    public async Task<IActionResult> VDevGetTonalitiesIndexCacheItemsCount(HttpRequest req)
    {
        var tonalitiesIndex = await _tonalitiesCache.Get();
        return new OkObjectResult(new
        {
            Loops = tonalitiesIndex.Loops.Count,
            Songs = tonalitiesIndex.Songs.Count,
        });
    }

    [Function(nameof(VDevRebuildStructuresCache), AuthorizationLevel.Function, "get")]
    public async Task<IActionResult> VDevRebuildStructuresCache(HttpRequest req)
    {
        await _structuresCache.Rebuild();
        return new OkObjectResult((await _structuresCache.Get()).Links.Length);
    }

    [Function(nameof(VDevRebuildTonalitiesCache), AuthorizationLevel.Function, "get")]
    public async Task<IActionResult> VDevRebuildTonalitiesCache(HttpRequest req)
    {
        await _tonalitiesCache.Rebuild();
        var tonalitiesIndex = await _tonalitiesCache.Get();
        return new OkObjectResult(new
        {
            Loops = tonalitiesIndex.Loops.Count,
            Songs = tonalitiesIndex.Songs.Count,
        });
    }

    [Function(nameof(VDevFindAndCount), AuthorizationLevel.Function, "get")]
    public async Task<IActionResult> VDevFindAndCount(HttpRequest req, string searchQuery)
    {
        var progressions = await _progressionsCache.Get();
        return new OkObjectResult(_progressionsSearch.Search(progressions, Enumerable.Range(0, progressions.Count), _inputParser.ParseSequence(searchQuery)).Count);
    }

    [Function(nameof(VDevFindHeaderAndCount), AuthorizationLevel.Function, "get")]
    public async Task<IActionResult> VDevFindHeaderAndCount(HttpRequest req, string searchQuery)
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