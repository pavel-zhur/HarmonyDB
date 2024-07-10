using System.Text.Json.Serialization;

namespace HarmonyDB.Index.Api.Model.VExternal1;

public record StructureLoopsRequest : PagedRequestBase
{
    public int MinLength { get; init; } = 3;

    public int? MaxLength { get; init; }

    public int MinTotalSongs { get; init; } = 1;
    
    public int MinTotalOccurrences { get; init; } = 1;
    
    public int MinTotalSuccessions { get; init; } = 1;

    public float MinTonalityConfidence { get; init; }
    
    public float MinTonicConfidence { get; init; }

    public float MaxTonalityConfidence { get; init; } = 1;
    
    public float MaxTonicConfidence { get; init; } = 1;

    public float MinTonicScore { get; init; }
    
    public float MinScaleScore { get; init; }

    public StructureRequestSecondFilter SecondFilter { get; init; } = StructureRequestSecondFilter.Any;

    public StructureRequestDetectedScaleFilter DetectedScaleFilter { get; init; } = StructureRequestDetectedScaleFilter.Any;

    public int LoopsPerPage { get; init; } = 100;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public StructureLoopsRequestOrdering Ordering { get; init; } = StructureLoopsRequestOrdering.SuccessionsDesc;
}