using HarmonyDB.Theory.Chords.Models.Enums;

namespace HarmonyDB.Theory.Chords.Options;

public class ChordParsingOptions
{
    public static readonly ChordParsingOptions Default = new();

    public static readonly ChordParsingOptions MostForgiving = new()
    {
        ForgiveSameBass = true,
        ForgiveEdgeWhitespaces = true,
        NoteParsingOptions = NoteParsingOptions.MostForgiving,
        ForgiveRoundBraces = true,
        ChordTypeParsingOptions = ChordTypeParsingOptions.MostForgiving,
    };

    public bool ForgiveSameBass { get; set; }
    public bool ForgiveEdgeWhitespaces { get; set; }
    public NoteParsingOptions NoteParsingOptions { get; set; } = NoteParsingOptions.Default;
    public bool ForgiveRoundBraces { get; set; }
    public ChordTypeParsingOptions ChordTypeParsingOptions { get; set; } = ChordTypeParsingOptions.Default;

    /// <summary>
    /// Whether some meanings are left to be decided from the song (<see cref="NotationUse.Detect"/> and the like):
    /// such options parse only with the song's chords (<see cref="Parsers.ChordParser.ParseSong"/>).
    /// </summary>
    public bool NeedsSong
        => NoteParsingOptions.HHandling == HHandling.Detect
           || NoteParsingOptions.Solfege == NotationUse.Detect
           || NoteParsingOptions.GermanNoteNames == NotationUse.Detect
           || ChordTypeParsingOptions.PlusSeventhMeaning == PlusSeventhMeaning.Detect
           || ChordTypeParsingOptions.BareNinthMeaning == BareNinthMeaning.Detect;

    internal ChordParsingOptions Clone()
    {
        var result = (ChordParsingOptions)MemberwiseClone();
        result.NoteParsingOptions = NoteParsingOptions.Clone();
        result.ChordTypeParsingOptions = ChordTypeParsingOptions.Clone();
        return result;
    }
}
