using HarmonyDB.Index.Analysis.Models.CompactV1;
using HarmonyDB.Index.Analysis.Models.Interfaces;

namespace HarmonyDB.Index.Analysis.Models;

public class HarmonyMovement : ISearchableHarmonyMovement
{
    public required HarmonyGroup From { get; init; }

    public required HarmonyGroup To { get; init; }

    public required byte RootDelta { get; init; }

    public required ChordType FromType { get; init; }

    public required ChordType ToType { get; init; }

    public required string Title { get; init; }

    public CompactHarmonyMovement Compact()
        => new()
        {
            RootDelta = RootDelta,
            FromType = FromType,
            ToType = ToType,
        };
}