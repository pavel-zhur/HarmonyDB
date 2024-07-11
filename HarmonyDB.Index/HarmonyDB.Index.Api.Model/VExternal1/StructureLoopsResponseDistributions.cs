namespace HarmonyDB.Index.Api.Model.VExternal1;

public record StructureLoopsResponseDistributions
{
    public required int[] TotalSongs { get; init; }
    public required int[] TotalOccurrences { get; init; }
    public required int[] TotalSuccessions { get; init; }
    public required float[] TonalityConfidence { get; init; }
    public required float[] TonicConfidence { get; init; }
    public required float[] TonicScore { get; init; }
    public required float[] ScaleScore { get; init; }
}