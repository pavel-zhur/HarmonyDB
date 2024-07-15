using HarmonyDB.Index.Api.Model.VExternal1.Caches;

namespace HarmonyDB.Index.Api.Model.VExternal1.Main;

public record LoopsResponse : PagedResponseBase
{
    public required List<LoopStatistics> Loops { get; init; }
}