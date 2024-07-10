using HarmonyDB.Index.Analysis.Em.Models;
using HarmonyDB.Index.Analysis.Tools;
using HarmonyDB.Index.Api.Client;
using HarmonyDB.Index.Api.Model;
using HarmonyDB.Index.Api.Model.VExternal1;
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

public class StructureLoop : ServiceFunctionBase<StructureLoopRequest, StructureLoopResponse>
{
    private readonly IndexApiOptions _options;
    private readonly IndexApiClient _indexApiClient;
    private readonly StructuresCache _structuresCache;
    private readonly TonalitiesCache _tonalitiesCache;
    private readonly IndexHeadersCache _indexHeadersCache;
    private readonly CommonExecutions _commonExecutions;

    public StructureLoop(ILoggerFactory loggerFactory, SecurityContext securityContext, ConcurrencyLimiter concurrencyLimiter, IOptions<IndexApiOptions> options, IndexApiClient indexApiClient, StructuresCache structuresCache, TonalitiesCache tonalitiesCache, IndexHeadersCache indexHeadersCache, DownstreamApiClient downstreamApiClient, CommonExecutions commonExecutions)
        : base(loggerFactory, securityContext, concurrencyLimiter, options.Value.RedirectCachesToIndex)
    {
        _indexApiClient = indexApiClient;
        _structuresCache = structuresCache;
        _tonalitiesCache = tonalitiesCache;
        _indexHeadersCache = indexHeadersCache;
        _commonExecutions = commonExecutions;
        _options = options.Value;
    }

    [Function(IndexApiUrls.VExternal1StructureLoop)]
    public Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequest req, [FromBody] StructureLoopRequest request)
        => RunHandler(request);

    protected override async Task<StructureLoopResponse> Execute(StructureLoopRequest request)
    {
        if (_options.RedirectCachesToIndex)
        {
            return await _indexApiClient.StructureLoop(request);
        }

        var tonalities = await _tonalitiesCache.Get();
        var structures = await _structuresCache.Get();
        var headers = await _indexHeadersCache.Get();

        var stats = structures.Loops[request.Normalized];
        var tone = tonalities.Loops[request.Normalized];

        return new()
        {
            Loop = new(
                stats.Normalized,
                stats.Length,
                stats.TotalOccurrences,
                stats.TotalSuccessions,
                stats.TotalSongs,
                tone.TonalityProbabilities.ToLinear(),
                tone.Score.ScaleScore,
                tone.Score.TonicScore),

            LinkStatistics = structures.LinksByLoopId[request.Normalized]
                .Where(x => tonalities.Songs.ContainsKey(x.ExternalId))
                .Where(x => headers.Headers.ContainsKey(x.ExternalId))
                .Select(link => (
                    link,
                    tone: tonalities.Songs[link.ExternalId],
                    stats: structures.Songs[link.ExternalId],
                    header: headers.Headers[link.ExternalId]))
                .GroupBy(x => (
                    known: x.tone.KnownTonality?.SelectSingle(x => (x.Tonic, x.Scale == Scale.Minor).ToIndex()),
                    predicted: x.tone.TonalityProbabilities.ToLinear().GetPredictedTonality().ToIndex(),
                    x.link.NormalizationRoot))
                .Select(g => new StructureLinkStatistics(
                    g.Key.known,
                    g.Key.predicted,
                    g.Key.NormalizationRoot,
                    g.Count(),
                    g.Sum(l => l.link.GetWeight(structures.Loops[l.link.Normalized], g.Key.known.HasValue)),
                    g.Sum(x => x.link.Occurrences),
                    g.Sum(x => x.link.Successions),
                    g
                        .OrderByDescending(x => x.header.Rating)
                        .ThenBy(_ => Random.Shared.NextDouble())
                        .Select(x => (x, outputHeader: _commonExecutions.PrepareForOutput(x.header)))
                        .Where(x => x.outputHeader != null)
                        .Take(10)
                        .Select(x => new StructureSongTonality(
                            x.outputHeader!.ExternalId,
                            x.x.stats.TotalLoops,
                            x.x.tone.TonalityProbabilities.ToLinear(),
                            x.x.tone.Score.TonicScore,
                            x.x.tone.Score.ScaleScore,
                            x.outputHeader,
                            x.x.tone.KnownTonality?.SelectSingle(x => (x.Tonic, x.Scale == Scale.Minor).ToIndex())))
                        .ToList()))
                .ToList(),
        };
    }
}