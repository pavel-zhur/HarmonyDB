using HarmonyDB.Index.Analysis.Em.Models;
using HarmonyDB.Index.Analysis.Em.Services;
using HarmonyDB.Index.Analysis.Models.Em;
using HarmonyDB.Index.Analysis.Services;
using HarmonyDB.Index.BusinessLogic.Caches.Bases;
using HarmonyDB.Index.BusinessLogic.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OneShelf.Common;

namespace HarmonyDB.Index.BusinessLogic.Caches;

public class TonalitiesCache : BytesFileCacheBase<EmModel>
{
    private readonly ProgressionsCache _progressionsCache;
    private readonly IndexHeadersCache _indexHeadersCache;
    private readonly ILogger<TonalitiesCache> _logger;
    private readonly TonalitiesBalancer _tonalitiesBalancer;
    private readonly MusicAnalyzer _musicAnalyzer;

    public TonalitiesCache(ILogger<TonalitiesCache> logger,
        ProgressionsCache progressionsCache, IOptions<FileCacheBaseOptions> options, IndexHeadersCache indexHeadersCache, TonalitiesBalancer tonalitiesBalancer, MusicAnalyzer musicAnalyzer)
        : base(logger, options)
    {
        _logger = logger;
        _progressionsCache = progressionsCache;
        _indexHeadersCache = indexHeadersCache;
        _tonalitiesBalancer = tonalitiesBalancer;
        _musicAnalyzer = musicAnalyzer;
    }

    protected override string Key => "Tonalities";

    protected override EmModel ToPresentationModel(byte[] fileModel)
    {
        return EmModel.Deserialize(fileModel);
    }

    public async Task Rebuild(int? limitUnknown = null)
    {
        var progressions = await _progressionsCache.Get();
        var indexHeaders = await _indexHeadersCache.Get();

        var songsKeys = GetSongsKeys(indexHeaders);
        var all = await _tonalitiesBalancer.GetAllSongsLoops(progressions);

        if (limitUnknown.HasValue)
        {
            var limit = all
                .Select(x => x.externalId)
                .Distinct()
                .Where(x => !songsKeys.ContainsKey(x))
                .OrderBy(_ => Random.Shared.NextDouble())
                .Take(limitUnknown.Value)
                .ToHashSet();

            all = all.Where(x => songsKeys.ContainsKey(x.externalId) || limit.Contains(x.externalId)).ToList();
        }

        var result = _tonalitiesBalancer.GetEmModel(all, songsKeys);

        var emContext = _musicAnalyzer.CreateContext(result);
        _musicAnalyzer.UpdateProbabilities(result, emContext);

        await StreamCompressSerialize(result.Serialize());
    }

    private Dictionary<string, (byte songRoot, Scale mode)> GetSongsKeys(IndexHeaders indexHeaders) =>
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