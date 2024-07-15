using HarmonyDB.Index.Api.Model.VExternal1.Caches;

namespace HarmonyDB.Index.Api.Model.VExternal1.Tonalities;

public class SongResponse
{
    public required Song Song { get; init; }

    public required List<Loop> Loops { get; init; }

    public required List<StructureLink> Links { get; init; }
}