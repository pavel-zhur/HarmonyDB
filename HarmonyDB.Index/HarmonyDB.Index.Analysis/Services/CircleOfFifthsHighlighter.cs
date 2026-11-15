using HarmonyDB.Index.Analysis.Models;
using OneShelf.Common;

namespace HarmonyDB.Index.Analysis.Services;

public class CircleOfFifthsHighlighter
{
    private readonly ChordDataParser _chordDataParser;

    public CircleOfFifthsHighlighter(ChordDataParser chordDataParser)
    {
        _chordDataParser = chordDataParser;
    }

    /// <summary>
    /// What to mark on the circle of fifths for a song's chords (in the chord data form, as <see cref="ChordDataParser.GetChord"/> takes them).
    /// Each chord marks its root with its chord type for the circle of fifths, and its bass, if any, with <see cref="ChordType.Unknown"/>.
    /// A note marked as <see cref="ChordType.Major"/> or <see cref="ChordType.Minor"/> loses its <see cref="ChordType.Unknown"/> mark.
    /// </summary>
    public HashSet<(byte note, ChordType chordType)> GetHighlightedChords(IEnumerable<string> chords)
        => chords
            .Distinct()
            .Select(x => _chordDataParser.GetChord(x))
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .SelectMany(x => (note: x.root, x.chordTypeForCircleOfFifths).Once().SelectSingle(y => !x.bass.HasValue ? y : y.Append((note: x.bass.Value, chordTypeForCircleOfFifths: ChordType.Unknown))))
            .Distinct()
            .GroupBy(x => x.note)
            .SelectMany(g => g.Any(x => x.chordTypeForCircleOfFifths is ChordType.Major or ChordType.Minor) ? g.Where(x => x.chordTypeForCircleOfFifths is not ChordType.Unknown) : g)
            .ToHashSet();
}
