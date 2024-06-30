using HarmonyDB.Index.Analysis.Models.CompactV1;
using HarmonyDB.Index.Analysis.Models;
using HarmonyDB.Index.Analysis.Services;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using HarmonyDB.Common;
using HarmonyDB.Common.Representations.OneShelf;
using HarmonyDB.Index.Analysis.Tools;
using HarmonyDB.Index.Api.Model.VExternal1;
using HarmonyDB.Index.BusinessLogic.Models;
using Microsoft.Extensions.Logging;
using OneShelf.Common;
using Microsoft.Extensions.Options;

namespace HarmonyDB.Index.BusinessLogic.Services;

public class LoopsStatisticsCache : FileCacheBase<IReadOnlyDictionary<string, CompactLoopStatistics>, List<LoopStatistics>>
{
    private readonly ProgressionsSearch _progressionsSearch;
    private readonly ProgressionsCache _progressionsCache;

    public LoopsStatisticsCache(ILogger<LoopsStatisticsCache> logger, ProgressionsSearch progressionsSearch,
        ProgressionsCache progressionsCache, IOptions<FileCacheBaseOptions> options)
        : base(logger, options)
    {
        _progressionsSearch = progressionsSearch;
        _progressionsCache = progressionsCache;
    }

    protected override string Key => "LoopStatistics";

    protected override List<LoopStatistics> ToPresentationModel(IReadOnlyDictionary<string, CompactLoopStatistics> fileModel)
    {
        string ToChord(int note, ChordType chordType) => $"{new Note(note, NoteAlteration.Sharp).Representation(new())}{chordType.ChordTypeToString()}";

        Logger.LogInformation("{count} unique songs participating in the loops statistics.", fileModel.Values.SelectMany(x => x.ExternalIds).Distinct().Count());

        return fileModel
            .Select(l =>
            {
                var sequence = Loop.Deserialize(l.Key);
                var rootsStatistics = Convert.FromBase64String(l.Value.Counts).AsIReadOnlyList();
                var note = rootsStatistics.WithIndices().MaxBy(x => x.x).i;
                return new LoopStatistics
                {
                    Progression = string.Join(" ", ToChord(note, sequence.Span[0].FromType)
                        .Once()
                        .Concat(
                            MemoryMarshal.ToEnumerable(sequence)
                            //.Take(sequence.Length - 1)
                            .Select(m =>
                            {
                                note = Note.Normalize(note + m.RootDelta);
                                return ToChord(note, m.ToType);
                            }))),
                    Length = sequence.Length,
                    TotalOccurrences = l.Value.TotalOccurrences,
                    TotalSuccessions = l.Value.TotalSuccessions,
                    TotalSongs = l.Value.ExternalIds.Count,
                    IsCompound = _progressionsSearch.IsCompound(sequence),
                    RootsStatistics = rootsStatistics,
                };
            })
            .OrderByDescending(x => x.TotalOccurrences)
            .ThenByDescending(x => x.TotalSuccessions)
            .ToList();
    }

    public async Task Rebuild()
    {
        await StreamCompressSerialize(await GetAllLoops(await _progressionsCache.Get()));
    }

    private async Task<Dictionary<string, CompactLoopStatistics>> GetAllLoops(IReadOnlyDictionary<string, CompactChordsProgression> dictionary)
    {
        var cc = 0;
        var cf = 0;
        ConcurrentDictionary<string, (CompactLoopStatistics loopStatistics, int[] counts)> loopStatisticsBag = new();

        await Parallel.ForEachAsync(dictionary, (x, _) =>
        {
            var (externalId, compactChordsProgression) = x;
            try
            {
                var loops = _progressionsSearch.FindAllLoops(compactChordsProgression.ExtendedHarmonyMovementsSequences);

                foreach (var loop in loops)
                {
                    var serialized = Loop.Serialize(loop.GetNormalizedProgression(out var normalizationShift));
                    var shift = Loop.InvertNormalizationShift(normalizationShift, loop.Length);
                    var root = Note.Normalize(
                        compactChordsProgression
                            .ExtendedHarmonyMovementsSequences[loop.SequenceIndex]
                            .FirstRoot
                        + MemoryMarshal.ToEnumerable(compactChordsProgression
                                .ExtendedHarmonyMovementsSequences[loop.SequenceIndex].Movements)
                            .Take(loop.Start + shift)
                            .Sum(x => x.RootDelta));

                    var (loopStatistics, counts) = loopStatisticsBag.GetOrAdd(serialized, _ => (new()
                    {
                        ExternalIds = new(),
                        Counts = null!, // will be overridden later
                    }, new int[12]));

                    lock (loopStatistics)
                    {
                        loopStatistics.ExternalIds.Add(externalId);
                        loopStatistics.TotalOccurrences += loop.Occurrences;
                        loopStatistics.TotalSuccessions += loop.Successions;
                        counts[root]++;
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

        return loopStatisticsBag
            .ToDictionary(
                x => x.Key, 
                x =>
                {
                    var max = x.Value.counts.Max();
                    return x.Value.loopStatistics with
                    {
                        Counts = Convert.ToBase64String(x.Value.counts.Select(x => (byte)(max <= 255 ? x : x * 255 / max)).ToArray()),
                    };
                });
    }
}