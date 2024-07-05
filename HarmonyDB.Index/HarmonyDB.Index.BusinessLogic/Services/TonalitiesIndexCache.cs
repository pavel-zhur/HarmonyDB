using HarmonyDB.Index.Analysis.Models;
using HarmonyDB.Index.Analysis.Services;
using HarmonyDB.Index.BusinessLogic.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OneShelf.Common;
using HarmonyDB.Index.BusinessLogic.Services.Caches.Bases;
using HarmonyDB.Index.BusinessLogic.Services.Caches;

namespace HarmonyDB.Index.BusinessLogic.Services;

public class TonalitiesIndexCache : BytesFileCacheBase<TonalitiesIndex>
{
    private readonly ProgressionsCache _progressionsCache;
    private readonly IndexHeadersCache _indexHeadersCache;
    private readonly ILogger<TonalitiesIndexCache> _logger;
    private readonly TonalitiesBalancer _tonalitiesBalancer;

    public TonalitiesIndexCache(ILogger<TonalitiesIndexCache> logger,
        ProgressionsCache progressionsCache, IOptions<FileCacheBaseOptions> options, IndexHeadersCache indexHeadersCache, TonalitiesBalancer tonalitiesBalancer)
        : base(logger, options)
    {
        _logger = logger;
        _progressionsCache = progressionsCache;
        _indexHeadersCache = indexHeadersCache;
        _tonalitiesBalancer = tonalitiesBalancer;
    }

    protected override string Key => "TonalitiesIndex";

    protected override TonalitiesIndex ToPresentationModel(byte[] fileModel)
    {
        return TonalitiesIndex.Deserialize(fileModel);
    }

    public async Task Rebuild(int? limitUnknown = null)
    {
        var progressions = await _progressionsCache.Get();
        var indexHeaders = await _indexHeadersCache.Get();

        var songsKeys = GetSongsKeys(indexHeaders);
        var known = await _tonalitiesBalancer.GetKnownSongsLoopsKeys(progressions, songsKeys);
        var all = await _tonalitiesBalancer.GetAllSongsLoops(progressions);

        if (limitUnknown.HasValue)
        {
            var knownExternalIds = known.Select(x => x.externalId).ToHashSet();
            all = all
                .Where(x => knownExternalIds.Contains(x.externalId))
                .Concat(all
                    .GroupBy(x => x.externalId)
                    .Where(x => !knownExternalIds.Contains(x.Key)).Take(limitUnknown.Value)
                    .SelectMany(x => x))
                .OrderBy(_ => Random.Shared.NextDouble())
                .ToList();
        }

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

        var initialLoopsKeys = all.Select(x => x.normalized).Distinct().ToDictionary(x => x,
            x => (_tonalitiesBalancer.CreateNewProbabilities(false), false));

        var result = await _tonalitiesBalancer.Balance(all, initialSongsKeys, initialLoopsKeys);
        await StreamCompressSerialize(TonalitiesIndex.Serialize(result, all));
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