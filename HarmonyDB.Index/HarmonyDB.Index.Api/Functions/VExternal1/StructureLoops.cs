using HarmonyDB.Index.Analysis.Em.Models;
using HarmonyDB.Index.Analysis.Em.Services;
using HarmonyDB.Index.Analysis.Tools;
using HarmonyDB.Index.Api.Client;
using HarmonyDB.Index.Api.Model;
using HarmonyDB.Index.Api.Model.VExternal1;
using HarmonyDB.Index.Api.Models;
using HarmonyDB.Index.BusinessLogic.Caches;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OneShelf.Common.Api;
using OneShelf.Common.Api.WithAuthorization;

namespace HarmonyDB.Index.Api.Functions.VExternal1;

public class StructureLoops : ServiceFunctionBase<StructureLoopsRequest, StructureLoopsResponse>
{
    private readonly IndexApiOptions _options;
    private readonly IndexApiClient _indexApiClient;
    private readonly StructuresCache _structuresCache;
    private readonly TonalitiesCache _tonalitiesCache;

    public StructureLoops(ILoggerFactory loggerFactory, SecurityContext securityContext, ConcurrencyLimiter concurrencyLimiter, IOptions<IndexApiOptions> options, IndexApiClient indexApiClient, StructuresCache structuresCache, TonalitiesCache tonalitiesCache)
        : base(loggerFactory, securityContext, concurrencyLimiter, options.Value.RedirectCachesToIndex)
    {
        _indexApiClient = indexApiClient;
        _structuresCache = structuresCache;
        _tonalitiesCache = tonalitiesCache;
        _options = options.Value;
    }

