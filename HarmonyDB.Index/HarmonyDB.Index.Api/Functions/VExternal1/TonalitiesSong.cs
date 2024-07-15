using HarmonyDB.Index.Analysis.Em.Models;
using HarmonyDB.Index.Analysis.Tools;
using HarmonyDB.Index.Api.Client;
using HarmonyDB.Index.Api.Model;
using HarmonyDB.Index.Api.Model.VExternal1;
using HarmonyDB.Index.Api.Model.VExternal1.Tonalities;
using HarmonyDB.Index.Api.Models;
using HarmonyDB.Index.Api.Services;
using HarmonyDB.Index.BusinessLogic.Caches;
using HarmonyDB.Index.DownstreamApi.Client;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OneShelf.Common;
using OneShelf.Common.Api;
using OneShelf.Common.Api.WithAuthorization;

namespace HarmonyDB.Index.Api.Functions.VExternal1;

public class TonalitiesSong : ServiceFunctionBase<SongRequest, SongResponse>
{
    private readonly IndexApiOptions _options;
    private readonly IndexApiClient _indexApiClient;
    private readonly StructuresCache _structuresCache;
    private readonly TonalitiesCache _tonalitiesCache;
    private readonly IndexHeadersCache _indexHeadersCache;
    private readonly CommonExecutions _commonExecutions;

    public TonalitiesSong(ILoggerFactory loggerFactory, SecurityContext securityContext, ConcurrencyLimiter concurrencyLimiter, IOptions<IndexApiOptions> options, IndexApiClient indexApiClient, StructuresCache structuresCache, TonalitiesCache tonalitiesCache, IndexHeadersCache indexHeadersCache, DownstreamApiClient downstreamApiClient, CommonExecutions commonExecutions)
        : base(loggerFactory, securityContext, concurrencyLimiter, options.Value.RedirectCachesToIndex)
    {
        _indexApiClient = indexApiClient;
        _structuresCache = structuresCache;
        _tonalitiesCache = tonalitiesCache;
        _indexHeadersCache = indexHeadersCache;
        _commonExecutions = commonExecutions;
        _options = options.Value;
    }

    [Function(IndexApiUrls.VExternal1TonalitiesSong)]
    public Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequest req, [FromBody] SongRequest request)
        => RunHandler(request);

    protected override async Task<SongResponse> Execute(SongRequest request)
    {
        if (_options.RedirectCachesToIndex)
        {
            return await _indexApiClient.TonalitiesSong(request);
        }

        var tonalities = await _tonalitiesCache.Get();
        var structures = await _structuresCache.Get();
        var headers = await _indexHeadersCache.Get();

        var structureSong = structures.Songs[request.ExternalId];
        var songTonality = tonalities.Songs.TryGetValue(request.ExternalId, out var value) ? value : throw new ServiceCacheItemNotFoundException();
        var header = headers.Headers[request.ExternalId];
        header = _commonExecutions.PrepareForOutput(header) ?? throw new("The song header is not available.");

        return new()
        {
            Song = new(
                songTonality.Id,
                structureSong.TotalLoops,
                songTonality.TonalityProbabilities.ToLinear(),
                songTonality.Score.TonicScore,
                songTonality.Score.ScaleScore,
                header,
                songTonality.KnownTonality?.FromEm().ToIndex()),

            Links = structures.LinksBySongId[request.ExternalId].ToList(),

            Loops = structures.LinksBySongId[request.ExternalId]
                .Select(x => x.Normalized)
                .Distinct()
                .Select(normalized =>
                {
                    var stats = structures.Loops[normalized];
                    var tone = tonalities.Loops[normalized];

                    return new Loop(
                        normalized,
                        stats.Length,
                        stats.TotalOccurrences,
                        stats.TotalSuccessions,
                        stats.AverageCoverage,
                        stats.TotalSongs,
                        tone.TonalityProbabilities.ToLinear(),
                        tone.Score.ScaleScore,
                        tone.Score.TonicScore
                    );
                })
                .ToList(),
        };
    }
}