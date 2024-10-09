namespace HarmonyDB.Theory.Chords.Models.Enums;

public enum ChordParseError
{
    [Obsolete]
    NotImplemented,

    WhitespaceFragments,
    UnexpectedChordTypeToken,
    UnreadableRoot,
    SameBass,
    EmptyString,
    BadSymbols,
    DuplicateAdditions,
    EachExtensionTypeExpectedUnique,
    MaxOneMajExtensionExpected,
    EmptyBrackets,
}