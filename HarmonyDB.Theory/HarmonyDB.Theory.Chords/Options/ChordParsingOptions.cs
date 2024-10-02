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
    };

    public bool ForgiveSameBass { get; set; }
    public bool ForgiveEdgeWhitespaces { get; set; }
    public NoteParsingOptions NoteParsingOptions { get; set; } = NoteParsingOptions.Default;
    public bool ForgiveRoundBraces { get; set; }
}