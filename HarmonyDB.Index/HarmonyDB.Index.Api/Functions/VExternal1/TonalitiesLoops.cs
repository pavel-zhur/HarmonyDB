using System.Net;
using HarmonyDB.Index.Analysis.Em.Models;
using HarmonyDB.Index.Analysis.Em.Services;
using HarmonyDB.Index.Analysis.Tools;
using HarmonyDB.Index.Api.Client;
using HarmonyDB.Index.Api.Model;
using HarmonyDB.Index.Api.Model.VExternal1;
using HarmonyDB.Index.Api.Model.VExternal1.Tonalities;
using HarmonyDB.Index.Api.Models;
using HarmonyDB.Index.Api.Tools;
using HarmonyDB.Index.BusinessLogic.Caches;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OneShelf.Common.Api;
using OneShelf.Common.Api.WithAuthorization;
using Loop = HarmonyDB.Index.Api.Model.VExternal1.Tonalities.Loop;

namespace HarmonyDB.Index.Api.Functions.VExternal1;

public class TonalitiesLoops : ServiceFunctionBase<LoopsRequest, LoopsResponse>
{
    private readonly IndexApiOptions _options;
    private readonly IndexApiClient _indexApiClient;
    private readonly StructuresCache _structuresCache;
    private readonly TonalitiesCache _tonalitiesCache;

    public TonalitiesLoops(ILoggerFactory loggerFactory, SecurityContext securityContext, ConcurrencyLimiter concurrencyLimiter, IOptions<IndexApiOptions> options, IndexApiClient indexApiClient, StructuresCache structuresCache, TonalitiesCache tonalitiesCache)
        : base(loggerFactory, securityContext, options.Value.LimitConcurrency ? concurrencyLimiter : null)
    {
        _indexApiClient = indexApiClient;
        _structuresCache = structuresCache;
        _tonalitiesCache = tonalitiesCache;
        _options = options.Value;
    }

