using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using HarmonyDB.Index.Analysis.Models.CompactV1;
using HarmonyDB.Index.Analysis.Models;
using HarmonyDB.Index.Analysis.Services;
using HarmonyDB.Common.Representations.OneShelf;
using HarmonyDB.Index.Api.Model.VExternal1;
using HarmonyDB.Index.BusinessLogic.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OneShelf.Common;

namespace HarmonyDB.Index.BusinessLogic.Services;

public class LoopsStatistics2Cache : FileCacheBase<object, List<LoopStatistics>>
{
    private const int ProbabilitiesLength = Note.Modulus * 2;

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

    protected override List<LoopStatistics> ToPresentationModel(object fileModel)
    {
        throw new NotImplementedException();
    }

    public async Task Rebuild()
    {
        //await Try();
        //return;
        
        var progressions = await _progressionsCache.Get();
        var indexHeaders = await _indexHeadersCache.Get();

        var songsKeys = GetSongsKeys(indexHeaders);
        var known = await GetKnownSongsLoopsKeys(progressions, songsKeys);
        var all = await GetAllSongsLoops(progressions);

        var knownExternalIds = known.Select(x => x.externalId).ToHashSet();

        //all = all
        //    .Where(x => knownExternalIds.Contains(x.externalId))
        //    .Concat(all.GroupBy(x => x.externalId).Where(x => !knownExternalIds.Contains(x.Key)).Take(20000).SelectMany(x => x))
        //    .OrderBy(_ => Random.Shared.NextDouble())
        //    .ToList();

        var allExternalIds = all.Select(x => x.externalId).ToHashSet();

        var songWeights = all
            .GroupBy(x => x.externalId)
            .ToDictionary(x => x.Key, x => knownExternalIds.Contains(x.Key) ? 3 : 1);

        var loopWeights = all
            .GroupBy(x => x.normalized)
            .ToDictionary(x => x.Key, x => x.Sum(x => x.weight));

        var initialSongsKeys = songsKeys
            .Where(x => allExternalIds.Contains(x.Key))
            .ToDictionary(
                x => x.Key,
                x =>
                {
                    var result = CreateNewProbabilities(true);
                    result[ToIndex(x.Value.songRoot, x.Value.mode)] = 1;
                    return (probabilities: result, stable: true);
                });

        initialSongsKeys.AddRange(allExternalIds
            .Where(x => !initialSongsKeys.ContainsKey(x))
            .Select(x => (p: x, (CreateNewProbabilities(false), false))),
            false);
        
        var initialLoopsKeys = known
            .GroupBy(x => x.normalized)
            .ToDictionary(
                x => x.Key,
                x => x
                    .Select(x => (x.weight, index: ToIndex(x.loopRoot, x.mode)))
                    .GroupBy(x => x.index, x => x.weight)
                    .Select(g => (index: g.Key, weight: g.Sum()))
                    .ToList()
                    .SelectSingle(x =>
                    {
                        var result = CreateNewProbabilities(true);
                        var total = x.Sum(x => x.weight);
                        foreach (var (index, weight) in x)
                        {
                            result[index] = (float)weight / total;
                        }

                        return (probabilities: result, stable: false);
                    }));

        initialLoopsKeys.AddRange(all
            .Select(x => x.normalized)
            .Where(x => !initialLoopsKeys.ContainsKey(x))
            .Select(x => (x, (CreateNewProbabilities(false), false))), 
            false);

        Balance(all, initialSongsKeys, initialLoopsKeys, songWeights, loopWeights);
    }

    private async Task Try()
    {
        //Balance(new()
        //    {
        //    ("L1", "S1", 5, 10),
        //    ("L2", "S1", 1, 10),
        //    ("L3", "S1", 8, 10),

        //    ("L1", "S2", 6, 10),
        //    ("L2", "S2", 2, 10),
        //    ("L3", "S2", 9, 10),
        //},
        //    new Dictionary<string, float[]>
        //    {
        //        { "S1", Enumerable.Range(0, 24).Select(x => x == 2 ? 1f/5 : x == 5 ? 4f / 5 : 0).ToArray() },
        //        { "S2", CreateNewProbabilities(false) },
        //    },
        //    new Dictionary<string, float[]>
        //    {
        //        { "L1", CreateNewProbabilities(false) },
        //        { "L2", CreateNewProbabilities(false) },
        //        { "L3", CreateNewProbabilities(false) },
        //    });
    }

