using HarmonyDB.Common.Representations.OneShelf;
using HarmonyDB.Index.Analysis.Models.CompactV1;
using Microsoft.Extensions.Logging;
using HarmonyDB.Index.Analysis.Em.Models;
using HarmonyDB.Index.Analysis.Models.Em;

namespace HarmonyDB.Index.Analysis.Services;

public class TonalitiesBalancer(ILogger<TonalitiesBalancer> logger, IndexExtractor indexExtractor)
{
    public async Task<List<(string normalized, string externalId, byte normalizationRoot, short occurrences, short successions, int loopLength)>> GetAllSongsLoops(
        IReadOnlyDictionary<string, CompactChordsProgression> progressions)
    {
        var cc = 0;
        var cf = 0;
        List<(string normalized, string externalId, byte normalizationRoot, short occurrences, short successions, int loopLength)> result = new();

        await Parallel.ForEachAsync(progressions, (x, __) =>
        {
            var (externalId, compactChordsProgression) = x;
            try
            {
                var loopResults = new Dictionary<(string normalized, byte normalizationRoot), (short occurrences, short successions, int loopLength)>();
                foreach (var extendedHarmonyMovementsSequence in compactChordsProgression.ExtendedHarmonyMovementsSequences)
                {
                    var loops = indexExtractor.FindSimpleLoops(extendedHarmonyMovementsSequence.Movements, extendedHarmonyMovementsSequence.FirstRoot);

                    foreach (var loop in loops)
                    {
                        var key = (loop.Normalized, loop.NormalizationRoot);
                        var counters = loopResults.GetValueOrDefault(key);
                        loopResults[key] = ((short occurrences, short successions, int loopLength))(
                            counters.occurrences + (loop.EndIndex - loop.StartIndex + 1) / loop.LoopLength,
                            counters.successions + (loop.EndIndex - loop.StartIndex + 1) / loop.LoopLength - 1,
                            loop.LoopLength);
                    }

                    if (cc++ % 1000 == 0) logger.LogInformation($"{cc} s, {cf} f");
                }

                var items = loopResults
                    .Select(p => (p.Key.normalized, externalId, p.Key.normalizationRoot, p.Value.occurrences, p.Value.successions, p.Value.loopLength))
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

    public (byte songRoot, Scale scale)? TryParseBestTonality(string tonality)
    {
        if (!Note.CharactersToNotes.TryGetValue(tonality[0], out var note))
            return null;

        tonality = tonality.Substring(1);
        Scale scale;

        switch (tonality)
        {
            case "#":
                note = note.Sharp();
                scale = Scale.Major;
                break;
            case "b":
                note = note.Flat();
                scale = Scale.Major;
                break;
            case "#m":
                note = note.Sharp();
                scale = Scale.Minor;
                break;
            case "bm":
                note = note.Flat();
                scale = Scale.Minor;
                break;
            case "":
                scale = Scale.Major;
                break;
            case "m":
                scale = Scale.Minor;
                break;
            default:
                return null;
        }

        return (note.Value, scale);
    }

    public EmModel GetEmModel(
        List<(string normalized, string externalId, byte normalizationRoot, short occurrences, short successions, int loopLength)> all, 
        Dictionary<string, (byte songRoot, Scale scale)> songsKeys)
    {
        var loops = all
            .GroupBy(x => x.normalized)
            .ToDictionary(
                x => x.Key,
                x => new Loop
                {
                    Id = x.Key,
                    Length = x.First().loopLength,
                });

        var songs = all
            .GroupBy(x => x.externalId)
            .ToDictionary(
                x => x.Key,
                x => new Song
                {
                    Id = x.Key,
                    IsTonalityKnown = songsKeys.ContainsKey(x.Key),
                    KnownTonality = songsKeys.GetValueOrDefault(x.Key),
                });

        var links = all
            .Select(x => new LoopLink
            {
                Loop = loops[x.normalized],
                Song = songs[x.externalId],
                Shift = x.normalizationRoot,
                Occurrences = x.occurrences,
                Successions = x.successions,
            })
            .ToList();

        return new(songs.Values, loops.Values, links);
    }
}