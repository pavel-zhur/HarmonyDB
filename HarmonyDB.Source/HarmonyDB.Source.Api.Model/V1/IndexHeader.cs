namespace HarmonyDB.Source.Api.Model.V1;

public record IndexHeader : IndexHeaderBase
{
    public required IndexHeaderBestTonality? BestTonality { get; init; }
}