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
    private readonly IndexHeadersCache _indexHeadersCache;
    private readonly ILogger<TonalitiesCache> _logger;
    private readonly TonalitiesBalancer _tonalitiesBalancer;
    private readonly MusicAnalyzer _musicAnalyzer;
    private readonly StructuresCache _structuresCache;

    public TonalitiesCache(
        ILogger<TonalitiesCache> logger,
        IOptions<FileCacheBaseOptions> options, 
        IndexHeadersCache indexHeadersCache, 
        TonalitiesBalancer tonalitiesBalancer,
        MusicAnalyzer musicAnalyzer,
        StructuresCache structuresCache)
        : base(logger, options)
    {
        _logger = logger;
        _indexHeadersCache = indexHeadersCache;
        _tonalitiesBalancer = tonalitiesBalancer;
        _musicAnalyzer = musicAnalyzer;
        _structuresCache = structuresCache;
    }

    protected override string Key => "Tonalities";

    protected override EmModel ToPresentationModel(byte[] fileModel)
    {
        return EmModel.Deserialize(fileModel);
    }

    public async Task Rebuild(int? limitUnknown = null)
    {
        var indexHeaders = await _indexHeadersCache.Get();

        var songsKeys = GetSongsKeys(indexHeaders);

        var all = (await _structuresCache.Get()).Links;
        if (limitUnknown.HasValue)
        {
            var limit = all
                .Select(x => x.ExternalId)
                .Distinct()
                .Where(x => !songsKeys.ContainsKey(x))
                .OrderBy(_ => Random.Shared.NextDouble())
                .Take(limitUnknown.Value)
                .ToHashSet();

            all = all.Where(x => songsKeys.ContainsKey(x.ExternalId) || limit.Contains(x.ExternalId)).ToList();
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