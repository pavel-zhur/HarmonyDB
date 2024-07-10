using HarmonyDB.Index.Analysis.Tools;
using HarmonyDB.Index.Api.Client;
using HarmonyDB.Index.Api.Model;
using HarmonyDB.Index.Api.Model.VExternal1;
using HarmonyDB.Index.Api.Models;
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

public class StructureSongs : ServiceFunctionBase<StructureSongsRequest, StructureSongsResponse>
{
    private readonly IndexApiOptions _options;
    private readonly IndexApiClient _indexApiClient;
    private readonly StructuresCache _structuresCache;
    private readonly TonalitiesCache _tonalitiesCache;
    private readonly IndexHeadersCache _indexHeadersCache;
    private readonly DownstreamApiClient _downstreamApiClient;

    public StructureSongs(ILoggerFactory loggerFactory, SecurityContext securityContext, ConcurrencyLimiter concurrencyLimiter, IOptions<IndexApiOptions> options, IndexApiClient indexApiClient, StructuresCache structuresCache, TonalitiesCache tonalitiesCache, IndexHeadersCache indexHeadersCache, DownstreamApiClient downstreamApiClient)
        : base(loggerFactory, securityContext, concurrencyLimiter, options.Value.RedirectCachesToIndex)
    {
        _indexApiClient = indexApiClient;
        _structuresCache = structuresCache;
        _tonalitiesCache = tonalitiesCache;
        _indexHeadersCache = indexHeadersCache;
        _downstreamApiClient = downstreamApiClient;
        _options = options.Value;
    }

