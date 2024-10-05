namespace HarmonyDB.Theory.Chords.Models.Enums;

public enum ChordParseResultError
{
    WhitespaceFragments,
    UnexpectedChordTypeToken,
    UnreadableRoot,
    SameBass,
    EmptyString,
}