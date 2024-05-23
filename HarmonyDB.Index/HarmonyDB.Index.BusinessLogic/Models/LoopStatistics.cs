using System.Text.Json.Serialization;

namespace HarmonyDB.Index.BusinessLogic.Models;

public class LoopStatistics
{
    [JsonPropertyName("e")]
    public required List<string> ExternalIds { get; init; }

    [JsonPropertyName("o")]
    public int SumOccurrences { get; set; }

    [JsonPropertyName("s")]
    public int SumSuccessions { get; set; }
}