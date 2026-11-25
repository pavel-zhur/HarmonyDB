using HarmonyDB.Common.Representations.OneShelf;
using HarmonyDB.Index.Analysis.Models.Ids;
using HarmonyDB.Index.Analysis.Tools;
using HarmonyDB.Index.Api.Client;
using HarmonyDB.Index.Api.Model;
using HarmonyDB.Index.Api.Model.VExternal1;
using HarmonyDB.Index.Api.Model.VExternal1.Tonalities;
using HarmonyDB.Index.Api.Models;
using HarmonyDB.Index.Api.Services;
using HarmonyDB.Index.Api.Tools;
using HarmonyDB.Index.BusinessLogic.Caches;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OneShelf.Common;
using OneShelf.Common.Api;
using OneShelf.Common.Api.WithAuthorization;

namespace HarmonyDB.Index.Api.Functions.VExternal1;

public class TonalitiesLoop : ServiceFunctionBase<LoopRequest, LoopResponse>
{
    private readonly IndexApiOptions _options;
    private readonly IndexApiClient _indexApiClient;
    private readonly StructuresCache _structuresCache;
    private readonly TonalitiesCache _tonalitiesCache;
    private readonly IndexHeadersCache _indexHeadersCache;
    private readonly CommonExecutions _commonExecutions;
    private readonly IndexIds _ids;

    public TonalitiesLoop(ILoggerFactory loggerFactory, SecurityContext securityContext, ConcurrencyLimiter concurrencyLimiter, IOptions<IndexApiOptions> options, IndexApiClient indexApiClient, StructuresCache structuresCache, TonalitiesCache tonalitiesCache, IndexHeadersCache indexHeadersCache, CommonExecutions commonExecutions, IndexIds ids)
        : base(loggerFactory, securityContext, options.Value.LimitConcurrency ? concurrencyLimiter : null)
    {
        _indexApiClient = indexApiClient;
        _structuresCache = structuresCache;
        _tonalitiesCache = tonalitiesCache;
        _indexHeadersCache = indexHeadersCache;
        _commonExecutions = commonExecutions;
        _ids = ids;
        _options = options.Value;
    }

    [Function(IndexApiUrls.VExternal1TonalitiesLoop, AuthorizationLevel.Function, "post", InternalScope = IndexInternalScopes.Index)]
    public Task<IActionResult> Run(HttpRequest req, [FromBody] LoopRequest request)
        => RunHandler(request);

    protected override async Task<LoopResponse> Execute(LoopRequest request)
    {
        if (_options.RedirectCachesToIndex)
        {
            return await _indexApiClient.TonalitiesLoop(request);
        }

        var tonalities = await _tonalitiesCache.Get();
        var structures = await _structuresCache.Get();
        var headers = await _indexHeadersCache.Get();

        var loop = _ids.Loops.GetNumber(request.Normalized);
        var stats = structures.GetLoop(loop);
        var tone = tonalities.Loops.Positions.GetPosition(loop);

        return new()
        {
            Loop = new(
                stats.Normalized,
                stats.Length,
                stats.TotalOccurrences,
                stats.TotalSuccessions,
                stats.AverageCoverage,
                stats.TotalSongs,
                tonalities.Loops.GetProbabilities(tone),
                tonalities.Loops.GetScore(tone).ScaleScore,
                tonalities.Loops.GetScore(tone).TonicScore),

            LinkStatistics = structures.GetLinksByLoop(loop)
                .Where(x => tonalities.Songs.Positions.Contains(x.Song))
                .Where(x => headers.Songs.Contains(x.Song))
                .Select(link =>
                {
                    var tone = tonalities.Songs.Positions.GetPosition(link.Song);
                    var stats = structures.GetSong(link.Song);
                    var header = headers.Get(headers.Songs.GetPosition(link.Song));
                    var known = tonalities.Songs.GetKnownTonality(tone)?.FromEm().ToIndex();
                    var predicted = tonalities.Songs.GetProbabilities(tone).GetPredictedTonality().ToIndex();
                    var normalizationRoot = link.NormalizationRoot;
                    return (
                        link,
                        tone,
                        stats,
                        header,
                        known,
                        predicted,
                        normalizationRoot);
                })
                .GroupBy(x =>
                {
                    var knownOrPredicted = x.known?.FromIndex() ?? x.predicted.FromIndex();
                    return (
                        derivedTonalityIndex: (Note.Normalize(x.normalizationRoot - knownOrPredicted.root), knownOrPredicted.isMinor).ToIndex(),
                        derivedFromKnown: x.known.HasValue);
                })
                .Select(g => new LinkStatistics(
                    g.Key.derivedTonalityIndex,
                    g.Key.derivedFromKnown,
                    g.Count(),
                    g.Sum(l => TonalitiesExtensions.GetWeight(l.link.Occurrences, l.link.Successions, stats.Length, l.known.HasValue)),
                    g.Sum(x => x.link.Occurrences),
                    g.Sum(x => x.link.Successions),
                    g.Average(x => x.link.Coverage),
                    g.Average(x => tonalities.Songs.GetScore(x.tone).TonicScore),
                    g.Average(x => tonalities.Songs.GetScore(x.tone).ScaleScore),
                    g.Key.derivedFromKnown ? 1 : g.Average(x => tonalities.Songs.GetProbabilities(x.tone).TonalityConfidence()),
                    g
                        .OrderByDescending(x => x.header.Rating)
                        .ThenBy(_ => Random.Shared.NextDouble())
                        .Select(x => (x, outputHeader: _commonExecutions.PrepareForOutput(x.header)))
                        .Where(x => x.outputHeader != null)
                        .Take(10)
                        .Select(x => new LinkExample(
                            new(
                                x.outputHeader!.ExternalId,
                                x.x.stats.TotalLoops,
                                tonalities.Songs.GetProbabilities(x.x.tone),
                                tonalities.Songs.GetScore(x.x.tone).TonicScore,
                                tonalities.Songs.GetScore(x.x.tone).ScaleScore,
                                x.outputHeader,
                                tonalities.Songs.GetKnownTonality(x.x.tone)?.FromEm().ToIndex()),
                            structures.ToStructureLink(x.x.link).ToModel()))
                        .ToList()))
                .ToList(),
        };
    }
}