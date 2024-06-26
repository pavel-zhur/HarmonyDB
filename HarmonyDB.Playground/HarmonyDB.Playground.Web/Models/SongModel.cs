using HarmonyDB.Common;

namespace HarmonyDB.Playground.Web.Models;

public record SongModel
{
    public required string ExternalId { get; set; }
    
    public bool IncludeTrace { get; set; }

    public string? Highlight { get; set; }

    public int Transpose { get; set; }

    public NoteAlteration? Alteration { get; set; }
}