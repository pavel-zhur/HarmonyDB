using System.Text.Json.Serialization;

namespace HarmonyDB.Index.Api.Model.VExternal1.Tonalities;

public record LoopsRequest : PagedRequestBase
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

    public RequestSecondFilter SecondFilter { get; init; } = RequestSecondFilter.Any;

    public RequestScaleFilter DetectedScaleFilter { get; init; } = RequestScaleFilter.Any;

    public int LoopsPerPage { get; init; } = 100;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public LoopsRequestOrdering Ordering { get; init; } = LoopsRequestOrdering.SuccessionsDesc;
}