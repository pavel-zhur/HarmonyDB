namespace HarmonyDB.Index.Api.Model.VExternal1.Tonalities;

public record LoopsResponse : PagedResponseBase
{
    public required List<Loop> Loops { get; init; }

    public required LoopsResponseDistributions Distributions { get; init; }

    public required LoopsResponseDistributions WeightedDistributionsByOccurrences { get; init; }
}