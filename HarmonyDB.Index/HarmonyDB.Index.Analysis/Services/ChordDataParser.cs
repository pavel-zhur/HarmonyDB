using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using HarmonyDB.Common;
using HarmonyDB.Common.Representations.OneShelf;
using HarmonyDB.Index.Analysis.Models;
using HarmonyDB.Index.Analysis.Models.V1;
using HarmonyDB.Index.Analysis.Tools;
using Microsoft.Extensions.Logging;
using OneShelf.Common;

namespace HarmonyDB.Index.Analysis.Services;

public class ChordDataParser
{
    private const int Range = 24;
    private const int Modulus = 12;
    private const string NotePattern = "\\[\\!(\\d+)([b#])?\\!\\]";

    private readonly ConcurrentDictionary<string, (byte[] chord, byte root, byte? bass, ChordType chordTypeForCircleOfFifths, ChordType chordTypeForProgressions, string?, NoteAlteration? rootAlteration, NoteAlteration? bassAlteration)?> _chordDataCache = new();

    private readonly ILogger<ChordDataParser> _logger;

    public ChordDataParser(ILogger<ChordDataParser> logger)
    {
        _logger = logger;
    }

    public (int[] bass, int[] main, int root, string? fingering)? GetNotes(string chordData)
    {
        var parsed = GetChord(chordData);
        if (parsed == null)
        {
            return null;
        }

        var (chord, root, maybeBass, _, _, fingering, _, _) = parsed.Value;

        var bass = maybeBass ?? root;

        var main = chord
            .Select(x => x + root)
            .Concat(chord.Select(x => x + root - Modulus))
            .Concat(chord.Select(x => x + root + Modulus))
            .Distinct()
            .Where(x => x > bass)
            .Where(x => x < Range)
            .OrderBy(x => x)
            .ToArray();

        return (new[]
        {
            bass - Modulus,
            bass,
        }, main, root, fingering);
    }

    public ChordDataV1 GetProgressionData(string chordData)
    {
        var originalRepresentation = Regex.Replace(
            chordData,
            NotePattern,
            match => new Note(byte.Parse(match.Groups[1].Value), ParseNoteAlteration(match.Groups[2].Value)).Representation(new()));

        var chord = GetChord(chordData);
        if (!chord.HasValue)
        {
            return new()
            {
                HarmonyRepresentation = "?",
                OriginalRepresentation = originalRepresentation,
                ChordData = chordData,
            };
        }

        var (_, root, bass, _, chordType, _, rootAlteration, bassAlteration) = chord.Value;
        return new()
        {
            HarmonyRepresentation = $"{new Note(root, rootAlteration).Representation(new())}{chordType.ChordTypeToString()}{(bass.HasValue ? $"/{new Note(bass.Value, bassAlteration).Representation(new())}" : null)}",
            ChordData = chordData,
            HarmonyData = new()
            {
                Root = root,
                Alteration = rootAlteration,
                ChordType = chordType,
            },
            OriginalRepresentation = originalRepresentation,
        };
    }

    private static NoteAlteration? ParseNoteAlteration(string match)
    {
        return match switch
        {
            "#" => NoteAlteration.Sharp,
            "b" => NoteAlteration.Flat,
            _ => null,
        };
    }

