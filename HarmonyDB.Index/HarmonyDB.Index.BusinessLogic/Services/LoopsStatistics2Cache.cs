using HarmonyDB.Index.Analysis.Models;
using HarmonyDB.Index.Analysis.Services;
using HarmonyDB.Index.Api.Model.VExternal1;
using HarmonyDB.Index.BusinessLogic.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OneShelf.Common;
using HarmonyDB.Index.BusinessLogic.Services.Caches.Bases;
using HarmonyDB.Index.BusinessLogic.Services.Caches;

namespace HarmonyDB.Index.BusinessLogic.Services;

public class LoopsStatistics2Cache : FileCacheBase<object, List<LoopStatistics>>
{
    private readonly ProgressionsCache _progressionsCache;
    private readonly IndexHeadersCache _indexHeadersCache;
    private readonly ILogger<LoopsStatistics2Cache> _logger;
    private readonly TonalitiesBalancer _tonalitiesBalancer;

    public LoopsStatistics2Cache(ILogger<LoopsStatistics2Cache> logger,
        ProgressionsCache progressionsCache, IOptions<FileCacheBaseOptions> options, IndexHeadersCache indexHeadersCache, TonalitiesBalancer tonalitiesBalancer)
        : base(logger, options)
    {
        _logger = logger;
        _progressionsCache = progressionsCache;
        _indexHeadersCache = indexHeadersCache;
        _tonalitiesBalancer = tonalitiesBalancer;
    }

    protected override string Key => "LoopStatistics2";

    protected override List<LoopStatistics> ToPresentationModel(object fileModel)
    {
        throw new NotImplementedException();
    }

    public async Task Rebuild()
    {
        //await Try();
        //return;
        
        var progressions = await _progressionsCache.Get();
        var indexHeaders = await _indexHeadersCache.Get();

        var songsKeys = GetSongsKeys(indexHeaders);
        var known = await _tonalitiesBalancer.GetKnownSongsLoopsKeys(progressions, songsKeys);
        var all = await _tonalitiesBalancer.GetAllSongsLoops(progressions);

        //var knownExternalIds = known.Select(x => x.externalId).ToHashSet();
        //all = all
        //    .Where(x => knownExternalIds.Contains(x.externalId))
        //    .Concat(all.GroupBy(x => x.externalId).Where(x => !knownExternalIds.Contains(x.Key)).Take(20000).SelectMany(x => x))
        //    .OrderBy(_ => Random.Shared.NextDouble())
        //    .ToList();

        var allExternalIds = all.Select(x => x.externalId).ToHashSet();

        var initialSongsKeys = songsKeys
            .Where(x => allExternalIds.Contains(x.Key))
            .ToDictionary(
                x => x.Key,
                x =>
                {
                    var result = _tonalitiesBalancer.CreateNewProbabilities(true);
                    result[_tonalitiesBalancer.ToIndex(x.Value.songRoot, x.Value.mode)] = 1;
                    return (probabilities: result, stable: true);
                });

        initialSongsKeys.AddRange(allExternalIds
            .Where(x => !initialSongsKeys.ContainsKey(x))
            .Select(x => (p: x, (_tonalitiesBalancer.CreateNewProbabilities(false), false))),
            false);

        var initialLoopsKeys = known
            .GroupBy(x => x.normalized)
            .ToDictionary(
                x => x.Key,
                x => x
                    .Select(x => (x.weight, index: _tonalitiesBalancer.ToIndex(x.loopRoot, x.mode)))
                    .GroupBy(x => x.index, x => x.weight)
                    .Select(g => (index: g.Key, weight: g.Sum()))
                    .ToList()
                    .SelectSingle(x =>
                    {
                        var result = _tonalitiesBalancer.CreateNewProbabilities(true);
                        var total = x.Sum(x => x.weight);
                        foreach (var (index, weight) in x)
                        {
                            result[index] = (float)weight / total;
                        }

                        return (probabilities: result, stable: false);
                    }));

        initialLoopsKeys.AddRange(all
            .Select(x => x.normalized)
            .Where(x => !initialLoopsKeys.ContainsKey(x))
            .Select(x => (x, (_tonalitiesBalancer.CreateNewProbabilities(false), false))), 
            false);

        _tonalitiesBalancer.Balance(all, initialSongsKeys, initialLoopsKeys);
    }

    private Dictionary<string, (byte songRoot, ChordType mode)> GetSongsKeys(IndexHeaders indexHeaders) =>
        indexHeaders
            .Headers
            .Where(x => x.Value.BestTonality?.IsReliable == true)
            .Select(x =>
            {
                var tonality = x.Value.BestTonality.Tonality;

                var parsed = _tonalitiesBalancer.TryParseBestTonality(tonality);
                if (!parsed.HasValue)
                {
                    _logger.LogWarning("Could not parse the best tonality {key}", tonality);
                    return null;
                }

                return (externalId: x.Key, parsed: parsed.Value).OnceAsNullable();
            })
            .Where(x => x.HasValue)
            .ToDictionary(x => x!.Value.externalId, x => x!.Value.parsed);
}