    [Function(IndexApiUrls.VExternal1StructureSongs)]
    public Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequest req, [FromBody] StructureSongsRequest request)
        => RunHandler(request);

    protected override async Task<StructureSongsResponse> Execute(StructureSongsRequest request)
    {
        if (_options.RedirectCachesToIndex)
        {
            return await _indexApiClient.StructureSongs(request);
        }

        var tonalities = await _tonalitiesCache.Get();
        var structures = await _structuresCache.Get();
        var headers = await _indexHeadersCache.Get();

        var results = tonalities.Songs
            .Join(structures.Songs, x => x.Key, x => x.Key, (x, y) => (tone: x.Value, stats: y.Value))
            .Join(headers.Headers
                    .Where(x => !string.IsNullOrWhiteSpace(_downstreamApiClient.GetSourceTitle(x.Value.Source))).DefaultIfEmpty(),
                x => x.stats.ExternalId, 
                x => x.Key, (x, y) => (x.tone, x.stats, header: y.Value))
            .Select(x => new StructureSongTonality(
                x.stats.ExternalId,
                x.stats.TotalLoops,
                x.tone.TonalityProbabilities.ToLinear(),
                x.tone.Score.ScaleScore,
                x.tone.Score.TonicScore,
                x.header with
                {
                    Source = _downstreamApiClient.GetSourceTitle(x.header.Source),
                }))
            .ToList();

        var songs = (request.Ordering switch
        {
            StructureSongsRequestOrdering.TotalLoopsAsc => results.OrderBy(x => x.TotalLoops),
            StructureSongsRequestOrdering.TotalLoopsDesc => results.OrderByDescending(x => x.TotalLoops),
            StructureSongsRequestOrdering.ScaleScoreDesc => results.OrderByDescending(x => x.ScaleScore),
            StructureSongsRequestOrdering.ScaleScoreAsc => results.OrderBy(x => x.ScaleScore),
            StructureSongsRequestOrdering.TonicScoreDesc => results.OrderByDescending(x => x.TonicScore),
            StructureSongsRequestOrdering.TonicScoreAsc => results.OrderBy(x => x.TonicScore),
            StructureSongsRequestOrdering.TonalityConfidenceDesc => results.OrderByDescending(x => x.Probabilities.TonalityConfidence()),
            StructureSongsRequestOrdering.TonalityConfidenceAsc => results.OrderBy(x => x.Probabilities.TonalityConfidence()),
            StructureSongsRequestOrdering.TonicConfidenceDesc => results.OrderByDescending(x => x.Probabilities.TonicConfidence()),
            StructureSongsRequestOrdering.TonicConfidenceAsc => results.OrderBy(x => x.Probabilities.TonicConfidence()),
            StructureSongsRequestOrdering.RatingAsc => results.OrderBy(x => x.IndexHeader.Rating),
            StructureSongsRequestOrdering.RatingDesc => results.OrderByDescending(x => x.IndexHeader.Rating),
            _ => throw new ArgumentOutOfRangeException()
        })
        .Where(x => x.TotalLoops >= request.MinTotalLoops)
        .Where(x => x.TotalLoops <= (request.MaxTotalLoops ?? int.MaxValue))
        .Where(x => x.Probabilities.TonalityConfidence() >= request.MinTonalityConfidence)
        .Where(x => x.Probabilities.TonalityConfidence() <= request.MaxTonalityConfidence)
        .Where(x => x.Probabilities.TonicConfidence() >= request.MinTonicConfidence)
        .Where(x => x.Probabilities.TonicConfidence() <= request.MaxTonicConfidence)
        .Where(x => x.TonicScore >= request.MinTonicScore)
        .Where(x => x.ScaleScore >= request.MinScaleScore)
        .Where(x => x.IndexHeader.Rating >= request.MinRating)
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
        .Where(x => request.CorrectDetectionFilter switch
        {
            StructureSongsRequestCorrectDetectionFilter.Any => true,
            StructureSongsRequestCorrectDetectionFilter.Exact 
                => x.IndexHeader.BestTonality != null 
                    && x.IndexHeader.BestTonality.Tonality.TryParseBestTonality() == x.Probabilities.GetPredictedTonality(),
            StructureSongsRequestCorrectDetectionFilter.ExactScaleAgnostic
                => x.IndexHeader.BestTonality != null 
                    && x.IndexHeader.BestTonality.Tonality.TryParseBestTonality()?.GetMajorTonic() == x.Probabilities.GetPredictedTonality().GetMajorTonic(),
            StructureSongsRequestCorrectDetectionFilter.No 
                => x.IndexHeader.BestTonality != null
                    && x.IndexHeader.BestTonality.Tonality.TryParseBestTonality() != x.Probabilities.GetPredictedTonality()
                    && x.IndexHeader.BestTonality.Tonality.TryParseBestTonality().HasValue,
            StructureSongsRequestCorrectDetectionFilter.IncorrectScale
                => x.IndexHeader.BestTonality != null
                    && (known: x.IndexHeader.BestTonality.Tonality.TryParseBestTonality(), predicted: x.Probabilities.GetPredictedTonality())
                        .SelectSingle(x => x.known.HasValue && x.known != x.predicted && x.known.Value.GetMajorTonic() == x.predicted.GetMajorTonic()),
            _ => throw new ArgumentOutOfRangeException(),
        })
        .Where(x => request.KnownTonalityFilter switch
        {
            StructureSongsRequestKnownTonalityFilter.Any => true,
            StructureSongsRequestKnownTonalityFilter.Some => x.IndexHeader.BestTonality != null,
            StructureSongsRequestKnownTonalityFilter.No => x.IndexHeader.BestTonality == null,
            StructureSongsRequestKnownTonalityFilter.Reliable => x.IndexHeader.BestTonality?.IsReliable == true,
            _ => throw new ArgumentOutOfRangeException(),
        })
        .ToList();

        return new()
        {
            Total = songs.Count,
            TotalPages = songs.Count / request.SongsPerPage + (songs.Count % request.SongsPerPage == 0 ? 0 : 1),
            CurrentPageNumber = request.PageNumber,
            Songs = songs.Skip((request.PageNumber - 1) * request.SongsPerPage).Take(request.SongsPerPage).ToList(),
        };
    }
}