    private void Balance(List<(string normalized, string externalId, byte normalizationRoot, int weight)> all,
        Dictionary<string, (float[] probabilities, bool stable)> songsKeys,
        Dictionary<string, (float[] probabilities, bool stable)> loopsKeys,
        Dictionary<string, int> songWeights,
        Dictionary<string, int> loopWeights)
    {
        Dictionary<string, (float[] probabilities, bool stable)> previousSongsKeys = songsKeys.ToDictionary(x => x.Key, x => (x.Value.probabilities.ToArray(), x.Value.stable));
        Dictionary<string, (float[] probabilities, bool stable)> previousLoopsKeys = loopsKeys.ToDictionary(x => x.Key, x => (x.Value.probabilities.ToArray(), x.Value.stable));

        var songLoops = all
            .GroupBy(x => x.externalId)
            .Select(g => (
                externalId: g.Key,
                loops: g
                    .GroupBy(x => x.normalized)
                    .Select(g => (
                        normalized: g.Key,
                        loopWeight: loopWeights.GetValueOrDefault(g.Key, 1),
                        data: g.Select(x => (x.normalizationRoot, x.weight)).ToList()))
                    .ToList()))
            .ToList();

        var loopSongs = all
            .GroupBy(x => x.normalized)
            .Select(g => (
                normalized: g.Key,
                songs: g
                    .GroupBy(x => x.externalId)
                    .Select(g => (
                        externalId: g.Key,
                        songWeight: songWeights.GetValueOrDefault(g.Key, 1),
                        data: g.Select(x => (x.normalizationRoot, x.weight)).ToList()))
                    .ToList()))
            .ToList();

        while (true)
        {
            CalculateSongsKeys(previousLoopsKeys, songLoops, songsKeys);
            CalculateLoopsKeys(previousSongsKeys, loopSongs, loopsKeys);

            var songsDelta = CalculateDelta(previousSongsKeys, songsKeys);
            var loopsDelta = CalculateDelta(previousLoopsKeys, loopsKeys);

            _logger.LogInformation($"delta: songs {songsDelta}, loops {loopsDelta}");

            (loopsKeys, previousLoopsKeys) = (previousLoopsKeys, loopsKeys);
            (songsKeys, previousSongsKeys) = (previousSongsKeys, songsKeys);
        }
    }

    private void CalculateLoopsKeys(
        IReadOnlyDictionary<string, (float[] probabilities, bool stable)> songsKeys,
        List<(string normalized, List<(string externalId, int songWeight, List<(byte normalizationRoot, int weight)> data)> songs)> loopSongs,
        IReadOnlyDictionary<string, (float[] probabilities, bool stable)> result)
    {
        Parallel.ForEach(loopSongs, x =>
        {
            if (result[x.normalized].stable) return;
            var values = x.songs // for each loop
                .SelectMany(x => // for each song
                {
                    var songWeight = x.songWeight;
                    var songKeys = songsKeys[x.externalId]; // what keys that song is in (probabilities)
                    var data = x.data; // loop containment in the song, with weights and normalizationRoot

                    // Normalize(normalizationRoot - songRoot) == loopRoot
                    // Normalize(normalizationRoot - loopRoot) == songRoot
                    // Normalize(loopRoot + songRoot) == normalizationRoot

                    return songKeys
                        .probabilities
                        .WithIndices()
                        .SelectMany(x =>
                        {
                            var (songRoot, chordType) = FromIndex(x.i);
                            var songProbability = x.x;
                            return data.Select(x =>
                            {
                                var loopRoot = Note.Normalize(x.normalizationRoot - songRoot);
                                return (index: ToIndex(loopRoot, chordType), value: songProbability * x.weight * songWeight);
                            }).ToList();
                        });
                })
                .GroupBy(x => x.index)
                .Select(g => (index: g.Key, value: g.Sum(x => x.value)))
                .ToList();

            var sum = values.Sum(x => x.value);
            
            var probabilities = result[x.normalized].probabilities;

            if (sum <= float.Epsilon)
            {
                for (var i = 0; i < ProbabilitiesLength; i++)
                {
                    probabilities[i] = 1f / ProbabilitiesLength;
                }
            }
            else
            {
                probabilities.Initialize();
                foreach (var (index, value) in values)
                {
                    probabilities[index] = value / sum;
                }
            }
        });
    }

