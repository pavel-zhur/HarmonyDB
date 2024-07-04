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
        var progressions = await _progressionsCache.Get();
        var indexHeaders = await _indexHeadersCache.Get();

        var songsKeys = GetSongsKeys(indexHeaders);
        var known = await GetKnownSongsLoopsKeys(progressions, songsKeys);
        var all = await GetAllSongsLoops(progressions);

        var allExternalIds = all.Select(x => x.externalId).ToHashSet();

        var initialSongsKeys = songsKeys.Where(x => allExternalIds.Contains(x.Key)).ToDictionary(x => x.Key, x =>
        {
            var result = CreateNewProbabilities(true);
            result[ToIndex(x.Value.songRoot, x.Value.mode)] = 1;
            return result;
        });

        initialSongsKeys.AddRange(allExternalIds
            .Where(x => !initialSongsKeys.ContainsKey(x))
            .Select(x => (p: x, CreateNewProbabilities(false))),
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

                        return result;
                    }));

        initialLoopsKeys.AddRange(all
            .Select(x => x.normalized)
            .Where(x => !initialLoopsKeys.ContainsKey(x))
            .Select(x => (x, CreateNewProbabilities(false))), 
            false);

        Balance(all, initialSongsKeys, initialLoopsKeys);
    }

    private void Balance(
        List<(string normalized, string externalId, byte normalizationRoot, int weight)> all, 
        Dictionary<string, float[]> songsKeys,
        Dictionary<string, float[]> loopsKeys)
    {
        var previousSongsKeys = songsKeys.ToDictionary(x => x.Key, x => x.Value.ToArray());
        var previousLoopsKeys = loopsKeys.ToDictionary(x => x.Key, x => x.Value.ToArray());

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

        while (true)
        {
            songsKeys = CalculateSongsKeys(previousLoopsKeys, songLoops);
            loopsKeys = CalculateLoopsKeys(previousSongsKeys, loopSongs);

            var songsDelta = CalculateDelta(previousSongsKeys, songsKeys);
            var loopsDelta = CalculateDelta(previousLoopsKeys, loopsKeys);

            _logger.LogInformation($"delta: songs {songsDelta}, loops {loopsDelta}");

            previousLoopsKeys = loopsKeys;
            previousSongsKeys = songsKeys;
        }
    }

    private Dictionary<string, float[]> CalculateLoopsKeys(
        Dictionary<string, float[]> songsKeys,
        List<(string normalized, List<(string externalId, List<(byte normalizationRoot, int weight)> data)> songs)> loopSongs) 
        => loopSongs.ToDictionary(
            x => x.normalized, // for each loop
            x => x.songs
                .SelectMany(x => // for each song
                {
                    var songKeys = songsKeys[x.externalId]; // what keys that song is in (probabilities)
                    var data = x.data; // loop containment in the song, with weights and normalizationRoot

                    // Normalize(normalizationRoot - songRoot) == loopRoot
                    // Normalize(normalizationRoot - loopRoot) == songRoot
                    // Normalize(loopRoot + songRoot) == normalizationRoot

                    return songKeys
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
                .ToList()
                .SelectSingle(values =>
                {
                    var sum = values.Sum(x => x.value);
                    var result = CreateNewProbabilities(true);
                    foreach (var (index, value) in values)
                    {
                        result[index] = value / sum;
                    }

                    return result;
                }));

    private Dictionary<string, float[]> CalculateSongsKeys(
        Dictionary<string, float[]> loopsKeys,
        List<(string externalId, List<(string normalized, List<(byte normalizationRoot, int weight)> data)> loops)> songLoops)
        => songLoops.ToDictionary(
            x => x.externalId, // for each song
            x => x.loops
                .SelectMany(x => // for each loop
                {
                    var loopKeys = loopsKeys[x.normalized]; // what keys that loop is in (probabilities)
                    var data = x.data; // songs containing the loop, with weights and normalizationRoot

                    // Normalize(normalizationRoot - songRoot) == loopRoot
                    // Normalize(normalizationRoot - loopRoot) == songRoot
                    // Normalize(loopRoot + songRoot) == normalizationRoot

                    return loopKeys
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
                .ToList()
                .SelectSingle(values =>
                {
                    var sum = values.Sum(x => x.value);
                    var result = CreateNewProbabilities(true);
                    foreach (var (index, value) in values)
                    {
                        result[index] = value / sum;
                    }

                    return result;
                }));

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

    private (float average, float max, float sum) CalculateDelta(Dictionary<string, float[]> previousValues, Dictionary<string, float[]> values)
    {
        var all = previousValues
            .SelectMany(p => p.Value.Zip(values[p.Key]).Select(p => Math.Abs(p.First - p.Second)))
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