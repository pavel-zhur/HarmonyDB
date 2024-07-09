using HarmonyDB.Common.Representations.OneShelf;
using Microsoft.Extensions.Logging;
using HarmonyDB.Index.Analysis.Em.Models;
using HarmonyDB.Index.Analysis.Models.Em;
using HarmonyDB.Index.Analysis.Models.Index;

namespace HarmonyDB.Index.Analysis.Services;

public class TonalitiesBalancer(ILogger<TonalitiesBalancer> logger)
{
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
        IReadOnlyList<Link> all, 
        Dictionary<string, (byte songRoot, Scale scale)> songsKeys)
    {
        var loops = all
            .GroupBy(x => x.Normalized)
            .ToDictionary(
                x => x.Key,
                x => new Loop
                {
                    Id = x.Key,
                    Length = Models.Loop.Deserialize(x.Key).Length,
                });

        var songs = all
            .GroupBy(x => x.ExternalId)
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
                Loop = loops[x.Normalized],
                Song = songs[x.ExternalId],
                Shift = x.NormalizationRoot,
                Occurrences = x.Occurrences,
                Successions = x.Successions,
            })
            .ToList();

        return new(songs.Values, loops.Values, links);
    }
}