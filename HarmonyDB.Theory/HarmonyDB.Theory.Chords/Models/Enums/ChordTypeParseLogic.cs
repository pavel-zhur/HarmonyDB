namespace HarmonyDB.Theory.Chords.Models.Enums;

/// <summary>
/// The interpretation rules that were applied to read the chord type, for tracing the ambiguous readings.
/// </summary>
[Flags]
public enum ChordTypeParseLogic
{
    None = 0,

    /// <summary>
    /// An alteration was applied to the degree after it: "7#9", "7-5", "+5".
    /// </summary>
    AlterationBeforeDegree = 1,

    /// <summary>
    /// An alteration was applied to the degree before it: "75+", "9-", "add11+".
    /// </summary>
    AlterationAfterDegree = 1 << 1,

    /// <summary>
    /// A plus that alters no degree was read as the augmented fifth: "C+", "C7+" (when the plus does not raise the seventh).
    /// </summary>
    PlusAsAugmented = 1 << 2,

    /// <summary>
    /// A minus right after the root was read as the minor: "C-", "C-7".
    /// </summary>
    MinusAsMinor = 1 << 3,

    /// <summary>
    /// A sharp or a flat that alters no degree was applied to the root: "Fm#" is F#m.
    /// </summary>
    MisplacedRootAccidental = 1 << 4,

    /// <summary>
    /// The ninth, eleventh or thirteenth without a seventh, written in parentheses or after a slash, was read as added: "C(9)" and "C/9" are Cadd9.
    /// </summary>
    ParenthesizedDegreeAdded = 1 << 5,

    /// <summary>
    /// The ninth or eleventh next to the sixth, without a seventh, was read as added: "C6/9", "C69".
    /// </summary>
    DegreeAddedToSixth = 1 << 6,

    /// <summary>
    /// The altered ninth written as a bare number implied the seventh: "C9-", "C-9" and "C(b9)" are C7(b9).
    /// </summary>
    AlteredDegreeImpliedSeventh = 1 << 7,

    /// <summary>
    /// A bare "2" was read according to <see cref="Options.BareSecondMeaning"/>.
    /// </summary>
    BareSecond = 1 << 8,

    /// <summary>
    /// A bare "4" without a written third was read according to <see cref="Options.BareFourthMeaning"/>.
    /// </summary>
    BareFourth = 1 << 9,

    /// <summary>
    /// The diminished triad with a plain seventh was read as the diminished seventh chord: "Cdim(7)", "C7dim".
    /// </summary>
    DiminishedWithSeventh = 1 << 10,

    /// <summary>
    /// The solfège reading of the root left an unreadable rest and the letter reading was taken: "Fadd9".
    /// </summary>
    LetterRootInsteadOfSolfege = 1 << 11,

    /// <summary>
    /// A suspension written over the minor was read as the suspended chord: "Amsus2" is Asus2 (the suspension replaces the third).
    /// </summary>
    SuspensionOverMinor = 1 << 12,

    /// <summary>
    /// "maj" without a degree was read according to <see cref="Options.MajWithoutDegreeMeaning"/>.
    /// </summary>
    MajWithoutDegree = 1 << 13,

    /// <summary>
    /// A capital "M" without a degree was read according to <see cref="Options.CapitalMWithoutDegreeMeaning"/>.
    /// </summary>
    CapitalMWithoutDegree = 1 << 14,

    /// <summary>
    /// "dim" without a seventh was read according to <see cref="Options.DimWithoutSeventhMeaning"/>.
    /// </summary>
    DimWithoutSeventh = 1 << 15,

    /// <summary>
    /// The plus raised the seventh: "C7+", "C+7" (see <see cref="Options.PlusSeventhMeaning"/>).
    /// </summary>
    PlusRaisedSeventh = 1 << 16,

    /// <summary>
    /// A bare ninth without a seventh was read according to <see cref="Options.BareNinthMeaning"/>: "C9".
    /// </summary>
    BareNinth = 1 << 17,

    /// <summary>
    /// A minus at the end that alters nothing was ignored as a hold mark: "Em7-".
    /// </summary>
    TrailingMinusIgnored = 1 << 18,

    /// <summary>
    /// "add" without its degree added the bare degree before it or the ninth: "C9add", "Cadd".
    /// </summary>
    AddWithoutDegree = 1 << 19,

    /// <summary>
    /// The fifth was written though it is there anyway: "G#75", "Gadd5", "D9/5".
    /// </summary>
    RedundantFifth = 1 << 20,

    /// <summary>
    /// The fifth over the minor was read as the power chord: "Em5" is E5.
    /// </summary>
    PowerOverMinor = 1 << 21,

    /// <summary>
    /// The fifth written after a slash over the minor seventh was read as the flat fifth: "Bm7/5" is Bm7b5.
    /// </summary>
    FlatFifthAfterMinorSeventh = 1 << 22,

    /// <summary>
    /// An altered fifth beside the power chord was added to it: "A5#5" is A, E, F.
    /// </summary>
    AlteredFifthAddedToPower = 1 << 23,
}
