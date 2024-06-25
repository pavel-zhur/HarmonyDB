using HarmonyDB.Index.Analysis.Models.CompactV1;
using HarmonyDB.Index.Analysis.Models;
using HarmonyDB.Index.Analysis.Services;
using System.Collections.Concurrent;
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

        var idsToSequences = fileModel.Keys.ToDictionary(x => x, Loop.Deserialize);

        return fileModel
            .Select(l =>
            {
                var sequence = idsToSequences[l.Key].ToArray();
                var note = sequence[0].FromType == ChordType.Minor ? 0 : 3;
                return new LoopStatistics
                {
                    Progression = string.Join(" ", ToChord(note, sequence[0].FromType)
                        .Once()
                        .Concat(sequence
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

    private async Task<ConcurrentDictionary<string, CompactLoopStatistics>> GetAllLoops(IReadOnlyDictionary<string, CompactChordsProgression> dictionary)
    {
        var cc = 0;
        var cf = 0;
        ConcurrentDictionary<string, CompactLoopStatistics> loopsBag = new();

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
                        bag.TotalOccurrences += loop.Occurrences;
                        bag.TotalSuccessions += loop.Successions;
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