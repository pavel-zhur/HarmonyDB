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
    /// When parsing a chord together with the other chords of its song, decide what the bare B means from the song:
    /// when the song uses H, or when the chords with the bare B are all out of the key the other chords make and all fit it with the other reading of B,
    /// the other <see cref="Models.Enums.HHandling"/> is used for the song (B flat instead of B natural, or the other way round).
    /// See <see cref="Parsers.ChordParser.DetectHHandling"/>.
    /// </summary>
    public bool DetectHHandlingFromSong { get; set; }

    public ChordParsingOptions WithHHandling(HHandling hHandling)
    {
        var result = (ChordParsingOptions)MemberwiseClone();
        result.NoteParsingOptions = NoteParsingOptions.WithHHandling(hHandling);
        return result;
    }
}
