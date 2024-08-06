namespace HarmonyDB.Index.Analysis.Models.Index;

public record PolyBlocksExtractionParameters
{
    public bool SequencesExcludeIfOverLoops { get; set; } = true;
    public bool SequencesExcludeIfOverLoopsWithTails { get; set; } = true;
    public bool SequencesExcludeIfExtendsLeftWithoutLosingOccurrences { get; set; } = true;
    public bool SequencesExcludeIfExtendsRightWithoutLosingOccurrencesOrOverlappingWithItself { get; set; } = true;

    public bool SequencesExcludeIfContainsAllChordsMoreThanOnce { get; set; } = false;
    public bool SequencesExcludeIfContainsSuccessiveLoopsBreak { get; set; } = false;

    public bool LoopsExcludeIfContainsAllChordsMoreThanOnce { get; set; } = false;
    public bool LoopsExcludeIfContainsSuccessiveLoopsBreak { get; set; } = false;
}