using HarmonyDB.Common.Representations.OneShelf;
using HarmonyDB.Index.Analysis.Models.CompactV1;
using HarmonyDB.Index.Analysis.Models;
using Microsoft.Extensions.Logging;
using OneShelf.Common;

namespace HarmonyDB.Index.Analysis.Services;

public class TonalitiesBalancer(ILogger<TonalitiesBalancer> logger, IndexExtractor indexExtractor)
{
    private const int ProbabilitiesLength = Note.Modulus * 2;

    public void Balance(
        List<(string normalized, string externalId, byte normalizationRoot, int weight)> all,
        Dictionary<string, (float[] probabilities, bool stable)> songsKeys,
        Dictionary<string, (float[] probabilities, bool stable)> loopsKeys)
    {
        var previousSongsKeys = Clone(songsKeys);
        var previousLoopsKeys = Clone(loopsKeys);

        var songLoops = all
            .GroupBy(x => x.externalId)
            .Select(g => (
                externalId: g.Key,
                loops: g
                    .GroupBy(x => x.normalized)
                    .Select(g => (
                        normalized: g.Key,
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
                        data: g.Select(x => (x.normalizationRoot, x.weight)).ToList()))
                    .ToList()))
            .ToList();

        var successfulInARow = 0;
        while (true)
        {
            CalculateSongsKeys(previousLoopsKeys, songLoops, songsKeys);
            CalculateLoopsKeys(previousSongsKeys, loopSongs, loopsKeys);

            var songsDelta = CalculateDelta(previousSongsKeys, songsKeys);
            var loopsDelta = CalculateDelta(previousLoopsKeys, loopsKeys);

            if (songsDelta.max < 0.01 && loopsDelta.max < 0.01)
            {
                successfulInARow++;
            }
            else
            {
                successfulInARow = 0;
            }

            if (successfulInARow > 3) break;

            logger.LogInformation($"delta: songs {songsDelta}, loops {loopsDelta}");

            (loopsKeys, previousLoopsKeys) = (previousLoopsKeys, loopsKeys);
            (songsKeys, previousSongsKeys) = (previousSongsKeys, songsKeys);
        }
    }

    private static Dictionary<string, (float[], bool stable)> Clone(Dictionary<string, (float[] probabilities, bool stable)> keys)
    {
        return keys.ToDictionary(x => x.Key, x => (x.Value.probabilities.ToArray(), x.Value.stable));
    }

    public async Task<List<(string normalized, string externalId, byte loopRoot, ChordType mode, int weight)>> GetKnownSongsLoopsKeys(
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
                    var loops = indexExtractor.FindSimpleLoops(extendedHarmonyMovementsSequence.Movements, extendedHarmonyMovementsSequence.FirstRoot);

                    foreach (var loop in loops)
                    {
                        var key = (loop.Normalized, Note.Normalize(loop.NormalizationRoot - songRoot), mode);
                        var counters = loopResults.GetValueOrDefault(key);
                        loopResults[key] = (
                            counters.occurrences + (loop.EndIndex - loop.StartIndex + 1) / loop.LoopLength,
                            counters.successions + (loop.EndIndex - loop.StartIndex + 1) / loop.LoopLength - 1);
                    }

                    if (cc++ % 1000 == 0) logger.LogInformation($"{cc} s, {cf} f");
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
                logger.LogError(e, "Error adding the loop.");
                Interlocked.Increment(ref cf);
            }

            return ValueTask.CompletedTask;
        });

        return result;
    }

    public async Task<List<(string normalized, string externalId, byte normalizationRoot, int weight)>> GetAllSongsLoops(
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
                    var loops = indexExtractor.FindSimpleLoops(extendedHarmonyMovementsSequence.Movements, extendedHarmonyMovementsSequence.FirstRoot);

                    foreach (var loop in loops)
                    {
                        var key = (loop.Normalized, loop.NormalizationRoot);
                        var counters = loopResults.GetValueOrDefault(key);
                        loopResults[key] = (
                            counters.occurrences + (loop.EndIndex - loop.StartIndex + 1) / loop.LoopLength,
                            counters.successions + (loop.EndIndex - loop.StartIndex + 1) / loop.LoopLength - 1);
                    }

                    if (cc++ % 1000 == 0) logger.LogInformation($"{cc} s, {cf} f");
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
                logger.LogError(e, "Error adding the loop.");
                Interlocked.Increment(ref cf);
            }

            return ValueTask.CompletedTask;
        });

        return result;
    }

    public int ToIndex(byte root, ChordType chordType) => root * 2 + (int)chordType;

    public float[] CreateNewProbabilities(bool empty)
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

    public (byte songRoot, ChordType mode)? TryParseBestTonality(string tonality)
    {
        if (!Note.CharactersToNotes.TryGetValue(tonality[0], out var note))
            return null;

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
                return null;
        }

        return (note.Value, mode);
    }

    private void CalculateLoopsKeys(
        IReadOnlyDictionary<string, (float[] probabilities, bool stable)> songsKeys,
        List<(string normalized, List<(string externalId, List<(byte normalizationRoot, int weight)> data)> songs)> loopSongs, 
        IReadOnlyDictionary<string, (float[] probabilities, bool stable)> result)
    {
        Parallel.ForEach(loopSongs, x =>
        {
            if (result[x.normalized].stable) return;
            var values = x.songs // for each loop
                .SelectMany(x => // for each song
                {
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
                                return (index: ToIndex(loopRoot, chordType), value: songProbability * x.weight);
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
        List<(string externalId, List<(string normalized, List<(byte normalizationRoot, int weight)> data)> loops)> songLoops,
        IReadOnlyDictionary<string, (float[] probabilities, bool stable)> result)
    {
        Parallel.ForEach(songLoops, x =>
        {
            if (result[x.externalId].stable) return;
            var values = x.loops // for each song
                .SelectMany(x => // for each loop
                {
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
                                return (index: ToIndex(songRoot, chordType), value: loopProbability * x.weight);
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

    private (float average, float max, float sum) CalculateDelta(IReadOnlyDictionary<string, (float[] probabilities, bool stable)> previousValues, IReadOnlyDictionary<string, (float[] probabilities, bool stable)> values)
    {
        var all = previousValues
            .SelectMany(p => p.Value.probabilities.Zip(values[p.Key].probabilities).Select(p => Math.Abs(p.First - p.Second)))
            .ToList();

        return (all.Average(), all.Max(), all.Sum());
    }

    private static int GetWeight(int occurrences, int successions) => occurrences + successions * 2;

    private static (byte root, ChordType chordType) FromIndex(int index) => ((byte)(index / 2), (ChordType)(index % 2));
}