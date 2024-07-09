using HarmonyDB.Index.Analysis.Em.Models;
using HarmonyDB.Index.Analysis.Em.Services;
using HarmonyDB.Index.Analysis.Models.Em;
using HarmonyDB.Index.Analysis.Models.Index;
using HarmonyDB.Index.Analysis.Tools;
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
    private readonly MusicAnalyzer _musicAnalyzer;
    private readonly StructuresCache _structuresCache;

    public TonalitiesCache(
        ILogger<TonalitiesCache> logger,
        IOptions<FileCacheBaseOptions> options, 
        IndexHeadersCache indexHeadersCache, 
        MusicAnalyzer musicAnalyzer,
        StructuresCache structuresCache)
        : base(logger, options)
    {
        _logger = logger;
        _indexHeadersCache = indexHeadersCache;
        _musicAnalyzer = musicAnalyzer;
        _structuresCache = structuresCache;
    }

    protected override string Key => "Tonalities";

    protected override async Task<EmModel> ToPresentationModel(byte[] fileModel)
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

        var (emModel, loopLinks) = GetEmModel(all, songsKeys);

        var emContext = _musicAnalyzer.CreateContext(loopLinks);
        _musicAnalyzer.UpdateProbabilities(emModel, emContext);

        await StreamCompressSerialize(emModel.Serialize());
    }

    private Dictionary<string, (byte songRoot, Scale scale)> GetSongsKeys(IndexHeaders indexHeaders) =>
        indexHeaders
            .Headers
            //.Where(x => x.Value.BestTonality != null)
            .Where(x => x.Value.BestTonality?.IsReliable == true)
            .Select(x =>
            {
                var tonality = x.Value.BestTonality!.Tonality;

                var parsed = tonality.TryParseBestTonality();
                if (!parsed.HasValue)
                {
                    _logger.LogWarning("Could not parse the best tonality {key}", tonality);
                    return null;
                }

                return (externalId: x.Key, parsed: (parsed.Value.root, parsed.Value.isMinor ? Scale.Minor : Scale.Major)).OnceAsNullable();
            })
            .Where(x => x.HasValue)
            .ToDictionary(x => x!.Value.externalId, x => x!.Value.parsed);

    private (EmModel emModel, IReadOnlyList<LoopLink> loopLinks) GetEmModel(
        IReadOnlyList<StructureLink> all,
        Dictionary<string, (byte songRoot, Scale scale)> songsKeys)
    {
        var loops = all
            .GroupBy(x => x.Normalized)
            .ToDictionary(
                x => x.Key,
                x => new Loop
                {
                    Id = x.Key,
                    Length = (byte)Analysis.Models.Loop.Deserialize(x.Key).Length,
                });

        var songs = all
            .GroupBy(x => x.ExternalId)
            .ToDictionary(
                x => x.Key,
                x => new Song
                {
                    Id = x.Key,
                    KnownTonality = songsKeys.TryGetValue(x.Key, out var known) ? known : null,
                });

        var links = all
            .Select(x => new LoopLink
            {
                Loop = loops[x.Normalized],
                Song = songs[x.ExternalId],
                Shift = x.NormalizationRoot,
                Occurrences = x.Occurrences,
                Successions = x.Successions,
            })
            .ToList();

        return (new(songs.Values, loops.Values), links);
    }
}