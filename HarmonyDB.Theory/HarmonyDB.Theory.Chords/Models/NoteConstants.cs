using HarmonyDB.Theory.Chords.Models.Enums;

namespace HarmonyDB.Theory.Chords.Models;

public static class NoteConstants
{
    public const byte MinNoteValue = 0;
    public const byte MaxNoteValue = 11;

    public static readonly Note MinNote = new(MinNoteValue);
    public static readonly Note MaxNote = new(MaxNoteValue);

    public static readonly Note GermanBNote = new(1);

    public const byte TotalNotes = 12;

    public const char MinNaturalNoteChar = 'A';
    public const char MaxNaturalNoteChar = 'G';

    public const char HSynonymChar = 'H';
    public const NaturalNote HSynonym = NaturalNote.B;

    public const char FlatSymbol = 'b';
    public const char SharpSymbol = '#';

    public const char FlatUnicodeSymbol = '♭';
    public const char SharpUnicodeSymbol = '♯';

    public const string NoChord = "N.C.";

    public static readonly IReadOnlyList<string> NoChordVariants =
    [
        NoChord,
        "N.C",
        "NC",
        "N.С.", // russian C
    ];

    public static readonly IReadOnlyList<char> FlatSymbols = new List<char> { FlatSymbol, FlatUnicodeSymbol };
    public static readonly IReadOnlyList<char> SharpSymbols = new List<char> { SharpSymbol, SharpUnicodeSymbol };

    public static IReadOnlyList<char> NaturalNoteChars =
    [
        'A', 'B', 'C', 'D', 'E', 'F', 'G',
    ];

    public static IReadOnlyList<string> NaturalNoteSolfegeNames =
    [
        "LA", "SI", "DO", "RE", "MI", "FA", "SOL",
    ];

    public static IReadOnlyList<NaturalNote> NaturalNotes =
    [
        NaturalNote.A,
        NaturalNote.B,
        NaturalNote.C,
        NaturalNote.D,
        NaturalNote.E,
        NaturalNote.F,
        NaturalNote.G,
    ];
}