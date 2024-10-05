namespace HarmonyDB.Theory.Chords.Options;

public class ChordTypeParsingOptions
{
    public static readonly ChordTypeParsingOptions Default = new();
    public static readonly ChordTypeParsingOptions MostForgiving = new();

    public bool TrimWhitespaceFragments { get; set; } = true;
}