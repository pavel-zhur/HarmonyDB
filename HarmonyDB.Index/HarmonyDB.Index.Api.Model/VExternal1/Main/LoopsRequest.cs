using System.Text.Json.Serialization;

namespace HarmonyDB.Index.Api.Model.VExternal1.Main;

public record LoopsRequest : PagedRequestBase
{
    public int MinLength { get; init; } = 3;

    public int? MaxLength { get; init; }

    public int MinTotalSongs { get; init; } = 1;
    
    public int MinTotalSuccessions { get; init; } = 1;
    
    public int MinTotalOccurrences { get; init; } = 1;

    public int LoopsPerPage { get; init; } = 100;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public LoopsRequestOrdering Ordering { get; init; } = LoopsRequestOrdering.SongsDesc;

    public LoopsRequestCompoundFilter Compound { get; init; } = LoopsRequestCompoundFilter.Simple;
}