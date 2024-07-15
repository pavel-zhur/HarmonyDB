using HarmonyDB.Index.Api.Model.VExternal1.Caches;

namespace HarmonyDB.Index.Api.Model.VExternal1;

public class StructureSongResponse
{
    public required StructureSongTonality Song { get; init; }

    public required List<StructureLoopTonality> Loops { get; init; }

    public required List<StructureLink> Links { get; init; }
}