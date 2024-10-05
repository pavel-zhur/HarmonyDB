namespace HarmonyDB.Theory.Chords.Models.Enums;

public enum ChordParseError
{
    WhitespaceFragments,
    UnexpectedChordTypeToken,
    UnreadableRoot,
    SameBass,
    EmptyString,
}