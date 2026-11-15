namespace HarmonyDB.Theory.Chords.Models.Enums;

public enum ChordParseError
{
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

    /// <summary>
    /// A sharp, flat, plus or minus that alters no degree and has no meaning of its own there: "C7-", "Cm#" (unless forgiven).
    /// </summary>
    UnboundAlteration,

    /// <summary>
    /// A degree that cannot be altered this way: "+3", "b7", "#6".
    /// </summary>
    UnsupportedAlteration,

    /// <summary>
    /// "add" not followed by a degree.
    /// </summary>
    AddWithoutDegree,

    /// <summary>
    /// Qualities that exclude each other: "Cmaug", "Cdimaug", "Cmsus4".
    /// </summary>
    IncompatibleQualities,

    /// <summary>
    /// An extension that the quality does not take: "Cdim7(9)" with a seventh other than the diminished one, "Cø(maj7)".
    /// </summary>
    IncompatibleExtension,

    /// <summary>
    /// Too many alterations to read: more than <see cref="Constants.ChordConstants.MaxAlterations"/>.
    /// </summary>
    TooManyAlterations,

    /// <summary>
    /// A fifth after a leading minus: "A-5-5" (a tab fragment).
    /// </summary>
    UnexpectedFifth,

    /// <summary>
    /// The same degree written twice: "C7/7", "E-5-5-5" (usually a tab fragment).
    /// </summary>
    DuplicateDegrees,
}
