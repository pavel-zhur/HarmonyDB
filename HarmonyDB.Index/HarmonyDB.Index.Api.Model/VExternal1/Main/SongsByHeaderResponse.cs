namespace HarmonyDB.Index.Api.Model.VExternal1.Main;

public record SongsByHeaderResponse : PagedResponseBase
{
    public required List<SongsByHeaderResponseSong> Songs { get; init; }
}