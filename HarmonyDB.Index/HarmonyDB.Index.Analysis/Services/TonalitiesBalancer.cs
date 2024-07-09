using HarmonyDB.Common.Representations.OneShelf;
using Microsoft.Extensions.Logging;
using HarmonyDB.Index.Analysis.Em.Models;
using HarmonyDB.Index.Analysis.Models.Em;
using HarmonyDB.Index.Analysis.Models.Index;
using System.Collections.Concurrent;

namespace HarmonyDB.Index.Analysis.Services;

public class TonalitiesBalancer(ILogger<TonalitiesBalancer> logger)
{
    private readonly ConcurrentDictionary<string, (byte songRoot, Scale scale)?> _cache = new();

    public (byte songRoot, Scale scale)? TryParseBestTonality(string tonality)
    {
        if (_cache.TryGetValue(tonality, out var cachedResult))
        {
            return cachedResult;
        }

        if (!Note.CharactersToNotes.TryGetValue(tonality[0], out var note))
        {
            _cache[tonality] = null;
            return null;
        }

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
                _cache[tonality] = null;
                return null;
        }

        var result = (note.Value, scale);
        _cache[tonality] = result;
        return result;
    }

    public (EmModel emModel, IReadOnlyList<LoopLink> loopLinks) GetEmModel(
        IReadOnlyList<StructureLink> all, 
        Dictionary<string, (byte songRoot, Scale scale)> songsKeys)
    {
        var loops = all
            .GroupBy(x => x.Normalized)
            .ToDictionary(
                x => x.Key,
                x => new Loop
                {
                    Id = x.Key,
                    Length = (byte)Models.Loop.Deserialize(x.Key).Length,
                });

        var songs = all
            .GroupBy(x => x.ExternalId)
            .ToDictionary(
                x => x.Key,
                x => new Song
                {
                    Id = x.Key,
                    KnownTonality = songsKeys.TryGetValue(x.Key, out var known) ? known : null,
                });

        var links = all
            .Select(x => new LoopLink
            {
                Loop = loops[x.Normalized],
                Song = songs[x.ExternalId],
                Shift = x.NormalizationRoot,
                Occurrences = x.Occurrences,
                Successions = x.Successions,
            })
            .ToList();

        return (new(songs.Values, loops.Values), links);
    }
}