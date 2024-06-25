namespace HarmonyDB.Index.Api.Model.VExternal1;

public record LoopsResponse : PagedResponseBase
{
    public required List<LoopStatistics> Loops { get; init; }
}