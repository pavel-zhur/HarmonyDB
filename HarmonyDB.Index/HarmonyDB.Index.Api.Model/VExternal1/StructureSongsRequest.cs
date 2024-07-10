using System.Text.Json.Serialization;

namespace HarmonyDB.Index.Api.Model.VExternal1;

public record StructureSongsRequest : PagedRequestBase
{
    public int MinRating { get; init; } = 70;

    public int MinTotalLoops { get; init; } = 1;
    
    public int? MaxTotalLoops { get; init; }

    public float MinTonalityConfidence { get; init; }
    
    public float MinTonicConfidence { get; init; }

    public float MaxTonalityConfidence { get; init; } = 1;
    
    public float MaxTonicConfidence { get; init; } = 1;

    public float MinTonicScore { get; init; }
    
    public float MinScaleScore { get; init; }

    public StructureRequestSecondFilter SecondFilter { get; init; } = StructureRequestSecondFilter.Any;

    public StructureRequestScaleFilter DetectedScaleFilter { get; init; } = StructureRequestScaleFilter.Any;

    public StructureRequestScaleFilter KnownScaleFilter { get; init; } = StructureRequestScaleFilter.Any;

    public StructureSongsRequestCorrectDetectionFilter CorrectDetectionFilter { get; init; } = StructureSongsRequestCorrectDetectionFilter.Any;

    public StructureSongsRequestKnownTonalityFilter KnownTonalityFilter { get; init; } = StructureSongsRequestKnownTonalityFilter.Any;
    
    public int SongsPerPage { get; init; } = 100;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public StructureSongsRequestOrdering Ordering { get; init; } = StructureSongsRequestOrdering.TonalityConfidenceDesc;
}