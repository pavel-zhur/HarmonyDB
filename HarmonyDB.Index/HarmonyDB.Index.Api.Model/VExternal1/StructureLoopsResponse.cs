namespace HarmonyDB.Index.Api.Model.VExternal1;

public record StructureLoopsResponse : PagedRequestBase
{
    public required List<StructureLoopTonality> Loops { get; init; }
}