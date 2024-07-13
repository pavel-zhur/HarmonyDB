using HarmonyDB.Index.Analysis.Models.Index;
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
        ProgressionsCache progressionsCache, IndexExtractor indexExtractor) : base(logger, options)
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
                var loopResults = new Dictionary<(string normalized, byte normalizationRoot), (float occurrences, float successions, short length)>();
                foreach (var extendedHarmonyMovementsSequence in compactChordsProgression.ExtendedHarmonyMovementsSequences)
                {
                    var loops = _indexExtractor.FindSimpleLoops(extendedHarmonyMovementsSequence.Movements, extendedHarmonyMovementsSequence.FirstRoot);

                    foreach (var loop in loops)
                    {
                        var key = (loop.Normalized, loop.NormalizationRoot);
                        var counters = loopResults.GetValueOrDefault(key);
                        var length = loop.EndIndex - loop.StartIndex + 1;
                        loopResults[key] = ((float occurrences, float successions, short length))(
                            counters.occurrences + (float)length / loop.LoopLength,
                            counters.successions + (float)length / loop.LoopLength - 1,
                            counters.length + length);
                    }

                    if (cc++ % 1000 == 0) Logger.LogInformation($"{cc} s, {cf} f");
                }

                var items = loopResults
                    .Select(p => new StructureLink(
                        p.Key.normalized, 
                        externalId, 
                        p.Key.normalizationRoot, 
                        p.Value.occurrences, 
                        p.Value.successions,
                        (float)p.Value.length / compactChordsProgression.ExtendedHarmonyMovementsSequences.Sum(s => s.Movements.Length)))
                    .ToList();

                lock (result)
                {
                    result.AddRange(items);
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