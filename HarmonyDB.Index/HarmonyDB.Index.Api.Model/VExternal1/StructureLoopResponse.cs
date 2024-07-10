namespace HarmonyDB.Index.Api.Model.VExternal1;

public class StructureLoopResponse
{
    public required StructureLoopTonality Loop { get; init; }

    public required List<StructureLinkStatistics> LinkStatistics { get; init; }
}