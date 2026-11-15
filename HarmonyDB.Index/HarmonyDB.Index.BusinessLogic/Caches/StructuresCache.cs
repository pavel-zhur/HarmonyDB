using HarmonyDB.Index.Analysis.Models.Ids;
using HarmonyDB.Index.Analysis.Models.Structure;
using HarmonyDB.Index.Analysis.Services;
using HarmonyDB.Index.BusinessLogic.Caches.Bases;
using HarmonyDB.Index.BusinessLogic.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HarmonyDB.Index.BusinessLogic.Caches;

public class StructuresCache : BytesFileCacheBase<Structures>
{
    private readonly ProgressionsCache _progressionsCache;
    private readonly IndexExtractor _indexExtractor;
    private readonly IndexIds _ids;

    public StructuresCache(
        ILogger<FileCacheBase<byte[], Structures>> logger,
        IOptions<FileCacheBaseOptions> options,
        ProgressionsCache progressionsCache,
        IndexExtractor indexExtractor,
        IndexIds ids) : base(logger, options)
    {
        _progressionsCache = progressionsCache;
        _indexExtractor = indexExtractor;
        _ids = ids;
    }

    protected override string Key => "Structures";

    protected override Structures ReadPresentationModel(Stream stream) => Structures.Read(stream, _ids);

    public async Task Rebuild()
    {
        var progressions = await _progressionsCache.Get();

        var cc = 0;
        var cf = 0;
        List<StructureLink> result = new();

        await Parallel.ForEachAsync(Enumerable.Range(0, progressions.Count), (position, _) =>
        {
            var externalId = progressions.GetExternalId(position);
            var compactChordsProgression = progressions.Get(position);
            try
            {
                var items = _indexExtractor.FindStructureLinks(externalId, compactChordsProgression);

                lock (result)
                {
                    result.AddRange(items);

                    if ((cc += items.Count) % 1000 == 0) Logger.LogInformation($"{cc} s, {cf} f");
                }
            }
            catch (Exception e)
            {
                Logger.LogError(e, "Error adding the loop.");
                Interlocked.Increment(ref cf);
            }

            return ValueTask.CompletedTask;
        });

        await StreamCompressSerialize(Structures.Serialize(result));
    }
}
