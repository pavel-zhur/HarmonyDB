using System.Text.Json;
using System.Text.Json.Serialization;
using HarmonyDB.Common.Representations.OneShelf;
using HarmonyDB.Index.Analysis.Models.CompactV1;
using HarmonyDB.Index.Analysis.Models;
using Microsoft.Extensions.Logging;
using OneShelf.Common;

namespace HarmonyDB.Index.Analysis.Services;

public class TonalitiesBalancer(ILogger<TonalitiesBalancer> logger, IndexExtractor indexExtractor)
{
    public const int ProbabilitiesLength = Note.Modulus * 2;

    public async Task<(Dictionary<string, (IReadOnlyList<float> probabilities, bool despiteStable)> songsKeys, Dictionary<string, float[]> loopsKeys)> Balance(
        List<(string normalized, string externalId, byte normalizationRoot, short occurrences, short successions)> all,
        Dictionary<string, (float[] probabilities, bool stable)> songsKeys,
        Dictionary<string, (float[] probabilities, bool stable)> loopsKeys)
    {
        var previousSongsKeys = Clone(songsKeys);
        var previousLoopsKeys = Clone(loopsKeys);

        List<(string externalId, List<(string normalized, List<(byte normalizationRoot, int weight)> data)> loops)> songLoops = ExtractNestedGroups(
            all,
            x => x.externalId,
            x => x.normalized);

        List<(string normalized, List<(string externalId, List<(byte normalizationRoot, int weight)> data)> songs)> loopSongs = ExtractNestedGroups(
            all,
            x => x.normalized,
            x => x.externalId);

        var successfulInARow = 0;
        var iteration = 0;
        var timestamp = DateTime.Now.Ticks;
        while (true)
        {
            var started = DateTime.Now;

            Calculate(previousLoopsKeys, songLoops, songsKeys);
            Calculate(previousSongsKeys, loopSongs, loopsKeys);

            var calculation = (DateTime.Now - started).TotalSeconds;
            started = DateTime.Now;

            var songsDelta = CalculateDelta(previousSongsKeys, songsKeys);
            var loopsDelta = CalculateDelta(previousLoopsKeys, loopsKeys);

            var deltas = (DateTime.Now - started).TotalSeconds;
            started = DateTime.Now;

            if (songsDelta.max < 0.01 && loopsDelta.max < 0.01)
            {
                successfulInARow++;
            }
            else
            {
                successfulInARow = 0;
            }

            await Save(timestamp, iteration, songsKeys, loopsKeys);

            var saving = (DateTime.Now - started).TotalSeconds;

            if (successfulInARow > 3) break;

            logger.LogInformation($"iteration {++iteration}: songs {songsDelta}, loops {loopsDelta}, calc {calculation:F} s, deltas {deltas:F} s, saving {saving:F} s");

            (loopsKeys, previousLoopsKeys) = (previousLoopsKeys, loopsKeys);
            (songsKeys, previousSongsKeys) = (previousSongsKeys, songsKeys);
        }

        var stableSongsKeys = songsKeys.ToDictionary(x => x.Key, x => (probabilities: CreateNewProbabilities(false), false));
        Calculate(loopsKeys, songLoops, stableSongsKeys);

        return (
            songsKeys.ToDictionary(x => x.Key, x => x.Value.stable
                ? (probabilities: stableSongsKeys[x.Key].probabilities.AsIReadOnlyList(), despiteStable: true)
                : (probabilities: x.Value.probabilities.AsIReadOnlyList(), despiteStable: false)),
            loopsKeys.ToDictionary(x => x.Key, x => x.Value.probabilities));
    }

    private async Task Save(
        long timestamp, 
        int iteration,
        Dictionary<string, (float[] probabilities, bool stable)> songsKeys,
        Dictionary<string, (float[] probabilities, bool stable)> loopsKeys)
    {
        await File.WriteAllTextAsync($"iteration.{timestamp}.{iteration}.json", JsonSerializer.Serialize(new
        {
            songsKeys = songsKeys.Select(p => new
            {
                p.Key,
                p.Value.probabilities,
            }),
            loopsKeys = loopsKeys.Select(p => new
            {
                p.Key,
                p.Value.probabilities,
            }),
        }));
    }

