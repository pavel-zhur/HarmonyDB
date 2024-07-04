using HarmonyDB.Index.Analysis.Models.CompactV1;
using HarmonyDB.Index.Analysis.Models;
using HarmonyDB.Index.Analysis.Services;
using HarmonyDB.Common.Representations.OneShelf;
using HarmonyDB.Index.BusinessLogic.Models;
using Microsoft.Extensions.Logging;
using OneShelf.Common;
using Microsoft.Extensions.Options;

namespace HarmonyDB.Index.BusinessLogic.Services;

public class LoopsStatistics2Cache : FileCacheBase<object, object>
{
    private readonly ProgressionsCache _progressionsCache;
    private readonly IndexExtractor _indexExtractor;
    private readonly IndexHeadersCache _indexHeadersCache;
    private readonly ILogger<LoopsStatistics2Cache> _logger;

    public LoopsStatistics2Cache(ILogger<LoopsStatistics2Cache> logger,
        ProgressionsCache progressionsCache, IOptions<FileCacheBaseOptions> options, IndexExtractor indexExtractor, IndexHeadersCache indexHeadersCache)
        : base(logger, options)
    {
        _logger = logger;
        _progressionsCache = progressionsCache;
        _indexExtractor = indexExtractor;
        _indexHeadersCache = indexHeadersCache;
    }

    protected override string Key => "LoopStatistics2";

    protected override List<object> ToPresentationModel(object fileModel)
    {
        throw new NotImplementedException();
        //string ToChord(byte note, ChordType chordType) => $"{new Note(note, NoteAlteration.Sharp).Representation(new())}{chordType.ChordTypeToString()}";

        //Logger.LogInformation("{count} unique songs participating in the loops statistics.", fileModel.Values.SelectMany(x => x.ExternalIds).Distinct().Count());

        //return fileModel
        //    .Select(l =>
        //    {
        //        var sequence = Loop.Deserialize(l.Key);
        //        var rootsStatistics = Convert.FromBase64String(l.Value.Counts).AsIReadOnlyList();
        //        var note = (byte)rootsStatistics.WithIndices().MaxBy(x => x.x).i;
        //        return new LoopStatistics2
        //        {
        //            Progression = string.Join(" ", ToChord(note, sequence.Span[0].FromType)
        //                .Once()
        //                .Concat(
        //                    MemoryMarshal.ToEnumerable(sequence)
        //                    //.Take(sequence.Length - 1)
        //                    .Select(m =>
        //                    {
        //                        note = Note.Normalize(note + m.RootDelta);
        //                        return ToChord(note, m.ToType);
        //                    }))),
        //            Length = sequence.Length,
        //            TotalOccurrences = l.Value.TotalOccurrences,
        //            TotalSuccessions = l.Value.TotalSuccessions,
        //            TotalSongs = l.Value.ExternalIds.Count,
        //            RootsStatistics = rootsStatistics,
        //        };
        //    })
        //    .OrderByDescending(x => x.TotalOccurrences)
        //    .ThenByDescending(x => x.TotalSuccessions)
        //    .ToList();
    }

    public async Task Rebuild()
    {
        var progressions = await _progressionsCache.Get();
        var indexHeaders = await _indexHeadersCache.Get();

        var songsKeys = GetSongsKeys(indexHeaders);
        var loopsKeys = await GetLoopsKeys(progressions, songsKeys);

        var loopsKeysNormalized = loopsKeys.GroupBy(x => x.Key.normalized)
            .OrderByDescending(x => x.Sum(x => x.Value.Sum(x => x.successions)))
            .ToDictionary(
                x => x.Key,
                g => g
                    .SelectMany(x =>
                    {
                        return x.Value.Select(y => (
                            y.normalizationRootNormalized,
                            y.songMode,
                            y.occurrences,
                            y.successions,
                            x.Key.externalId));
                    })
                    .GroupBy(x => (x.normalizationRootNormalized, x.songMode))
                    .OrderByDescending(x => x.Sum(x => x.successions))
                    .ToDictionary(x => x.Key,
                        x => x.Select(x => (x.externalId, x.occurrences, x.successions)).ToList()));

        //await StreamCompressSerialize(await GetAllLoops(await _progressionsCache.Get()));
        throw new NotImplementedException();
    }