    [Function(IndexApiUrls.VExternal1StructureLoops)]
    public Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequest req, [FromBody] StructureLoopsRequest request)
        => RunHandler(request);

    protected override async Task<StructureLoopsResponse> Execute(StructureLoopsRequest request)
    {
        if (_options.RedirectCachesToIndex)
        {
            return await _indexApiClient.StructureLoops(request);
        }

        var tonalities = await _tonalitiesCache.Get();
        var structures = await _structuresCache.Get();

        var results = tonalities.Loops
            .Join(structures.Loops, x => x.Key, x => x.Key, (x, y) => (tone: x.Value, stats: y.Value))
            .Select(x => new StructureLoopTonality(
                x.stats.Normalized,
                x.stats.Length,
                x.stats.TotalOccurrences,
                x.stats.TotalSuccessions,
                x.stats.TotalSongs,
                x.tone.TonalityProbabilities.ToLinear(),
                x.tone.Score.ScaleScore,
                x.tone.Score.TonicScore))
            .ToList();

        var loops = (request.Ordering switch
        {
            StructureLoopsRequestOrdering.LengthAscSongsDesc => results.OrderBy(x => x.Length).ThenByDescending(x => x.TotalSongs),
            StructureLoopsRequestOrdering.LengthDescSongsDesc => results.OrderByDescending(x => x.Length).ThenByDescending(x => x.TotalSongs),
            StructureLoopsRequestOrdering.LengthAscSuccessionsDesc => results.OrderBy(x => x.Length).ThenByDescending(x => x.TotalSuccessions),
            StructureLoopsRequestOrdering.LengthDescSuccessionsDesc => results.OrderByDescending(x => x.Length).ThenByDescending(x => x.TotalSuccessions),
            StructureLoopsRequestOrdering.LengthAscOccurrencesDesc => results.OrderBy(x => x.Length).ThenByDescending(x => x.TotalOccurrences),
            StructureLoopsRequestOrdering.LengthDescOccurrencesDesc => results.OrderByDescending(x => x.Length).ThenByDescending(x => x.TotalOccurrences),
            StructureLoopsRequestOrdering.SongsDesc => results.OrderByDescending(x => x.TotalSongs),
            StructureLoopsRequestOrdering.SuccessionsDesc => results.OrderByDescending(x => x.TotalSuccessions),
            StructureLoopsRequestOrdering.OccurrencesDesc => results.OrderByDescending(x => x.TotalOccurrences),
            StructureLoopsRequestOrdering.LengthAscScaleScoreDesc => results.OrderBy(x => x.Length).ThenByDescending(x => x.ScaleScore),
            StructureLoopsRequestOrdering.LengthDescScaleScoreDesc => results.OrderByDescending(x => x.Length).ThenByDescending(x => x.ScaleScore),
            StructureLoopsRequestOrdering.LengthAscTonicScoreDesc => results.OrderBy(x => x.Length).ThenByDescending(x => x.TonicScore),
            StructureLoopsRequestOrdering.LengthDescTonicScoreDesc => results.OrderByDescending(x => x.Length).ThenByDescending(x => x.TonicScore),
            StructureLoopsRequestOrdering.ScaleScoreDesc => results.OrderByDescending(x => x.ScaleScore),
            StructureLoopsRequestOrdering.ScaleScoreAsc => results.OrderBy(x => x.ScaleScore),
            StructureLoopsRequestOrdering.TonicScoreDesc => results.OrderByDescending(x => x.TonicScore),
            StructureLoopsRequestOrdering.TonicScoreAsc => results.OrderBy(x => x.TonicScore),
            StructureLoopsRequestOrdering.LengthAscTonalityConfidenceDesc => results.OrderBy(x => x.Length).ThenByDescending(x => x.Probabilities.TonalityConfidence()),
            StructureLoopsRequestOrdering.LengthDescTonalityConfidenceDesc => results.OrderByDescending(x => x.Length).ThenByDescending(x => x.Probabilities.TonalityConfidence()),
            StructureLoopsRequestOrdering.LengthAscTonicConfidenceDesc => results.OrderBy(x => x.Length).ThenByDescending(x => x.Probabilities.TonicConfidence()),
            StructureLoopsRequestOrdering.LengthDescTonicConfidenceDesc => results.OrderByDescending(x => x.Length).ThenByDescending(x => x.Probabilities.TonicConfidence()),
            StructureLoopsRequestOrdering.TonalityConfidenceDesc => results.OrderByDescending(x => x.Probabilities.TonalityConfidence()),
            StructureLoopsRequestOrdering.TonalityConfidenceAsc => results.OrderBy(x => x.Probabilities.TonalityConfidence()),
            StructureLoopsRequestOrdering.TonicConfidenceDesc => results.OrderByDescending(x => x.Probabilities.TonicConfidence()),
            StructureLoopsRequestOrdering.TonicConfidenceAsc => results.OrderBy(x => x.Probabilities.TonicConfidence()),
            _ => throw new ArgumentOutOfRangeException()
        })
        .Where(x => x.Length >= request.MinLength)
        .Where(x => x.Length <= (request.MaxLength ?? int.MaxValue))
        .Where(x => x.TotalSongs >= request.MinTotalSongs)
        .Where(x => x.TotalOccurrences >= request.MinTotalOccurrences)
        .Where(x => x.TotalSuccessions >= request.MinTotalSuccessions)
        .Where(x => x.Probabilities.TonalityConfidence() >= request.MinTonalityConfidence)
        .Where(x => x.Probabilities.TonalityConfidence() <= request.MaxTonalityConfidence)
        .Where(x => x.Probabilities.TonicConfidence() >= request.MinTonicConfidence)
        .Where(x => x.Probabilities.TonicConfidence() <= request.MaxTonicConfidence)
        .Where(x => x.TonicScore >= request.MinTonicScore)
        .Where(x => x.ScaleScore >= request.MinScaleScore)
        .Where(x => request.DetectedScaleFilter switch
        {
            StructureRequestDetectedScaleFilter.Any => true,
            StructureRequestDetectedScaleFilter.Major => !x.Probabilities.GetPredictedTonality().isMinor,
            StructureRequestDetectedScaleFilter.Minor => x.Probabilities.GetPredictedTonality().isMinor,
            _ => throw new ArgumentOutOfRangeException(),
        })
        .Where(x => request.SecondFilter switch
        {
            StructureRequestSecondFilter.Any => true,
            StructureRequestSecondFilter.Parallel => x.Probabilities.GetSecondPredictedTonality().GetMajorTonic() == x.Probabilities.GetPredictedTonality().GetMajorTonic(),
            StructureRequestSecondFilter.NotParallel => x.Probabilities.GetSecondPredictedTonality().GetMajorTonic() != x.Probabilities.GetPredictedTonality().GetMajorTonic(),
            _ => throw new ArgumentOutOfRangeException(),
        })
        .ToList();

        return new()
        {
            Total = loops.Count,
            TotalPages = loops.Count / request.LoopsPerPage + (loops.Count % request.LoopsPerPage == 0 ? 0 : 1),
            CurrentPageNumber = request.PageNumber,
            Loops = loops.Skip((request.PageNumber - 1) * request.LoopsPerPage).Take(request.LoopsPerPage).ToList(),
        };
    }
}