    public (byte[] chord, byte root, byte? bass, ChordType chordTypeForCircleOfFifths, ChordType chordTypeForProgressions, string? fingering, NoteAlteration? rootAlteration, NoteAlteration? bassAlteration)? GetChord(string chordData)
    {
        return _chordDataCache.GetOrAdd(chordData, _ =>
        {
            try
            {
                var bassMatch = Regex.Match(chordData, $"^(.+)\\/{NotePattern}$");
                byte? bass = null;
                NoteAlteration? bassAlteration = null;
                var rootData = chordData;
                if (bassMatch.Success)
                {
                    bass = byte.Parse(bassMatch.Groups[2].Value);
                    bassAlteration = ParseNoteAlteration(bassMatch.Groups[3].Value);
                    rootData = bassMatch.Groups[1].Value;
                }

                var fingeringMatch = Regex.Match(rootData, $"^{NotePattern}(.*)$");
                var rootAlteration = fingeringMatch.Success ? ParseNoteAlteration(fingeringMatch.Groups[2].Value) : null;
                var fingering = fingeringMatch.Success ? fingeringMatch.Groups[3].Value : null;
                var tests = new (string pattern, Func<byte[]> notes, ChordType chordTypeForCircleOfFifths, ChordType chordTypeForProgressions)[]
                {
                    ($"^{NotePattern}$", () => [0, 4, 7], ChordType.Major, ChordType.Major),
                    ($"^{NotePattern}\\d*add\\d+$", () => [0, 4, 7], ChordType.Major, ChordType.Major),
                    ($"^{NotePattern}m$", () => [0, 3, 7], ChordType.Minor, ChordType.Minor),
                    ($"^{NotePattern}m\\d*add\\d+$", () => [0, 3, 7], ChordType.Minor, ChordType.Minor),

                    ($"^{NotePattern}5$", () => [0, 7], ChordType.Unknown, ChordType.Power),

                    ($"^{NotePattern}[7913]+$", () => [0, 4, 7, 10], ChordType.Major, ChordType.Major),
                    ($"^{NotePattern}m[7913]+$", () => [0, 3, 7, 10], ChordType.Minor, ChordType.Minor),

                    ($"^{NotePattern}\\d*sus2$", () => [0, 2, 7], ChordType.Unknown, ChordType.Sus2),
                    ($"^{NotePattern}m\\d*sus2$", () => [0, 2, 7], ChordType.Minor, ChordType.Minor),
                    ($"^{NotePattern}\\d*sus4?$", () => [0, 5, 7], ChordType.Unknown, ChordType.Sus4),
                    ($"^{NotePattern}m\\d*sus4?$", () => [0, 5, 7], ChordType.Minor, ChordType.Minor),

                    ($"^{NotePattern}m5$", () => [0, 7], ChordType.Unknown, ChordType.Unknown), // eq 5
                    ($"^{NotePattern}m\\+$", () => [0, 7], ChordType.Unknown, ChordType.Unknown), // eq 5

                    ($"^{NotePattern}maj[7913]+$", () => [0, 4, 7, 11], ChordType.Major, ChordType.Major),
                    ($"^{NotePattern}\\+[7913]+$", () => [0, 4, 7, 11], ChordType.Major, ChordType.Unknown),
                    ($"^{NotePattern}[7913]+\\+$", () => [0, 4, 7, 11], ChordType.Major, ChordType.Major),
                    ($"^{NotePattern}maj$", () => [0, 4, 7, 11], ChordType.Major, ChordType.Major),
                    ($"^{NotePattern}maj", () => [0, 4, 7, 11], ChordType.Major, ChordType.Unknown), // all starting with (to avoid mixing with m)
                    ($"^{NotePattern}mmaj[7913]+$", () => [0, 3, 7, 11], ChordType.Minor, ChordType.Minor),
                    ($"^{NotePattern}m\\+[7913]+$", () => [0, 3, 7, 11], ChordType.Minor, ChordType.Minor),
                    ($"^{NotePattern}m[7913]+\\+$", () => [0, 3, 7, 11], ChordType.Minor, ChordType.Minor),
                    ($"^{NotePattern}mmaj$", () => [0, 3, 7, 11], ChordType.Minor, ChordType.Minor),
                    ($"^{NotePattern}mmaj", () => [0, 3, 7, 11], ChordType.Minor, ChordType.Minor), // all starting with (to avoid mixing with m)

                    ($"^{NotePattern}m6$", () => [0, 3, 7, 9], ChordType.Minor, ChordType.Minor),
                    ($"^{NotePattern}6$", () => [0, 4, 7, 9], ChordType.Major, ChordType.Major),

                    ($"^{NotePattern}9$", () => [0, 4, 7, 10], ChordType.Major, ChordType.Major), // will play 7
                    ($"^{NotePattern}m9$", () => [0, 3, 7, 10], ChordType.Minor, ChordType.Minor), // will play 7

                    ($"^{NotePattern}[7913]+sus4$", () => [0, 5, 7, 10], ChordType.Unknown, ChordType.Sus4),
                    ($"^{NotePattern}[7913]+sus$", () => [0, 5, 7, 10], ChordType.Unknown, ChordType.Sus4), // sus4
                    ($"^{NotePattern}[7913]+sus2$", () => [0, 2, 7, 10], ChordType.Unknown, ChordType.Sus2),

                    ($"^{NotePattern}dim[7913]+$", () => [0, 3, 6, 10], ChordType.Minor, ChordType.Diminished),
                    ($"^{NotePattern}m[7913]+5-$", () => [0, 3, 6, 10], ChordType.Minor, ChordType.Diminished),
                    ($"^{NotePattern}m[7913]+-5$", () => [0, 3, 6, 10], ChordType.Minor, ChordType.Diminished),
                    ($"^{NotePattern}dim$", () => [0, 3, 6], ChordType.Minor, ChordType.Diminished),
                    ($"^{NotePattern}m-5$", () => [0, 3, 6], ChordType.Minor, ChordType.Diminished),
                    ($"^{NotePattern}m5-$", () => [0, 3, 6], ChordType.Minor, ChordType.Diminished),

                    ($"^{NotePattern}\\+$", () => [0, 4, 8], ChordType.Major, ChordType.Unknown),
                    ($"^{NotePattern}aug$", () => [0, 4, 8], ChordType.Major, ChordType.Augmented),
                    ($"^{NotePattern}[7913]+\\#5$", () => [0, 4, 8], ChordType.Major, ChordType.Augmented),
                    ($"^{NotePattern}[7913]+?5+$", () => [0, 4, 8], ChordType.Major, ChordType.Augmented),
                    ($"^{NotePattern}\\#5$", () => [0, 4, 8], ChordType.Major, ChordType.Augmented),
                    ($"^{NotePattern}.*5\\+$", () => [0, 4, 8], ChordType.Unknown, ChordType.Unknown),

                    ($"^{NotePattern}m", () => [0, 3, 7], ChordType.Minor, ChordType.Unknown),
                    ($"^{NotePattern}", () => [0, 4, 7], ChordType.Major, ChordType.Unknown),
                };

                var match = tests
                    .Select(x => (match: Regex.Match(rootData, x.pattern), x.notes, x.chordTypeForCircleOfFifths, x.chordTypeForProgressions))
                    .FirstOrDefault(x => x.match.Success);

                if (match.match == null)
                {
                    _logger.LogWarning("Chord parsing failed: {}.", chordData);
                    return null;
                }

                return (match.notes(), byte.Parse(match.match.Groups[1].Value), bass, match.chordTypeForCircleOfFifths, match.chordTypeForProgressions, fingering, rootAlteration, bassAlteration);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error parsing the chord data.");
                return null;
            }
        });
    }
}