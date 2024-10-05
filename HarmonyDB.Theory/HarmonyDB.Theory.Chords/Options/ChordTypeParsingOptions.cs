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
        QuestionsParsingBehavior = QuestionsParsingBehavior.IgnoreAndTreatOnlyAsPower,
        OnlyIntegersInParenthesesAreAddedDegrees = true,
    };

    public bool TrimWhitespaceFragments { get; set; }

    public bool IgnoreTrailingApostrophes { get; set; }

    public bool IgnoreTrailingSlashes { get; set; }
    
    public bool IgnoreTrailingStars { get; set; }
    
    public QuestionsParsingBehavior QuestionsParsingBehavior { get; set; } = QuestionsParsingBehavior.Error;

    public bool OnlyIntegersInParenthesesAreAddedDegrees { get; set; }
}