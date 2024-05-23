namespace HarmonyDB.Source.Api.Model.VInternal;

public class GetProgressionsIndexResponse
{
    public required IReadOnlyDictionary<string, byte[]> Progressions { get; init; }

    public string? NextToken { get; set; }
}