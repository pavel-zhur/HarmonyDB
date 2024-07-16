namespace HarmonyDB.Source.Api.Model.V1;

public record IndexHeaderBestTonality
{
    public required string Tonality { get; init; }

    public required bool IsReliable { get; init; }
}