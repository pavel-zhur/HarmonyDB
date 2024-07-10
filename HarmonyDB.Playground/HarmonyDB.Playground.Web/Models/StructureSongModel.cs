namespace HarmonyDB.Playground.Web.Models;

public record StructureSongModel
{
    public required string ExternalId { get; init; }

    public bool IncludeTrace { get; init; }
}