using System.Text.Json.Serialization;

namespace HarmonyDB.Index.Api.Model.VExternal1;

public record StructureLoopsRequest : PagedRequestBase
{
    public int MinLength { get; init; } = 3;

    public int? MaxLength { get; init; }

    public int MinTotalSongs { get; init; } = 1;
    
    public int MinTotalSuccessions { get; init; } = 1;
    
    public int MinTotalOccurrences { get; init; } = 1;

    public float MinConfidence { get; init; }

    public float MinTonicScore { get; init; }
    
    public float MinScaleScore { get; init; }

    public StructureLoopsRequestSecondFilter SecondFilter { get; init; } = StructureLoopsRequestSecondFilter.Any;

    public StructureLoopsRequestDetectedScaleFilter DetectedScaleFilter { get; init; } = StructureLoopsRequestDetectedScaleFilter.Any;

    public int LoopsPerPage { get; init; } = 100;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public StructureLoopsRequestOrdering Ordering { get; init; } = StructureLoopsRequestOrdering.ConfidenceDesc;
}