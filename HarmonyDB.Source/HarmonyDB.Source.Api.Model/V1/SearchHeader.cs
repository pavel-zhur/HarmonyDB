namespace HarmonyDB.Source.Api.Model.V1;

public record SearchHeader : PublicHeaderBase
{
    public required bool IsSupported { get; init; }
}