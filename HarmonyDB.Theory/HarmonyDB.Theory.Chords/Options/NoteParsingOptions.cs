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

    public bool CaseSensitive { get; set; } = true;
    public HHandling HHandling { get; set; } = HHandling.BMeansH;
    public bool ForgiveRussianC { get; set; }
}