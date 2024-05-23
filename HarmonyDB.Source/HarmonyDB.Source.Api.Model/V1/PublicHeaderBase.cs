namespace HarmonyDB.Source.Api.Model.V1;

public class PublicHeaderBase
{
    public required string ExternalId { get; set; }

    public required Uri SourceUri { get; set; }

    public required string Source { get; set; }

    public required string? Type { get; init; }

    public required float? Rating { get; init; }

    public required string? Title { get; init; }

    public required IReadOnlyList<string>? Artists { get; init; }

    public required IReadOnlyDictionary<string, object?> SpecificAttributes { get; init; }
}