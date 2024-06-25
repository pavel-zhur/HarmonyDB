namespace HarmonyDB.Source.Api.Model.V1;

public record PublicHeaderBase : IndexHeader
{
    public required Uri SourceUri { get; set; }

    public required string? Type { get; init; }

    public required IReadOnlyDictionary<string, object?> SpecificAttributes { get; init; }
}