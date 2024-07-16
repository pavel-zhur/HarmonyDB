namespace HarmonyDB.Index.Api.Model.VExternal1.Tonalities;

public record SongsResponse : PagedResponseBase
{
    public required List<Song> Songs { get; init; }
    
    public required SongsResponseDistributions Distributions { get; init; }
}