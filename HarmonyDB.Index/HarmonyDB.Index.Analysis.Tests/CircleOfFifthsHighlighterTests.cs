using HarmonyDB.Index.Analysis.Models;
using HarmonyDB.Index.Analysis.Services;

namespace HarmonyDB.Index.Analysis.Tests;

public class CircleOfFifthsHighlighterTests(CircleOfFifthsHighlighter circleOfFifthsHighlighter)
{
    [Fact]
    public void GetHighlightedChords()
    {
        var highlighted = circleOfFifthsHighlighter.GetHighlightedChords(
        [
            "[!0!]m", // Am
            "[!8!]", // F
            "[!3!]/[!10!]", // C/G: G is a bass
            "[!10!]7", // G7: G is a major, so not a bass any more
            "[!3!]/[!7!]", // C/E: E is only a bass
            "[!5!]sus4", // Dsus4: neither major nor minor
            "[!0!]m", // again
            "?", // not a chord
        ]);

        Assert.Equal(
            new HashSet<(byte note, ChordType chordType)>
            {
                (0, ChordType.Minor),
                (8, ChordType.Major),
                (3, ChordType.Major),
                (10, ChordType.Major),
                (7, ChordType.Unknown),
                (5, ChordType.Unknown),
            },
            highlighted);
    }

    [Fact]
    public void Empty()
    {
        Assert.Empty(circleOfFifthsHighlighter.GetHighlightedChords([]));
    }
}