    [ProducesResponseType<LoopsResponse>(200)]
    [Function(IndexApiUrls.VExternal1TonalitiesLoops)]
    public Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequest req, [FromBody] LoopsRequest request)
        => RunHandler(request);

    protected override async Task<LoopsResponse> Execute(LoopsRequest request)
    {
        if (_options.RedirectCachesToIndex)
        {
            return await _indexApiClient.TonalitiesLoops(request);
        }

        var tonalities = await _tonalitiesCache.Get();
        var structures = await _structuresCache.Get();

        var results = tonalities.Loops
            .Join(structures.Loops, x => x.Key, x => x.Key, (x, y) => (tone: x.Value, stats: y.Value))
            .Select(x => new Loop(
                x.stats.Normalized,
                x.stats.Length,
                x.stats.TotalOccurrences,
                x.stats.TotalSuccessions,
                x.stats.AverageCoverage,
                x.stats.TotalSongs,
                x.tone.TonalityProbabilities.ToLinear(),
                x.tone.Score.ScaleScore,
                x.tone.Score.TonicScore))
            .ToList();

        var loops = results
            .Where(x => x.Length >= request.MinLength)
            .Where(x => x.Length <= (request.MaxLength ?? int.MaxValue))
            .Where(x => x.TotalSongs >= request.MinTotalSongs)
            .Where(x => x.TotalOccurrences >= request.MinTotalOccurrences)
            .Where(x => x.TotalSuccessions >= request.MinTotalSuccessions)
            .Where(x => x.Probabilities.TonalityConfidence() >= request.MinTonalityConfidence)
            .Where(x => x.Probabilities.TonalityConfidence() <= request.MaxTonalityConfidence)
            .Where(x => x.Probabilities.TonicConfidence(false) >= request.MinTonicConfidence)
            .Where(x => x.Probabilities.TonicConfidence(false) <= request.MaxTonicConfidence)
            .Where(x => x.TonicScore >= request.MinTonicScore)
            .Where(x => x.ScaleScore >= request.MinScaleScore)
            .Where(x => request.DetectedScaleFilter switch
            {
                RequestScaleFilter.Any => true,
                RequestScaleFilter.Major => !x.Probabilities.GetPredictedTonality().isMinor,
                RequestScaleFilter.Minor => x.Probabilities.GetPredictedTonality().isMinor,
                _ => throw new ArgumentOutOfRangeException(),
            })
            .Where(x => request.SecondFilter switch
            {
                RequestSecondFilter.Any => true,
                RequestSecondFilter.Relative => x.Probabilities.GetSecondPredictedTonality().GetMajorTonic(false) == x.Probabilities.GetPredictedTonality().GetMajorTonic(false),
                RequestSecondFilter.NotRelative => x.Probabilities.GetSecondPredictedTonality().GetMajorTonic(false) != x.Probabilities.GetPredictedTonality().GetMajorTonic(false),
                _ => throw new ArgumentOutOfRangeException(),
            })
            .ToList();

        var orderedLoops = (request.Ordering switch
        {
            LoopsRequestOrdering.LengthAscSongsDesc => loops.OrderBy(x => x.Length).ThenByDescending(x => x.TotalSongs),
            LoopsRequestOrdering.LengthDescSongsDesc => loops.OrderByDescending(x => x.Length).ThenByDescending(x => x.TotalSongs),
            LoopsRequestOrdering.LengthAscSuccessionsDesc => loops.OrderBy(x => x.Length).ThenByDescending(x => x.TotalSuccessions),
            LoopsRequestOrdering.LengthDescSuccessionsDesc => loops.OrderByDescending(x => x.Length).ThenByDescending(x => x.TotalSuccessions),
            LoopsRequestOrdering.LengthAscOccurrencesDesc => loops.OrderBy(x => x.Length).ThenByDescending(x => x.TotalOccurrences),
            LoopsRequestOrdering.LengthDescOccurrencesDesc => loops.OrderByDescending(x => x.Length).ThenByDescending(x => x.TotalOccurrences),
            LoopsRequestOrdering.LengthAscCoverageDesc => loops.OrderBy(x => x.Length).ThenByDescending(x => x.AverageCoverage),
            LoopsRequestOrdering.LengthDescCoverageDesc => loops.OrderByDescending(x => x.Length).ThenByDescending(x => x.AverageCoverage),
            LoopsRequestOrdering.SongsDesc => loops.OrderByDescending(x => x.TotalSongs),
            LoopsRequestOrdering.SuccessionsDesc => loops.OrderByDescending(x => x.TotalSuccessions),
            LoopsRequestOrdering.OccurrencesDesc => loops.OrderByDescending(x => x.TotalOccurrences),
            LoopsRequestOrdering.CoverageDesc => loops.OrderByDescending(x => x.AverageCoverage),
            LoopsRequestOrdering.LengthAscScaleScoreDesc => loops.OrderBy(x => x.Length).ThenByDescending(x => x.ScaleScore),
            LoopsRequestOrdering.LengthDescScaleScoreDesc => loops.OrderByDescending(x => x.Length).ThenByDescending(x => x.ScaleScore),
            LoopsRequestOrdering.LengthAscTonicScoreDesc => loops.OrderBy(x => x.Length).ThenByDescending(x => x.TonicScore),
            LoopsRequestOrdering.LengthDescTonicScoreDesc => loops.OrderByDescending(x => x.Length).ThenByDescending(x => x.TonicScore),
            LoopsRequestOrdering.ScaleScoreDesc => loops.OrderByDescending(x => x.ScaleScore),
            LoopsRequestOrdering.ScaleScoreAsc => loops.OrderBy(x => x.ScaleScore),
            LoopsRequestOrdering.TonicScoreDesc => loops.OrderByDescending(x => x.TonicScore),
            LoopsRequestOrdering.TonicScoreAsc => loops.OrderBy(x => x.TonicScore),
            LoopsRequestOrdering.LengthAscTonalityConfidenceDesc => loops.OrderBy(x => x.Length).ThenByDescending(x => x.Probabilities.TonalityConfidence()),
            LoopsRequestOrdering.LengthDescTonalityConfidenceDesc => loops.OrderByDescending(x => x.Length).ThenByDescending(x => x.Probabilities.TonalityConfidence()),
            LoopsRequestOrdering.LengthAscTonicConfidenceDesc => loops.OrderBy(x => x.Length).ThenByDescending(x => x.Probabilities.TonicConfidence(false)),
            LoopsRequestOrdering.LengthDescTonicConfidenceDesc => loops.OrderByDescending(x => x.Length).ThenByDescending(x => x.Probabilities.TonicConfidence(false)),
            LoopsRequestOrdering.TonalityConfidenceDesc => loops.OrderByDescending(x => x.Probabilities.TonalityConfidence()),
            LoopsRequestOrdering.TonalityConfidenceAsc => loops.OrderBy(x => x.Probabilities.TonalityConfidence()),
            LoopsRequestOrdering.TonicConfidenceDesc => loops.OrderByDescending(x => x.Probabilities.TonicConfidence(false)),
            LoopsRequestOrdering.TonicConfidenceAsc => loops.OrderBy(x => x.Probabilities.TonicConfidence(false)),
            _ => throw new ArgumentOutOfRangeException()
        })
        .ToList();

        return new()
        {
            Total = loops.Count,
            TotalPages = loops.Count / request.LoopsPerPage + (loops.Count % request.LoopsPerPage == 0 ? 0 : 1),
            CurrentPageNumber = request.PageNumber,
            Loops = orderedLoops.Skip((request.PageNumber - 1) * request.LoopsPerPage).Take(request.LoopsPerPage).ToList(),
            Distributions = new()
            {
                TotalSongs = loops.GetPercentiles(x => x.TotalSongs),
                TotalOccurrences = loops.GetPercentiles(x => x.TotalOccurrences),
                TotalSuccessions = loops.GetPercentiles(x => x.TotalSuccessions),
                AverageCoverage = loops.GetPercentiles(x => x.AverageCoverage),
                TonalityConfidence = loops.GetPercentiles(x => x.Probabilities.TonalityConfidence()),
                TonicConfidence = loops.GetPercentiles(x => x.Probabilities.TonicConfidence(false)),
                TonicScore = loops.GetPercentiles(x => x.TonicScore),
                ScaleScore = loops.GetPercentiles(x => x.ScaleScore),
            },
            WeightedDistributionsByOccurrences = new()
            {
                TotalSongs = loops.GetWeightedPercentiles(x => (x.TotalSongs, x.TotalOccurrences)),
                TotalOccurrences = loops.GetWeightedPercentiles(x => (x.TotalOccurrences, x.TotalOccurrences)),
                TotalSuccessions = loops.GetWeightedPercentiles(x => (x.TotalSuccessions, x.TotalOccurrences)),
                AverageCoverage = loops.GetWeightedPercentiles(x => (x.AverageCoverage, x.TotalOccurrences)),
                TonalityConfidence = loops.GetWeightedPercentiles(x => (x.Probabilities.TonalityConfidence(), x.TotalOccurrences)),
                TonicConfidence = loops.GetWeightedPercentiles(x => (x.Probabilities.TonicConfidence(false), x.TotalOccurrences)),
                TonicScore = loops.GetWeightedPercentiles(x => (x.TonicScore, x.TotalOccurrences)),
                ScaleScore = loops.GetWeightedPercentiles(x => (x.ScaleScore, x.TotalOccurrences)),
            },
        };
    }
}
