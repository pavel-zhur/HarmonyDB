using HarmonyDB.Index.Analysis.Models.CompactV1;
using HarmonyDB.Index.Analysis.Models;
using HarmonyDB.Index.Analysis.Services;
using System.Collections.Concurrent;
using HarmonyDB.Index.BusinessLogic.Models;
using Microsoft.Extensions.Logging;

namespace HarmonyDB.Index.BusinessLogic.Services;

public class LoopsStatisticsCache : FileCacheBase<IReadOnlyDictionary<string, LoopStatistics>, IReadOnlyDictionary<string, LoopStatistics>>
{
    private readonly ProgressionsSearch _progressionsSearch;
    private readonly ProgressionsCache _progressionsCache;

    public LoopsStatisticsCache(ILogger<LoopsStatisticsCache> logger, ProgressionsSearch progressionsSearch, ProgressionsCache progressionsCache) 
        : base(logger)
    {
        _progressionsSearch = progressionsSearch;
        _progressionsCache = progressionsCache;
    }

    protected override string Key => "LoopStatistics";

    protected override IReadOnlyDictionary<string, LoopStatistics> ToPresentationModel(
        IReadOnlyDictionary<string, LoopStatistics> fileModel)
        => fileModel;

    public async Task Rebuild()
    {
        await StreamCompressSerialize(await GetAllLoops(await _progressionsCache.Get()));
    }

    private async Task<ConcurrentDictionary<string, LoopStatistics>> GetAllLoops(IReadOnlyDictionary<string, CompactChordsProgression> dictionary)
    {
        var cc = 0;
        var cf = 0;
        ConcurrentDictionary<string, LoopStatistics> loopsBag = new();

        await Parallel.ForEachAsync(dictionary, (x, _) =>
        {
            var (externalId, compactChordsProgression) = x;
            try
            {
                var loops = _progressionsSearch.FindAllLoops(compactChordsProgression.ExtendedHarmonyMovementsSequences);

                foreach (var loop in loops)
                {
                    var serialized = Loop.Serialize(loop.GetNormalizedProgression());
                    var bag = loopsBag.GetOrAdd(serialized, _ => new()
                    {
                        ExternalIds = new(),
                    });

                    lock (bag)
                    {
                        bag.ExternalIds.Add(externalId);
                        bag.SumOccurrences += loop.Occurrences;
                        bag.SumSuccessions += loop.Successions;
                    }
                }

                if (cc++ % 1000 == 0) Logger.LogInformation($"{cc} s, {cf} f");
            }
            catch (Exception e)
            {
                Logger.LogError(e, "Error adding the loop.");
                Interlocked.Increment(ref cf);
            }

            return ValueTask.CompletedTask;
        });

        return loopsBag;
    }
}