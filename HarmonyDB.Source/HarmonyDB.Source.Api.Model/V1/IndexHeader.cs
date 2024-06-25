namespace HarmonyDB.Source.Api.Model.V1;

public record IndexHeader
{
    public required string ExternalId { get; set; }

    public required string Source { get; set; }

    public required float? Rating { get; init; }

    public required string? Title { get; init; }

    public required IReadOnlyList<string>? Artists { get; init; }
}