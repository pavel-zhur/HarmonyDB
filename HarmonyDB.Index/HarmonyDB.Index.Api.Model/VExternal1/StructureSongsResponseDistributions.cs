namespace HarmonyDB.Index.Api.Model.VExternal1;

public record StructureSongsResponseDistributions
{
    public required float?[] Rating { get; init; }
    public required float[] TonalityConfidence { get; init; }
    public required float[] TonicConfidence { get; init; }
    public required float[] TonicScore { get; init; }
    public required float[] ScaleScore { get; init; }
    public required int[] TotalLoops { get; init; }
}