using System.Text.Json.Serialization;

namespace HarmonyDB.Index.Analysis.Models.V1;

public record ChordProgressionDataV1
{
    public required string OriginalRepresentation { get; init; }

    public required string HarmonyRepresentation { get; init; }

    public HarmonyDataV1? HarmonyData { get; init; }

    public required string ChordData { get; init; }

    [JsonIgnore]
    public bool IsStandard => HarmonyData?.ChordType is ChordType.Major or ChordType.Minor;
}