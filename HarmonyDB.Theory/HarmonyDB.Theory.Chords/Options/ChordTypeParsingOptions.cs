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
        ForgiveUnclosedParentheses = true,
        QuestionsParsingBehavior = QuestionsParsingBehavior.IgnoreAndTreatOnlyAsPower,
        ForgiveMisplacedRootAccidental = true,
    };

    public bool TrimWhitespaceFragments { get; set; }

    public bool IgnoreTrailingApostrophes { get; set; }

    public bool IgnoreTrailingSlashes { get; set; }

    public bool IgnoreTrailingStars { get; set; }

    /// <summary>
    /// Ignore the periods, commas, exclamation marks, semicolons and bar lines after the chord: "G.", "Am,", "C|".
    /// </summary>
    public bool IgnoreTrailingPunctuation { get; set; }

    /// <summary>
    /// Read a parenthesis left open at the end of the chord type as closed: "C(9" is C(9), "Dm7(4" is Dm7(4).
    /// </summary>
    public bool ForgiveUnclosedParentheses { get; set; }

    public QuestionsParsingBehavior QuestionsParsingBehavior { get; set; } = QuestionsParsingBehavior.Error;

    /// <summary>
    /// A sharp or a flat written after the chord type instead of after the root letter, altering nothing there, is applied to the root:
    /// "Fm#" is F#m, "Bmb" is Bbm, "F5#" is F#5, "C7#" is C#7.
    /// </summary>
    public bool ForgiveMisplacedRootAccidental { get; set; }

    /// <summary>
    /// Whether a plus or a minus may alter the degree written before it: "C75+" is C7(#5), "C9-" is C7(b9), "Cadd11+" is Cadd(#11).
    /// When false, a plus not followed by a degree is the augmented fifth ("C9+" is C9(#5)), and a minus not followed by a degree is an error.
    /// The alterations written before the degree ("C7#5", "C7-9") are always read.
    /// </summary>
    public bool AlterationsAfterDegrees { get; set; } = true;

    public PlusSeventhMeaning PlusSeventhMeaning { get; set; } = PlusSeventhMeaning.MajorSeventh;

    public LeadingMinusMeaning LeadingMinusMeaning { get; set; } = LeadingMinusMeaning.Flat;

    public BareSecondMeaning BareSecondMeaning { get; set; } = BareSecondMeaning.Suspended;

    public BareFourthMeaning BareFourthMeaning { get; set; } = BareFourthMeaning.Suspended;

    public MajWithoutDegreeMeaning MajWithoutDegreeMeaning { get; set; } = MajWithoutDegreeMeaning.MajorSeventh;

    public CapitalMWithoutDegreeMeaning CapitalMWithoutDegreeMeaning { get; set; } = CapitalMWithoutDegreeMeaning.MajorSeventh;

    public DimWithoutSeventhMeaning DimWithoutSeventhMeaning { get; set; } = DimWithoutSeventhMeaning.Triad;
}