    private Dictionary<string, (byte root, ChordType type)> GetSongsKeys(IndexHeaders indexHeaders)
    {
        return indexHeaders
            .Headers
            .Where(x => x.Value.BestTonality?.IsReliable == true)
            .Select(x =>
            {
                var tonality = x.Value.BestTonality.Tonality;

                try
                {
                    if (!Note.CharactersToNotes.TryGetValue(tonality[0], out var note))
                        throw new("Root note not found.");

                    tonality = tonality.Substring(1);
                    ChordType chordType;

                    switch (tonality)
                    {
                        case "#":
                            note = note.Sharp();
                            chordType = ChordType.Major;
                            break;
                        case "b":
                            note = note.Flat();
                            chordType = ChordType.Major;
                            break;
                        case "#m":
                            note = note.Sharp();
                            chordType = ChordType.Minor;
                            break;
                        case "bm":
                            note = note.Flat();
                            chordType = ChordType.Minor;
                            break;
                        case "":
                            chordType = ChordType.Major;
                            break;
                        case "m":
                            chordType = ChordType.Minor;
                            break;
                        default:
                            throw new ArgumentOutOfRangeException(nameof(tonality), tonality,
                                "Unexpected alteration / minority.");
                    }

                    return ((string externalId, byte root, ChordType type)?)(x.Key, note.Value, chordType);
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Could not parse the best tonality {key}", tonality);
                    return null;
                }
            })
            .Where(x => x.HasValue)
            .ToDictionary(x => x.Value.externalId, x => (x.Value.root, x.Value.type));
    }

    private async Task<Dictionary<(string normalized, string externalId), List<(byte normalizationRootNormalized, ChordType songMode, int occurrences, int successions)>>> GetLoopsKeys(
        IReadOnlyDictionary<string, CompactChordsProgression> progressions,
        Dictionary<string, (byte root, ChordType type)> songsKeys)
    {
        var cc = 0;
        var cf = 0;
        Dictionary<(string normalized, string externalId), List<(byte normalizationRootNormalized, ChordType songMode, int occurrences, int successions)>> result = new();

        await Parallel.ForEachAsync(progressions.Where(x => songsKeys.ContainsKey(x.Key)), (x, __) =>
        {
            var (externalId, compactChordsProgression) = x;
            var (songRoot, songMode) = songsKeys[x.Key];
            try
            {
                var loopResults = new Dictionary<(string normalized, byte normalizationRootNormalized, ChordType songMode), (int occurrences, int successions)>();
                foreach (var extendedHarmonyMovementsSequence in compactChordsProgression.ExtendedHarmonyMovementsSequences)
                {
                    var loops = _indexExtractor.FindSimpleLoops(extendedHarmonyMovementsSequence.Movements, extendedHarmonyMovementsSequence.FirstRoot);

                    foreach (var loop in loops)
                    {
                        var key = (loop.Normalized, Note.Normalize(loop.NormalizationRoot - songRoot), songMode);
                        var counters = loopResults.GetValueOrDefault(key);
                        loopResults[key] = (
                            counters.occurrences + (loop.EndIndex - loop.StartIndex + 1) / loop.LoopLength,
                            counters.successions + (loop.EndIndex - loop.StartIndex + 1) / loop.LoopLength - 1);
                    }

                    if (cc++ % 1000 == 0) Logger.LogInformation($"{cc} s, {cf} f");
                }

                var items = loopResults
                    .GroupBy(p => (p.Key.normalized, externalId))
                    .Select(g => (
                        g.Key,
                        g
                            .Select(x => (x.Key.normalizationRootNormalized, x.Key.songMode, x.Value.occurrences, x.Value.successions))
                            .ToList()))
                    .ToList();

                lock (result)
                {
                    result.AddRange(items, false);
                }
            }
            catch (Exception e)
            {
                Logger.LogError(e, "Error adding the loop.");
                Interlocked.Increment(ref cf);
            }

            return ValueTask.CompletedTask;
        });

        return result;
    }
}