namespace HarmonyDB.Source.Api.Model.V1.Api;

public class GetSongsResponse
{
    public required Dictionary<string, Chords> Songs { get; init; }
}