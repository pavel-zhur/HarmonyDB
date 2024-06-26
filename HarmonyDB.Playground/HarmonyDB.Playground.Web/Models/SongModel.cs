using HarmonyDB.Common;

namespace HarmonyDB.Playground.Web.Models;

public record SongModel
{
    public required string ExternalId { get; init; }
    
    public bool IncludeTrace { get; init; }

    public string? Highlight { get; init; }

    public int Transpose { get; init; }

    public NoteAlteration? Alteration { get; init; }
}