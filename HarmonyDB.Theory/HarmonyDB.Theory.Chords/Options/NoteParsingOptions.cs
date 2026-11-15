using HarmonyDB.Theory.Chords.Models.Enums;

namespace HarmonyDB.Theory.Chords.Options;

public class NoteParsingOptions
{
    public static readonly NoteParsingOptions Default = new();

    public static readonly NoteParsingOptions MostForgiving = new()
    {
        CaseSensitive = false,
        ForgiveCyrillicLetters = true,
    };

    /// <summary>
    /// When true, note letters are upper case only, and the solfège names are written either in capitals or capitalized (LA, La).
    /// </summary>
    public bool CaseSensitive { get; set; } = true;

    public HHandling HHandling { get; set; } = HHandling.BMeansH;

    /// <summary>
    /// Read the Cyrillic letters that look like Latin ones as those: А, В, С, Е, Н in the notes, м, о in the chord type ("Ам", "Н7", "Вb").
    /// </summary>
    public bool ForgiveCyrillicLetters { get; set; }

    /// <summary>
    /// Read the solfège names (Do, Re, Mi, Fa, Sol, La, Si) as notes.
    /// They collide with the letters: "Do" may also be D with the diminished symbol "o", and "DO" — D with "O".
    /// When read, the solfège reading wins; a letter reading is tried only when the solfège one leaves an unreadable rest ("Fadd9").
    /// <see cref="NotationUse.Detect"/>: read in the songs that use the other solfège names (Re, Mi, Fa, Sol, La, Si).
    /// </summary>
    public NotationUse Solfege { get; set; } = NotationUse.Always;

    /// <summary>
    /// Read the German note names: the sharps and flats as suffixes (Fis, Cis, Es, As, Des, Bes), the lower case letters as minor chords (fis, a, h).
    /// <see cref="NotationUse.Detect"/>: read in the songs that use German names that are not English words (Fis, Cis, Gis, Dis, Des, Ges, fis, h…).
    /// </summary>
    public NotationUse GermanNoteNames { get; set; } = NotationUse.Never;

    internal NoteParsingOptions Clone() => (NoteParsingOptions)MemberwiseClone();
}
