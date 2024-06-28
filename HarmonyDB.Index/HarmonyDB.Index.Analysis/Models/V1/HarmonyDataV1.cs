using HarmonyDB.Common;

namespace HarmonyDB.Index.Analysis.Models.V1;

public record HarmonyDataV1
{
    public required int Root { get; init; }

    public NoteAlteration? Alteration { get; init; }

    public required ChordType ChordType { get; init; }
}