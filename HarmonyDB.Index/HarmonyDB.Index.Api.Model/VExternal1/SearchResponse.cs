namespace HarmonyDB.Index.Api.Model.VExternal1;

public record SearchResponse
{
    public required int Total { get; init; }

    public required int TotalPages { get; init; }

    public required List<SearchResponseSong> Songs { get; init; }

    public required int CurrentPageNumber { get; init; }
}