using System.Text.Json.Serialization;

namespace HarmonyDB.Index.Api.Model.VExternal1;

public record SearchRequest
{
    public required string Query { get; init; }

    public int PageNumber { get; init; } = 1;

    public float MinCoverage { get; init; } = 0.5f;

    public int MinRating { get; init; } = 70;

    public int SongsPerPage { get; init; } = 100;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public SearchRequestOrdering Ordering { get; init; } = SearchRequestOrdering.ByRating;
}