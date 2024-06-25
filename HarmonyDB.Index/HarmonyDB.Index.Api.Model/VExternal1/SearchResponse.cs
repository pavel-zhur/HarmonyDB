namespace HarmonyDB.Index.Api.Model.VExternal1;

public record SearchResponse : PagedResponseBase
{
    public required List<SearchResponseSong> Songs { get; init; }
}