    private void CalculateSongsKeys(
        IReadOnlyDictionary<string, (float[] probabilities, bool stable)> loopsKeys,
        List<(string externalId, List<(string normalized, int loopWeight, List<(byte normalizationRoot, int weight)> data)> loops)> songLoops,
        IReadOnlyDictionary<string, (float[] probabilities, bool stable)> result)
    {
        Parallel.ForEach(songLoops, x =>
        {
            if (result[x.externalId].stable) return;
            var values = x.loops // for each song
                .SelectMany(x => // for each loop
                {
                    var loopWeight = x.loopWeight;
                    var loopKeys = loopsKeys[x.normalized]; // what keys that loop is in (probabilities)
                    var data = x.data; // songs containing the loop, with weights and normalizationRoot

                    // Normalize(normalizationRoot - songRoot) == loopRoot
                    // Normalize(normalizationRoot - loopRoot) == songRoot
                    // Normalize(loopRoot + songRoot) == normalizationRoot

                    return loopKeys
                        .probabilities
                        .WithIndices()
                        .SelectMany(x =>
                        {
                            var (loopRoot, chordType) = FromIndex(x.i);
                            var loopProbability = x.x;
                            return data.Select(x =>
                            {
                                var songRoot = Note.Normalize(x.normalizationRoot - loopRoot);
                                return (index: ToIndex(songRoot, chordType), value: loopProbability * x.weight * loopWeight);
                            }).ToList();
                        });
                })
                .GroupBy(x => x.index)
                .Select(g => (index: g.Key, value: g.Sum(x => x.value)))
                .ToList();

            var sum = values.Sum(x => x.value);

            var probabilities = result[x.externalId].probabilities;
            if (sum <= float.Epsilon)
            {
                for (var i = 0; i < ProbabilitiesLength; i++)
                {
                    probabilities[i] = 1f / ProbabilitiesLength;
                }
            }
            else
            {
                probabilities.Initialize();
                foreach (var (index, value) in values)
                {
                    probabilities[index] = value / sum;
                }
            }
        });
    }

    private static float[] CreateNewProbabilities(bool empty)
    {
        var result = new float[ProbabilitiesLength];
        if (!empty)
        {
            for (var i = 0; i < ProbabilitiesLength; i++)
            {
                result[i] = 1f / ProbabilitiesLength;
            }
        }

        return result;
    }

    private (float average, float max, float sum) CalculateDelta(IReadOnlyDictionary<string, (float[] probabilities, bool stable)> previousValues, IReadOnlyDictionary<string, (float[] probabilities, bool stable)> values)
    {
        var all = previousValues
            .SelectMany(p => p.Value.probabilities.Zip(values[p.Key].probabilities).Select(p => Math.Abs(p.First - p.Second)))
            .ToList();

        return (all.Average(), all.Max(), all.Sum());
    }

    private static int GetWeight(int occurrences, int successions) => occurrences + successions * 2;

    private static int ToIndex(byte root, ChordType chordType) => root * 2 + (int)chordType;

    private static (byte root, ChordType chordType) FromIndex(int index) => ((byte)(index / 2), (ChordType)(index % 2));

    private Dictionary<string, (byte songRoot, ChordType mode)> GetSongsKeys(IndexHeaders indexHeaders) =>
        indexHeaders
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
                    ChordType mode;

                    switch (tonality)
                    {
                        case "#":
                            note = note.Sharp();
                            mode = ChordType.Major;
                            break;
                        case "b":
                            note = note.Flat();
                            mode = ChordType.Major;
                            break;
                        case "#m":
                            note = note.Sharp();
                            mode = ChordType.Minor;
                            break;
                        case "bm":
                            note = note.Flat();
                            mode = ChordType.Minor;
                            break;
                        case "":
                            mode = ChordType.Major;
                            break;
                        case "m":
                            mode = ChordType.Minor;
                            break;
                        default:
                            throw new ArgumentOutOfRangeException(nameof(tonality), tonality,
                                "Unexpected alteration / minority.");
                    }

