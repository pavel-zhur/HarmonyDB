using HarmonyDB.Index.Analysis.Em.Models;
using HarmonyDB.Index.Analysis.Tools;
using HarmonyDB.Index.Api.Client;
using HarmonyDB.Index.Api.Model;
using HarmonyDB.Index.Api.Model.VExternal1;
using HarmonyDB.Index.Api.Models;
using HarmonyDB.Index.Api.Services;
using HarmonyDB.Index.Api.Tools;
using HarmonyDB.Index.BusinessLogic.Caches;
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
    private readonly CommonExecutions _commonExecutions;

    public StructureSongs(ILoggerFactory loggerFactory, SecurityContext securityContext, ConcurrencyLimiter concurrencyLimiter, IOptions<IndexApiOptions> options, IndexApiClient indexApiClient, StructuresCache structuresCache, TonalitiesCache tonalitiesCache, IndexHeadersCache indexHeadersCache, CommonExecutions commonExecutions)
        : base(loggerFactory, securityContext, concurrencyLimiter, options.Value.RedirectCachesToIndex)
    {
        _indexApiClient = indexApiClient;
        _structuresCache = structuresCache;
        _tonalitiesCache = tonalitiesCache;
        _indexHeadersCache = indexHeadersCache;
        _commonExecutions = commonExecutions;
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
            .Join(headers.Headers.Select(x => _commonExecutions.PrepareForOutput(x.Value)!).Where(x => x != null!),
                x => x.stats.ExternalId, 
                x => x.ExternalId, (x, y) => (x.tone, x.stats, header: y))
            .Select(x => new StructureSongTonality(
                x.stats.ExternalId,
                x.stats.TotalLoops,
                x.tone.TonalityProbabilities.ToLinear(),
                x.tone.Score.TonicScore,
                x.tone.Score.ScaleScore,
                x.header,
                x.header.BestTonality?.Tonality.TryParseBestTonality()?.ToIndex()))
            .ToList();

        var songs = results
            .Where(x => x.TotalLoops >= request.MinTotalLoops)
            .Where(x => x.TotalLoops <= (request.MaxTotalLoops ?? int.MaxValue))
            .Where(x => x.Probabilities.TonalityConfidence() >= request.MinTonalityConfidence)
            .Where(x => x.Probabilities.TonalityConfidence() <= request.MaxTonalityConfidence)
            .Where(x => x.Probabilities.TonicConfidence(true) >= request.MinTonicConfidence)
            .Where(x => x.Probabilities.TonicConfidence(true) <= request.MaxTonicConfidence)
            .Where(x => x.TonicScore >= request.MinTonicScore)
            .Where(x => x.ScaleScore >= request.MinScaleScore)
            .Where(x => x.IndexHeader.Rating >= request.MinRating)
            .Where(x => request.DetectedScaleFilter switch
            {
                StructureRequestScaleFilter.Any => true,
                StructureRequestScaleFilter.Major => !x.Probabilities.GetPredictedTonality().isMinor,
                StructureRequestScaleFilter.Minor => x.Probabilities.GetPredictedTonality().isMinor,
                _ => throw new ArgumentOutOfRangeException(),
            })
            .Where(x => request.KnownScaleFilter switch
            {
                StructureRequestScaleFilter.Any => true,
                StructureRequestScaleFilter.Major => x.KnownTonalityIndex?.FromIndex().isMinor == false,
                StructureRequestScaleFilter.Minor => x.KnownTonalityIndex?.FromIndex().isMinor == true,
                _ => throw new ArgumentOutOfRangeException(),
            })
            .Where(x => request.SecondFilter switch
            {
                StructureRequestSecondFilter.Any => true,
                StructureRequestSecondFilter.Parallel => x.Probabilities.GetSecondPredictedTonality().GetMajorTonic(true) == x.Probabilities.GetPredictedTonality().GetMajorTonic(true),
                StructureRequestSecondFilter.NotParallel => x.Probabilities.GetSecondPredictedTonality().GetMajorTonic(true) != x.Probabilities.GetPredictedTonality().GetMajorTonic(true),
                _ => throw new ArgumentOutOfRangeException(),
            })
            .Where(x => request.CorrectDetectionFilter switch
            {
                StructureSongsRequestCorrectDetectionFilter.Any => true,
                StructureSongsRequestCorrectDetectionFilter.Exact 
                    => x.KnownTonalityIndex?.FromIndex() == x.Probabilities.GetPredictedTonality(),
                StructureSongsRequestCorrectDetectionFilter.ExactScaleAgnostic 
                    => x.KnownTonalityIndex?.FromIndex().GetMajorTonic(true) == x.Probabilities.GetPredictedTonality().GetMajorTonic(true),
                StructureSongsRequestCorrectDetectionFilter.No 
                    => x.KnownTonalityIndex != null
                       && x.KnownTonalityIndex != x.Probabilities.GetPredictedTonality().ToIndex(),
                StructureSongsRequestCorrectDetectionFilter.IncorrectScale
                    => (known: x.KnownTonalityIndex?.FromIndex(), predicted: x.Probabilities.GetPredictedTonality())
                            .SelectSingle(x => x.known.HasValue && x.known != x.predicted && x.known.Value.GetMajorTonic(true) == x.predicted.GetMajorTonic(true)),
                StructureSongsRequestCorrectDetectionFilter.NoAndNotParallel
                    => (known: x.KnownTonalityIndex?.FromIndex(), predicted: x.Probabilities.GetPredictedTonality())
                            .SelectSingle(x => x.known.HasValue && x.known != x.predicted && x.known.Value.GetMajorTonic(true) != x.predicted.GetMajorTonic(true)),
                _ => throw new ArgumentOutOfRangeException(),
            })
            .Where(x => request.KnownTonalityFilter switch
            {
                StructureSongsRequestKnownTonalityFilter.Any => true,
                StructureSongsRequestKnownTonalityFilter.Some => x.IndexHeader.BestTonality != null,
                StructureSongsRequestKnownTonalityFilter.No => x.IndexHeader.BestTonality == null,
                StructureSongsRequestKnownTonalityFilter.Reliable => x.IndexHeader.BestTonality?.IsReliable == true,
                StructureSongsRequestKnownTonalityFilter.Unreliable => x.IndexHeader.BestTonality?.IsReliable == false,
                _ => throw new ArgumentOutOfRangeException(),
            })
            .ToList();

        var orderedSongs = (request.Ordering switch
        {
            StructureSongsRequestOrdering.TotalLoopsAsc => songs.OrderBy(x => x.TotalLoops),
            StructureSongsRequestOrdering.TotalLoopsDesc => songs.OrderByDescending(x => x.TotalLoops),
            StructureSongsRequestOrdering.ScaleScoreDesc => songs.OrderByDescending(x => x.ScaleScore),
            StructureSongsRequestOrdering.ScaleScoreAsc => songs.OrderBy(x => x.ScaleScore),
            StructureSongsRequestOrdering.TonicScoreDesc => songs.OrderByDescending(x => x.TonicScore),
            StructureSongsRequestOrdering.TonicScoreAsc => songs.OrderBy(x => x.TonicScore),
            StructureSongsRequestOrdering.TonalityConfidenceDesc => songs.OrderByDescending(x =>
                x.Probabilities.TonalityConfidence()),
            StructureSongsRequestOrdering.TonalityConfidenceAsc => songs.OrderBy(x =>
                x.Probabilities.TonalityConfidence()),
            StructureSongsRequestOrdering.TonicConfidenceDesc => songs.OrderByDescending(x =>
                x.Probabilities.TonicConfidence(true)),
            StructureSongsRequestOrdering.TonicConfidenceAsc => songs.OrderBy(x =>
                x.Probabilities.TonicConfidence(true)),
            StructureSongsRequestOrdering.RatingAsc => songs.OrderBy(x => x.IndexHeader.Rating),
            StructureSongsRequestOrdering.RatingDesc => songs.OrderByDescending(x => x.IndexHeader.Rating),
            _ => throw new ArgumentOutOfRangeException()
        }).ToList();

        return new()
        {
            Total = songs.Count,
            TotalPages = songs.Count / request.SongsPerPage + (songs.Count % request.SongsPerPage == 0 ? 0 : 1),
            CurrentPageNumber = request.PageNumber,
            Songs = orderedSongs.Skip((request.PageNumber - 1) * request.SongsPerPage).Take(request.SongsPerPage).ToList(),
            Distributions = new()
            {
                Rating = songs.GetPercentiles(x => x.IndexHeader.Rating),
                TonalityConfidence = songs.GetPercentiles(x => x.Probabilities.TonalityConfidence()),
                TonicConfidence = songs.GetPercentiles(x => x.Probabilities.TonicConfidence(true)),
                TonicScore = songs.GetPercentiles(x => x.TonicScore),
                ScaleScore = songs.GetPercentiles(x => x.ScaleScore),
            },
        };
    }
}
