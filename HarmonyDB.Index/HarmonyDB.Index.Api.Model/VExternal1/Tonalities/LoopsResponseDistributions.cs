namespace HarmonyDB.Index.Api.Model.VExternal1.Tonalities;

public record LoopsResponseDistributions
{
    public required int[] TotalSongs { get; init; }
    public required float[] TotalOccurrences { get; init; }
    public required float[] TotalSuccessions { get; init; }
    public required float[] AverageCoverage { get; init; }
    public required float[] TonalityConfidence { get; init; }
    public required float[] TonicConfidence { get; init; }
    public required float[] TonicScore { get; init; }
    public required float[] ScaleScore { get; init; }
}