namespace HarmonyDB.Index.Api.Model.VExternal1.Main;

public record SongsByChordsResponse : PagedResponseBase
{
    public required List<SongsByChordsResponseSong> Songs { get; init; }
}