    private static List<(string outerKey, List<(string innerKey, List<(byte normalizationRoot, int weight)> data)> loops)> ExtractNestedGroups(
        List<(string normalized, string externalId, byte normalizationRoot, short occurrences, short successions)> all,
        Func<(string normalized, string externalId), string> outer,
        Func<(string normalized, string externalId), string> inner)
    {
        return all
            .GroupBy(x => outer((x.normalized, x.externalId)))
            .Select(g => (
                g.Key,
                g
                    .GroupBy(x => inner((x.normalized, x.externalId)))
                    .Select(g => (
                        g.Key,
                        g.Select(x => (x.normalizationRoot, GetWeight(x.occurrences, x.successions))).ToList()))
                    .ToList()))
            .ToList();
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
                var loopResults = new Dictionary<(string normalized, byte loopRoot, ChordType mode), (short occurrences, short successions)>();
                foreach (var extendedHarmonyMovementsSequence in compactChordsProgression.ExtendedHarmonyMovementsSequences)
                {
                    var loops = indexExtractor.FindSimpleLoops(extendedHarmonyMovementsSequence.Movements, extendedHarmonyMovementsSequence.FirstRoot);

                    foreach (var loop in loops)
                    {
                        var key = (loop.Normalized, Note.Normalize(loop.NormalizationRoot - songRoot), mode);
                        var counters = loopResults.GetValueOrDefault(key);
                        loopResults[key] = ((short occurrences, short successions))(
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

    public async Task<List<(string normalized, string externalId, byte normalizationRoot, short occurrences, short successions)>> GetAllSongsLoops(
        IReadOnlyDictionary<string, CompactChordsProgression> progressions)
    {
        var cc = 0;
        var cf = 0;
        List<(string normalized, string externalId, byte normalizationRoot, short occurrences, short successions)> result = new();

        await Parallel.ForEachAsync(progressions, (x, __) =>
        {
            var (externalId, compactChordsProgression) = x;
            try
            {
                var loopResults = new Dictionary<(string normalized, byte normalizationRoot), (short occurrences, short successions)>();
                foreach (var extendedHarmonyMovementsSequence in compactChordsProgression.ExtendedHarmonyMovementsSequences)
                {
                    var loops = indexExtractor.FindSimpleLoops(extendedHarmonyMovementsSequence.Movements, extendedHarmonyMovementsSequence.FirstRoot);

                    foreach (var loop in loops)
                    {
                        var key = (loop.Normalized, loop.NormalizationRoot);
                        var counters = loopResults.GetValueOrDefault(key);
                        loopResults[key] = ((short occurrences, short successions))(
                            counters.occurrences + (loop.EndIndex - loop.StartIndex + 1) / loop.LoopLength,
                            counters.successions + (loop.EndIndex - loop.StartIndex + 1) / loop.LoopLength - 1);
                    }

                    if (cc++ % 1000 == 0) logger.LogInformation($"{cc} s, {cf} f");
                }

                var items = loopResults
                    .Select(p => (p.Key.normalized, externalId, p.Key.normalizationRoot, p.Value.occurrences, p.Value.successions))
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

    public int ToIndex(byte root, ChordType songMode) => root * 2 + (int)songMode;

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

    private void Calculate(
        IReadOnlyDictionary<string, (float[] probabilities, bool stable)> innerKeys,
        List<(string outerKey, List<(string innerKey, List<(byte normalizationRoot, int weight)> data)> songs)> nestedGroups, 
        IReadOnlyDictionary<string, (float[] probabilities, bool stable)> outputOuterKeys)
    {
        Parallel.ForEach(nestedGroups, x =>
        {
            // for each outer object
            if (outputOuterKeys[x.outerKey].stable) return;
            var values = x.songs
                .SelectMany(x => // for each inner object
                {
                    var innerKey = innerKeys[x.innerKey]; // what keys the inner object is in (probabilities)
                    var relation = x.data; // relation, with weights and normalizationRoot

                    // Normalize(normalizationRoot - songRoot) == loopRoot
                    // Normalize(normalizationRoot - loopRoot) == songRoot
                    // Normalize(loopRoot + songRoot) == normalizationRoot

                    // Normalize(normalizationRoot - innerKey) == outerKey
                    // Normalize(normalizationRoot - outerKey) == innerKey
                    // Normalize(outerKey + innerKey) == normalizationRoot

                    return innerKey
                        .probabilities
                        .WithIndices()
                        .SelectMany(x =>
                        {
                            var (innerRoot, songMode) = FromIndex(x.i);
                            var probability = x.x;
                            return relation.Select(x =>
                            {
                                var outerRoot = Note.Normalize(x.normalizationRoot - innerRoot);
                                return (index: ToIndex(outerRoot, songMode), value: probability * x.weight);
                            }).ToList();
                        });
                })
                .GroupBy(x => x.index)
                .Select(g => (index: g.Key, value: g.Sum(x => x.value)))
                .ToList();

            var sum = values.Sum(x => x.value);

            var probabilities = outputOuterKeys[x.outerKey].probabilities;
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

    private static int GetWeight(short occurrences, short successions) => occurrences + successions * 2;

    private static (byte root, ChordType songMode) FromIndex(int index) => ((byte)(index / 2), (ChordType)(index % 2));
}