namespace HarmonyDB.Index.Api.Model.VExternal1.Tonalities;

public class SongResponse
{
    public required Song Song { get; init; }

    public required List<Loop> Loops { get; init; }

    public required List<Link> Links { get; init; }
}