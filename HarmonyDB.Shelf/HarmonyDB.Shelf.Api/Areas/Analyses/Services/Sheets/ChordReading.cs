using HarmonyDB.Common.Representations.OneShelf;
using HarmonyDB.Theory.Chords.Models;
using HarmonyDB.Theory.Chords.Models.Enums;
using HarmonyDB.Theory.Chords.Options;
using HarmonyDB.Theory.Chords.Parsers;

namespace HarmonyDB.Shelf.Api.Areas.Analyses.Services.Sheets;

/// <summary>
/// Reads the chord names of an analysis, and names their degrees in a key.
/// </summary>
public static class ChordReading
{
    /// <summary>
    /// How an analysis writes its chords: letter names only (no solfège, no German names, B is B), a thirteenth chord with
    /// its eleventh (the full stack of thirds); every other meaning as the parser has it by default.
    /// </summary>
    public static ChordParsingOptions Options { get; } = new()
    {
        NoteParsingOptions = new()
        {
            Solfege = NotationUse.Never,
            GermanNoteNames = NotationUse.Never,
        },
        ChordTypeParsingOptions = new()
        {
            ThirteenthMeaning = ThirteenthMeaning.WithEleventh,
        },
    };

    // The letters from C, and their pitches from C.
    private const string Letters = "CDEFGAB";
    private static readonly int[] LetterPitches = [0, 2, 4, 5, 7, 9, 11];

    // The scales' pitches from the tonic, degree by degree; a minor key's scale is its natural minor.
    private static readonly int[] MajorScale = [0, 2, 4, 5, 7, 9, 11];
    private static readonly int[] MinorScale = [0, 2, 3, 5, 7, 8, 10];

    private static readonly string[] Romans = ["I", "II", "III", "IV", "V", "VI", "VII"];

    /// <summary>
    /// The chord's root and bass as written: their letters and their sharps (positive) or flats (negative).
    /// </summary>
    /// <exception cref="UnreadableChordException">The parser does not read the name as a chord.</exception>
    public static (WrittenNote Root, WrittenNote? Bass) Read(string name)
    {
        var result = ChordParser.Parse(name, Options);
        if (result.Type != ChordParseResultType.Success)
            throw new UnreadableChordException(name);

        var chord = result.Chord!;
        return (Written(chord.RootRepresentation, name), chord.BassRepresentation == null ? null : Written(chord.BassRepresentation, name));
    }

    /// <summary>
    /// The degree of a note in a key: the Roman number of its letter counted from the tonic's letter, after the sharps or
    /// flats by which it stands against the key's scale note on that letter ("V", "bVII", "#IV", "bbIII").
    /// </summary>
    public static string Degree(WrittenNote note, SheetKey key)
    {
        var tonic = new WrittenNote(key.Tonic[0], key.Tonic.Length == 1 ? 0 : key.Tonic[1] == '#' ? key.Tonic.Length - 1 : 1 - key.Tonic.Length);
        var degree = (Letters.IndexOf(note.Letter) - Letters.IndexOf(tonic.Letter) + 7) % 7;
        var expected = tonic.Pitch + (key.IsMinor ? MinorScale : MajorScale)[degree];
        var difference = ((note.Pitch - expected) % 12 + 18) % 12 - 6;
        return new string(difference > 0 ? '#' : 'b', Math.Abs(difference)) + Romans[degree];
    }

    /// <summary>
    /// A degree as the material gives it ("IV", "IV / bVI", "VI/III"): the chord's and its bass's, when it is written as
    /// degrees are (a Roman number after its sharps or flats), one for a chord, two for a chord with a bass; otherwise null.
    /// </summary>
    public static (string? Degree, string? BassDegree)? GivenDegree(string? text, bool hasBass)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var parts = text.Split('/').Select(x => x.Trim()).ToList();
        if (parts.Count != (hasBass ? 2 : 1) || !parts.All(DegreePattern.IsMatch))
            return null;

        return (parts[0], hasBass ? parts[1] : null);
    }

    private static readonly System.Text.RegularExpressions.Regex DegreePattern = new("^(#+|b+)?(I|II|III|IV|V|VI|VII)$");

    private static WrittenNote Written(NoteRepresentation representation, string name)
    {
        var letter = representation.NaturalNoteRepresentation switch
        {
            NaturalNoteRepresentation.A => 'A',
            NaturalNoteRepresentation.B => 'B',
            NaturalNoteRepresentation.C => 'C',
            NaturalNoteRepresentation.D => 'D',
            NaturalNoteRepresentation.E => 'E',
            NaturalNoteRepresentation.F => 'F',
            NaturalNoteRepresentation.G => 'G',
            _ => throw new UnreadableChordException(name),
        };

        return new(letter, representation.Sharps - representation.Flats);
    }

    /// <summary>
    /// A note as written: its letter, and its sharps (positive) or flats (negative).
    /// </summary>
    public sealed record WrittenNote(char Letter, int Accidentals)
    {
        public int Pitch => LetterPitches[Letters.IndexOf(Letter)] + Accidentals;
    }
}

/// <summary>
/// A chord name of an analysis that the chord parser does not read.
/// </summary>
public sealed class UnreadableChordException(string name) : Exception($"The chord '{name}' is not readable.")
{
    public string Name { get; } = name;
}
