using System.Text.Json.Serialization;

namespace HarmonyDB.Index.BusinessLogic.Models;

public record CompactLoopStatistics
{
    [JsonPropertyName("e")]
    public required List<string> ExternalIds { get; init; }

    [JsonPropertyName("o")]
    public int TotalOccurrences { get; set; }

    [JsonPropertyName("s")]
    public int TotalSuccessions { get; set; }

    [JsonPropertyName("c")]
    public required string Counts { get; init; }
}