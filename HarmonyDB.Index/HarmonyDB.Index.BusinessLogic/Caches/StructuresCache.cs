using HarmonyDB.Index.Analysis.Models.CompactV1;
using HarmonyDB.Index.Analysis.Models.Index;
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

    public StructuresCache(
        ILogger<FileCacheBase<byte[], Structures>> logger,
        IOptions<FileCacheBaseOptions> options, 
        ProgressionsCache progressionsCache,
        IndexExtractor indexExtractor) : base(logger, options)
    {
        _progressionsCache = progressionsCache;
        _indexExtractor = indexExtractor;
    }

    protected override string Key => "Structures";

    protected override async Task<Structures> ToPresentationModel(byte[] fileModel) => Structures.Deserialize(fileModel);

    public async Task Rebuild()
    {
        var progressions = await _progressionsCache.Get();

        var cc = 0;
        var cf = 0;
        List<StructureLink> result = new();

        await Parallel.ForEachAsync(progressions, (x, __) =>
        {
            var (externalId, compactChordsProgression) = x;
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