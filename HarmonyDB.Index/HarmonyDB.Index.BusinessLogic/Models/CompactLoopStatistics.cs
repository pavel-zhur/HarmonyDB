﻿using System.Text.Json.Serialization;

namespace HarmonyDB.Index.BusinessLogic.Models;

public class CompactLoopStatistics
{
    [JsonPropertyName("e")]
    public required List<string> ExternalIds { get; init; }

    [JsonPropertyName("o")]
    public int TotalOccurrences { get; set; }

    [JsonPropertyName("s")]
    public int TotalSuccessions { get; set; }
}