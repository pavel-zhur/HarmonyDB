namespace HarmonyDB.Sources.Api.Model.V1.Api;

public class GetSongsResponse
{
    public required Dictionary<string, Chords> Songs { get; init; }
}