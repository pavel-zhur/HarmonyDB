namespace HarmonyDB.Playground.Web.Models;

public record StructureLoopModel
{
    public required string Normalized { get; init; }

    public bool IncludeTrace { get; init; }
}