using HarmonyDB.Theory.Chords.Models.Enums;

namespace HarmonyDB.Theory.Chords.Options;

public class NoteParsingOptions
{
    public static readonly NoteParsingOptions Default = new();

    public static readonly NoteParsingOptions MostForgiving = new()
    {
        CaseSensitive = false,
        ForgiveRussianC = true,
    };

    /// <summary>
    /// When true, note letters are upper case only, and the solfège names are written either in capitals or capitalized (LA, La).
    /// </summary>
    public bool CaseSensitive { get; set; } = true;

    public HHandling HHandling { get; set; } = HHandling.BMeansH;

    public bool ForgiveRussianC { get; set; }

    /// <summary>
    /// Read the solfège names (Do, Re, Mi, Fa, Sol, La, Si) as notes.
    /// They collide with the letters: "Do" may also be D with the diminished symbol "o", and "DO" — D with "O".
    /// When true, the solfège reading wins; a letter reading is tried only when the solfège one leaves an unreadable rest ("Fadd9").
    /// </summary>
    public bool Solfege { get; set; } = true;
}
