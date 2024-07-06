using HarmonyDB.Common.Representations.OneShelf;
using HarmonyDB.Index.Analysis.Models.CompactV1;
using HarmonyDB.Index.Analysis.Models;
using Microsoft.Extensions.Logging;
using OneShelf.Common;
using System;
using System.Collections.Concurrent;

namespace HarmonyDB.Index.Analysis.Services;

public class TonalitiesBalancer(ILogger<TonalitiesBalancer> logger, IndexExtractor indexExtractor)
{
    public const int ProbabilitiesLength = Note.Modulus * 2;

    public async Task<(Dictionary<string, (IReadOnlyList<float> probabilities, bool despiteStable)> songsKeys, Dictionary<string, float[]> loopsKeys)> Balance(
        List<(string normalized, string externalId, byte normalizationRoot, short occurrences, short successions)> all,
        Dictionary<string, (float[] probabilities, bool stable, ScoreHolder score)> songsKeys,
        Dictionary<string, (float[] probabilities, bool stable, ScoreHolder score)> loopsKeys)
    {
        List<(string normalized, List<(string externalId, List<(byte normalizationRoot, int weight, int inverseCount)> data)> songs)> loopSongs = ExtractNestedGroups(
            all,
            x => x.normalized,
            x => x.externalId);

        List<(string externalId, List<(string normalized, List<(byte normalizationRoot, int weight, int inverseCount)> data)> loops)> songLoops = ExtractNestedGroups(
            all,
            x => x.externalId,
            x => x.normalized,
            loopSongs.ToDictionary(x => x.normalized, x => x.songs.Count));

        var successfulInARow = 0;
        var iteration = 0;
        while (true)
        {
            var started = DateTime.Now;

            Calculate(songsKeys, loopSongs, loopsKeys, out var loopsDelta);
            Calculate(loopsKeys, songLoops, songsKeys, out var songsDelta);

            var calculation = (DateTime.Now - started).TotalSeconds;

            if (loopsDelta < 0.01 && songsDelta < 0.01)
            {
                successfulInARow++;
            }
            else
            {
                successfulInARow = 0;
            }

            if (successfulInARow > 5) break;

            logger.LogInformation($"iteration {++iteration}: songs {songsDelta}, loops {loopsDelta}, calc {calculation:F} s");
        }

        var stableSongsKeys = songsKeys.ToDictionary(x => x.Key, x => (probabilities: CreateNewProbabilities(false), false, new ScoreHolder()));
        Calculate(loopsKeys, songLoops, stableSongsKeys, out _);

        return (
            songsKeys.ToDictionary(x => x.Key, x => x.Value.stable
                ? (probabilities: stableSongsKeys[x.Key].probabilities.AsIReadOnlyList(), despiteStable: true)
                : (probabilities: x.Value.probabilities.AsIReadOnlyList(), despiteStable: false)),
            loopsKeys.ToDictionary(x => x.Key, x => x.Value.probabilities));
    }

    private static List<(string outerKey, List<(string innerKey, List<(byte normalizationRoot, int weight, int inverseCount)> data)> loops)> ExtractNestedGroups(
        List<(string normalized, string externalId, byte normalizationRoot, short occurrences, short successions)> all,
        Func<(string normalized, string externalId), string> outer,
        Func<(string normalized, string externalId), string> inner,
        Dictionary<string, int>? inverseCounters = null)
    {
        return all
            .GroupBy(x => outer((x.normalized, x.externalId)))
            .Select(g => (
                g.Key,
                g
                    .GroupBy(x => inner((x.normalized, x.externalId)))
                    .Select(g => (
                        g.Key,
                        g.Select(x => (x.normalizationRoot, GetWeight(x.occurrences, x.successions), inverseCounters?[inner((x.normalized, x.externalId))] ?? 1)).ToList()))
                    .ToList()))
            .ToList();
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

    public (byte root, ChordType songMode) FromIndex(int index) => ((byte)(index / 2), (ChordType)(index % 2));

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
        IReadOnlyDictionary<string, (float[] probabilities, bool stable, ScoreHolder score)> innerKeys,
        List<(string outerKey, List<(string innerKey, List<(byte normalizationRoot, int weight, int inverseCounter)> data)> innerData)> nestedGroups, 
        IReadOnlyDictionary<string, (float[] probabilities, bool stable, ScoreHolder score)> outputOuterKeys,
        out float maxDelta)
    {
        var deltas = new ConcurrentBag<float>();

        Parallel.ForEach(nestedGroups, x =>
        {
            if (outputOuterKeys[x.outerKey].stable) return;

            var values = new float[ProbabilitiesLength];
            foreach (var (innerKeyId, relation) in x.innerData)
            {
                var innerKey = innerKeys[innerKeyId]; // what keys the inner object is in (probabilities)
                for (var i = 0; i < ProbabilitiesLength; i++)
                {
                    var (innerRoot, songMode) = FromIndex(i);
                    var probability = innerKey.probabilities[i];
                    foreach (var (normalizationRoot, weight, inverseCounter) in relation)
                    {
                        var outerRoot = Note.Normalize(normalizationRoot - innerRoot);
                        values[ToIndex(outerRoot, songMode)] += (float)(probability * weight * Math.Log(inverseCounter) * innerKey.score.Score);
                    }
                }
            }

            var sum = values.Sum();

            var probabilities = outputOuterKeys[x.outerKey].probabilities;
            if (sum <= float.Epsilon)
            {
                for (var i = 0; i < ProbabilitiesLength; i++)
                {
                    var oldValue = probabilities[i];
                    var newValue = 1f / ProbabilitiesLength;
                    probabilities[i] = newValue;
                    deltas.Add(Math.Abs(oldValue - newValue));
                }
            }
            else
            {
                probabilities.Initialize();
                for (var i = 0; i < ProbabilitiesLength; i++)
                {
                    var oldValue = probabilities[i];
                    var newValue = values[i] / sum;
                    probabilities[i] = newValue;
                    deltas.Add(Math.Abs(oldValue - newValue));
                }
            }
        });

        maxDelta = deltas.Max();
    }

    private static int GetWeight(short occurrences, short successions) => occurrences + successions * 2;

    public class ScoreHolder
    {
        public float Score { get; set; } = 1;
    }
}