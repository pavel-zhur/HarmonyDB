namespace HarmonyDB.Index.Api.Model.VExternal1.Main;

public record SongsByHeaderRequest : PagedRequestBase
{
    public required string Query { get; init; }

    public int MinRating { get; init; } = 70;

    public int SongsPerPage { get; init; } = 100;
}