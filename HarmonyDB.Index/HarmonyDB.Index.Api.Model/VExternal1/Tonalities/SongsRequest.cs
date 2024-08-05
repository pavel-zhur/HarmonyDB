using System.Text.Json.Serialization;

namespace HarmonyDB.Index.Api.Model.VExternal1.Tonalities;

public record SongsRequest : PagedRequestBase
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

    public RequestSecondFilter SecondFilter { get; init; } = RequestSecondFilter.Any;

    public RequestScaleFilter DetectedScaleFilter { get; init; } = RequestScaleFilter.Any;

    public RequestScaleFilter KnownScaleFilter { get; init; } = RequestScaleFilter.Any;

    public SongsRequestCorrectDetectionFilter CorrectDetectionFilter { get; init; } = SongsRequestCorrectDetectionFilter.Any;

    public SongsRequestKnownTonalityFilter KnownTonalityFilter { get; init; } = SongsRequestKnownTonalityFilter.Any;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public SongsRequestOrdering Ordering { get; init; } = SongsRequestOrdering.TonalityConfidenceDesc;
}