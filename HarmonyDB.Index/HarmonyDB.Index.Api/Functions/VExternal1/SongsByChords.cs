﻿using HarmonyDB.Index.Analysis.Services;
using HarmonyDB.Index.Analysis.Tools;
using HarmonyDB.Index.Api.Client;
using HarmonyDB.Index.Api.Model;
using HarmonyDB.Index.Api.Model.VExternal1;
using HarmonyDB.Index.Api.Model.VExternal1.Main;
using HarmonyDB.Index.Api.Models;
using HarmonyDB.Index.Api.Services;
using HarmonyDB.Index.BusinessLogic.Caches;
using HarmonyDB.Index.DownstreamApi.Client;
using HarmonyDB.Source.Api.Model.V1;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OneShelf.Common;
using OneShelf.Common.Api;
using OneShelf.Common.Api.WithAuthorization;

namespace HarmonyDB.Index.Api.Functions.VExternal1;

public class SongsByChords : ServiceFunctionBase<SongsByChordsRequest, SongsByChordsResponse>
{
    private readonly ProgressionsCache _progressionsCache;
    private readonly IndexHeadersCache _indexHeadersCache;
    private readonly ProgressionsSearch _progressionsSearch;
    private readonly InputParser _inputParser;
    private readonly IndexApiOptions _options;
    private readonly IndexApiClient _indexApiClient;
    private readonly CommonExecutions _commonExecutions;
    private readonly TonalitiesCache _tonalitiesCache;

    public SongsByChords(ILoggerFactory loggerFactory, SecurityContext securityContext, ProgressionsCache progressionsCache, IndexHeadersCache indexHeadersCache, ProgressionsSearch progressionsSearch, InputParser inputParser, ConcurrencyLimiter concurrencyLimiter, IOptions<IndexApiOptions> options, IndexApiClient indexApiClient, CommonExecutions commonExecutions, TonalitiesCache tonalitiesCache)
        : base(loggerFactory, securityContext, options.Value.LimitConcurrency ? concurrencyLimiter : null)
    {
        _progressionsCache = progressionsCache;
        _indexHeadersCache = indexHeadersCache;
        _progressionsSearch = progressionsSearch;
        _inputParser = inputParser;
        _indexApiClient = indexApiClient;
        _commonExecutions = commonExecutions;
        _tonalitiesCache = tonalitiesCache;
        _options = options.Value;
    }

    [Function(IndexApiUrls.VExternal1SongsByChords)]
    public Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequest req, [FromBody] SongsByChordsRequest request)
        => RunHandler(request);

    protected override async Task<SongsByChordsResponse> Execute(SongsByChordsRequest request)
    {
        if (_options.RedirectCachesToIndex)
        {
            return await _indexApiClient.SongsByChords(request);
        }

        var progressions = await _progressionsCache.Get();
        var headers = (await _indexHeadersCache.Get())
            .Headers
            .Values
            .Select(_commonExecutions.PrepareForOutput)
            .Where(x => x != null)
            .Select(x => x!)
            .ToList();

        var found = _progressionsSearch.Search(
            headers.Join(progressions, h => h.ExternalId, p => p.Key, (h, p) => (h, p))
                .Select(x => x.p.Value),
            _inputParser.ParseSequence(request.Query));

        var results = headers
            .Select(h => progressions.TryGetValue(h.ExternalId, out var progression) && found.TryGetValue(progression, out var coverage)
                ? (h, coverage).OnceAsNullable()
                : null)
            .Where(x => x.HasValue)
            .Select(x => x.Value)
            .Where(x => x.coverage >= request.MinCoverage && x.h.Rating >= request.MinRating)
            .OrderByDescending<(IndexHeader h, float coverage), int>(request.Ordering switch
            {
                SongsByChordsRequestOrdering.ByRating => x =>
                    (int)(x.h.Rating * 10 ?? 0) * 10 + (int)(x.coverage * 1000),
                SongsByChordsRequestOrdering.ByCoverage => x =>
                    (int)(x.h.Rating * 10 ?? 0) + (int)(x.coverage * 1000) * 10,
                _ => throw new ArgumentOutOfRangeException(),
            })
            .ToList();

        var tonalitiesCache = await _tonalitiesCache.Get();

        return new()
        {
            Songs = results.Skip((request.PageNumber - 1) * request.ItemsPerPage).Take(request.ItemsPerPage).Select(x => new SongsByChordsResponseSong
            {
                Header = x.h,
                Coverage = x.coverage,
                PredictedTonalityIndex = tonalitiesCache.Songs.GetValueOrDefault(x.h.ExternalId)?.TonalityProbabilities.ToLinear().GetPredictedTonality().ToIndex(),
            }).ToList(),
            Total = results.Count,
            TotalPages = results.Count / request.ItemsPerPage + (results.Count % request.ItemsPerPage == 0 ? 0 : 1),
            CurrentPageNumber = request.PageNumber,
        };
    }
}