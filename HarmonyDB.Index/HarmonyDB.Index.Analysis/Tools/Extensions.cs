using HarmonyDB.Index.Analysis.Models;

namespace HarmonyDB.Index.Analysis.Tools;

public static class Extensions
{
    public static string ChordTypeToString(this ChordType chordType, ChordTypePresentation presentation = ChordTypePresentation.Default) =>
        chordType switch
        {
            ChordType.Major => presentation == ChordTypePresentation.MajorAsM ? "M" : string.Empty,
            ChordType.Minor => presentation != ChordTypePresentation.Degrees ? "m" : string.Empty,
            ChordType.Power => "5",
            ChordType.Sus2 => "sus2",
            ChordType.Sus4 => "sus4",
            ChordType.Diminished => "dim",
            ChordType.Augmented => "aug",
            ChordType.Unknown => "?",
            _ => throw new ArgumentOutOfRangeException(nameof(chordType), chordType, "Unexpected chord type."),
        };
}