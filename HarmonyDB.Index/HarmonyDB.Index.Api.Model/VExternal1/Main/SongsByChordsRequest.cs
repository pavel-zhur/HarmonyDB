using System.Text.Json.Serialization;

namespace HarmonyDB.Index.Api.Model.VExternal1.Main;

public record SongsByChordsRequest : PagedRequestBase
{
    public required string Query { get; init; }

    public float MinCoverage { get; init; } = 0.5f;

    public int MinRating { get; init; } = 70;

    public int SongsPerPage { get; init; } = 100;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public SongsByChordsRequestOrdering Ordering { get; init; } = SongsByChordsRequestOrdering.ByRating;
}