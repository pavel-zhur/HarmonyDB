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
    RomansNotInParentheses,
    RomansInParenthesesExpectedOnly,
    OnlyIntegerInParentheses,
    DuplicateAdditions,
    EachExtensionTypeExpectedUnique,
    MaxOneMajExtensionExpected,
    MultipleFrets
}