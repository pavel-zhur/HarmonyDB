using HarmonyDB.Index.Analysis.Models;

namespace HarmonyDB.Index.Analysis.Tools;

public static class Extensions
{
    public static string ChordTypeToString(this ChordType chordType, bool majorAsM = false) =>
        chordType switch
        {
            ChordType.Major => majorAsM ? "M" : "",
            ChordType.Minor => "m",
            ChordType.Power => "5",
            ChordType.Sus2 => "sus2",
            ChordType.Sus4 => "sus4",
            ChordType.Diminished => "dim",
            ChordType.Augmented => "aug",
            ChordType.Unknown => "?",
            _ => throw new ArgumentOutOfRangeException(nameof(chordType), chordType, "Unexpected chord type."),
        };
}