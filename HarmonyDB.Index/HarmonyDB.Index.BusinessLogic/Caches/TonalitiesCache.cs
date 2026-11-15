using HarmonyDB.Index.Analysis.Em.Models;
using HarmonyDB.Index.Analysis.Em.Services;
using HarmonyDB.Index.Analysis.Models.Em;
using HarmonyDB.Index.Analysis.Models.Ids;
using HarmonyDB.Index.Analysis.Models.Structure;
using HarmonyDB.Index.Analysis.Tools;
using HarmonyDB.Index.BusinessLogic.Caches.Bases;
using HarmonyDB.Index.BusinessLogic.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OneShelf.Common;

namespace HarmonyDB.Index.BusinessLogic.Caches;

public class TonalitiesCache : BytesFileCacheBase<Tonalities>
{
    private readonly IndexHeadersCache _indexHeadersCache;
    private readonly ILogger<TonalitiesCache> _logger;
    private readonly MusicAnalyzer _musicAnalyzer;
    private readonly StructuresCache _structuresCache;
    private readonly IndexIds _ids;

    public TonalitiesCache(
        ILogger<TonalitiesCache> logger,
        IOptions<FileCacheBaseOptions> options, 
        IndexHeadersCache indexHeadersCache, 
        MusicAnalyzer musicAnalyzer,
        StructuresCache structuresCache,
        IndexIds ids)
        : base(logger, options)
    {
        _logger = logger;
        _indexHeadersCache = indexHeadersCache;
        _musicAnalyzer = musicAnalyzer;
        _structuresCache = structuresCache;
        _ids = ids;
    }

    protected override string Key => "Tonalities";

    protected override Tonalities ReadPresentationModel(Stream stream) => Tonalities.Read(stream, _ids);

    public async Task Rebuild(int? limitUnknown = null)
    {
        var indexHeaders = await _indexHeadersCache.Get();

        var songsKeys = GetSongsKeys(indexHeaders);

        var structures = await _structuresCache.Get();
        HashSet<int>? limit = null;
        if (limitUnknown.HasValue)
        {
            limit = Enumerable.Range(0, structures.Songs.Count)
                .Select(structures.Songs.GetNumber)
                .Where(x => !songsKeys.ContainsKey(_ids.Songs[x]))
                .OrderBy(_ => Random.Shared.NextDouble())
                .Take(limitUnknown.Value)
                .Concat(songsKeys.Keys.Where(x => _ids.Songs.TryGetNumber(x, out _)).Select(_ids.Songs.GetNumber))
                .ToHashSet();
        }

        var (emModel, loopLinks) = GetEmModel(structures, songsKeys, limit);

        var emContext = _musicAnalyzer.CreateContext(loopLinks);
        _musicAnalyzer.UpdateProbabilities(emModel, emContext);

        await StreamCompressSerialize(emModel.Serialize());
    }

    private Dictionary<string, (byte songRoot, Scale scale)> GetSongsKeys(IndexHeaders indexHeaders) =>
        Enumerable.Range(0, indexHeaders.Count)
            .Select(indexHeaders.Get)
            .Where(x => x.BestTonality?.IsReliable == true)
            .Select(x =>
            {
                var tonality = x.BestTonality!.Tonality;

                var parsed = tonality.TryParseBestTonality();
                if (!parsed.HasValue)
                {
                    _logger.LogWarning("Could not parse the best tonality {key}", tonality);
                    return null;
                }

                return (externalId: x.ExternalId, parsed: (parsed.Value.root, parsed.Value.isMinor ? Scale.Minor : Scale.Major)).OnceAsNullable();
            })
            .Where(x => x.HasValue)
            .ToDictionary(x => x!.Value.externalId, x => x!.Value.parsed);

    /// <param name="limit">The songs whose links are taken, all if null.</param>
    private (EmModel emModel, IReadOnlyList<LoopLink> loopLinks) GetEmModel(
        Structures structures,
        Dictionary<string, (byte songRoot, Scale scale)> songsKeys,
        HashSet<int>? limit)
    {
        var links = new List<CompactStructureLink>();
        foreach (var link in structures.Links)
        {
            if (limit?.Contains(link.Song) != false)
            {
                links.Add(link);
            }
        }

        var linksLoops = links.Select(x => x.Loop).ToHashSet();
        var linksSongs = links.Select(x => x.Song).ToHashSet();

        var loops = Enumerable.Range(0, structures.Loops.Count)
            .Select(structures.Loops.GetNumber)
            .Where(linksLoops.Contains)
            .ToDictionary(
                x => x,
                x => new Loop
                {
                    Id = _ids.Loops[x],
                });

        var songs = Enumerable.Range(0, structures.Songs.Count)
            .Select(structures.Songs.GetNumber)
            .Where(linksSongs.Contains)
            .ToDictionary(
                x => x,
                x => new Song
                {
                    Id = _ids.Songs[x],
                    KnownTonality = songsKeys.TryGetValue(_ids.Songs[x], out var known) ? known : null,
                });

        var loopLinks = links
            .Select(x => new LoopLink
            {
                Loop = loops[x.Loop],
                Song = songs[x.Song],
                Shift = x.NormalizationRoot,
                Weight = TonalitiesExtensions.GetWeight(x.Occurrences, x.Successions, structures.GetLoop(x.Loop).Length, songsKeys.ContainsKey(_ids.Songs[x.Song]))
            })
            .ToList();

        return (new(songs.Values, loops.Values), loopLinks);
    }
}