                    return ((string externalId, byte songRoot, ChordType type)?)(x.Key, note.Value, mode);
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Could not parse the best tonality {key}", tonality);
                    return null;
                }
            })
            .Where(x => x.HasValue)
            .ToDictionary(x => x.Value.externalId, x => (x.Value.songRoot, x.Value.type));

    private async Task<List<(string normalized, string externalId, byte loopRoot, ChordType mode, int weight)>> GetKnownSongsLoopsKeys(
        IReadOnlyDictionary<string, CompactChordsProgression> progressions,
        Dictionary<string, (byte songRoot, ChordType mode)> songsKeys)
    {
        var cc = 0;
        var cf = 0;
        List<(string normalized, string externalId, byte loopRoot, ChordType mode, int weight)> result = new();

        await Parallel.ForEachAsync(progressions.Where(x => songsKeys.ContainsKey(x.Key)), (x, __) =>
        {
            var (externalId, compactChordsProgression) = x;
            var (songRoot, mode) = songsKeys[x.Key];
            try
            {
                var loopResults = new Dictionary<(string normalized, byte loopRoot, ChordType mode), (int occurrences, int successions)>();
                foreach (var extendedHarmonyMovementsSequence in compactChordsProgression.ExtendedHarmonyMovementsSequences)
                {
                    var loops = _indexExtractor.FindSimpleLoops(extendedHarmonyMovementsSequence.Movements, extendedHarmonyMovementsSequence.FirstRoot);

                    foreach (var loop in loops)
                    {
                        var key = (loop.Normalized, Note.Normalize(loop.NormalizationRoot - songRoot), mode);
                        var counters = loopResults.GetValueOrDefault(key);
                        loopResults[key] = (
                            counters.occurrences + (loop.EndIndex - loop.StartIndex + 1) / loop.LoopLength,
                            counters.successions + (loop.EndIndex - loop.StartIndex + 1) / loop.LoopLength - 1);
                    }

                    if (cc++ % 1000 == 0) Logger.LogInformation($"{cc} s, {cf} f");
                }

                var items = loopResults
                    .Select(p => (p.Key.normalized, externalId, p.Key.loopRoot, p.Key.mode, GetWeight(p.Value.occurrences, p.Value.successions)))
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

        return result;
    }

    private async Task<List<(string normalized, string externalId, byte normalizationRoot, int weight)>> GetAllSongsLoops(
        IReadOnlyDictionary<string, CompactChordsProgression> progressions)
    {
        var cc = 0;
        var cf = 0;
        List<(string normalized, string externalId, byte normalizationRoot, int weight)> result = new();

        await Parallel.ForEachAsync(progressions, (x, __) =>
        {
            var (externalId, compactChordsProgression) = x;
            try
            {
                var loopResults = new Dictionary<(string normalized, byte normalizationRoot), (int occurrences, int successions)>();
                foreach (var extendedHarmonyMovementsSequence in compactChordsProgression.ExtendedHarmonyMovementsSequences)
                {
                    var loops = _indexExtractor.FindSimpleLoops(extendedHarmonyMovementsSequence.Movements, extendedHarmonyMovementsSequence.FirstRoot);

                    foreach (var loop in loops)
                    {
                        var key = (loop.Normalized, loop.NormalizationRoot);
                        var counters = loopResults.GetValueOrDefault(key);
                        loopResults[key] = (
                            counters.occurrences + (loop.EndIndex - loop.StartIndex + 1) / loop.LoopLength,
                            counters.successions + (loop.EndIndex - loop.StartIndex + 1) / loop.LoopLength - 1);
                    }

                    if (cc++ % 1000 == 0) Logger.LogInformation($"{cc} s, {cf} f");
                }

                var items = loopResults
                    .Select(p => (p.Key.normalized, externalId, p.Key.normalizationRoot, GetWeight(p.Value.occurrences, p.Value.successions)))
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

        return result;
    }
}