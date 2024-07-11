namespace HarmonyDB.Index.Api.Model.VExternal1;

public record StructureLoopsResponse : PagedResponseBase
{
    public required List<StructureLoopTonality> Loops { get; init; }

    public required StructureLoopsResponseDistributions Distributions { get; init; }
}