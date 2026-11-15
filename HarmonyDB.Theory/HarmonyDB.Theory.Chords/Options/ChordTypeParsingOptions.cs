namespace HarmonyDB.Theory.Chords.Options;

public class ChordTypeParsingOptions
{
    public static readonly ChordTypeParsingOptions Default = new();

    public static readonly ChordTypeParsingOptions MostForgiving = new()
    {
        TrimWhitespaceFragments = true,
        IgnoreTrailingApostrophes = true,
        IgnoreTrailingSlashes = true,
        IgnoreTrailingStars = true,
        IgnoreTrailingPunctuation = true,
        IgnoreVariantNumbers = true,
        ForgiveUnclosedParentheses = true,
        ForgiveSwallowedAdd = true,
        QuestionsParsingBehavior = QuestionsParsingBehavior.IgnoreAndTreatOnlyAsPower,
        ForgiveMisplacedRootAccidental = true,
    };

    public bool TrimWhitespaceFragments { get; set; }

    public bool IgnoreTrailingApostrophes { get; set; }

    public bool IgnoreTrailingSlashes { get; set; }

    public bool IgnoreTrailingStars { get; set; }

    /// <summary>
    /// Ignore the periods, commas, exclamation marks, semicolons, bar lines after the chord ("G.", "Am,", "C|"),
    /// and a trailing minus that alters nothing, a hold mark ("Em7-", "Aadd9-").
    /// </summary>
    public bool IgnoreTrailingPunctuation { get; set; }

    /// <summary>
    /// Ignore the variant number 1 after the chord: "G1" is G (the first of the variants G1, G2, G3 of a song).
    /// </summary>
    public bool IgnoreVariantNumbers { get; set; }

    /// <summary>
    /// Read a parenthesis left open at the end of the chord type as closed: "C(9" is C(9), "Dm7(4" is Dm7(4).
    /// </summary>
    public bool ForgiveUnclosedParentheses { get; set; }

    /// <summary>
    /// Read "dd" right after the root as "add": "Fdd9" is Fadd9 (a conversion from solfège took the "a" of "Fadd9" for "Fa").
    /// </summary>
    public bool ForgiveSwallowedAdd { get; set; }

    public QuestionsParsingBehavior QuestionsParsingBehavior { get; set; } = QuestionsParsingBehavior.Error;

    /// <summary>
    /// A sharp or a flat written after the chord type instead of after the root letter, altering nothing there, is applied to the root:
    /// "Fm#" is F#m, "Bmb" is Bbm, "F5#" is F#5, "C7#" is C#7.
    /// </summary>
    public bool ForgiveMisplacedRootAccidental { get; set; }

    public PlusSeventhMeaning PlusSeventhMeaning { get; set; } = PlusSeventhMeaning.MajorSeventh;

    public BareSecondMeaning BareSecondMeaning { get; set; } = BareSecondMeaning.Suspended;

    public BareFourthMeaning BareFourthMeaning { get; set; } = BareFourthMeaning.Suspended;

    public BareNinthMeaning BareNinthMeaning { get; set; } = BareNinthMeaning.Dominant;

    public MajWithoutDegreeMeaning MajWithoutDegreeMeaning { get; set; } = MajWithoutDegreeMeaning.MajorSeventh;

    public CapitalMWithoutDegreeMeaning CapitalMWithoutDegreeMeaning { get; set; } = CapitalMWithoutDegreeMeaning.MajorSeventh;

    public DimWithoutSeventhMeaning DimWithoutSeventhMeaning { get; set; } = DimWithoutSeventhMeaning.Triad;

    internal ChordTypeParsingOptions Clone() => (ChordTypeParsingOptions)MemberwiseClone();
}
