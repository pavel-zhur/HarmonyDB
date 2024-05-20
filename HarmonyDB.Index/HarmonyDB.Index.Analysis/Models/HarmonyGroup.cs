using HarmonyDB.Index.Analysis.Models.V1;

namespace HarmonyDB.Index.Analysis.Models;

public class HarmonyGroup
{
    public required int Index { get; init; }

    public required IReadOnlyList<ChordProgressionDataV1> OriginalSequence { get; init; }

    public required HarmonyDataV1? HarmonyData { get; init; }

    public required int StartChordIndex { get; init; }

    public required int EndChordIndex { get; init; }

    public bool IsStandard => HarmonyData?.ChordType is ChordType.Major or ChordType.Minor;

    public required string HarmonyRepresentation { get; init; }
}