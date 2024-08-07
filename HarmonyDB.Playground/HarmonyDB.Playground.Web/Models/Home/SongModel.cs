using HarmonyDB.Common;

namespace HarmonyDB.Playground.Web.Models.Home;

public record SongModel
{
    public required string ExternalId { get; init; }

    public bool IncludeTrace { get; init; }

    public string? Highlight { get; init; }

    public int Transpose { get; init; }

    public NoteAlteration? Alteration { get; init; }

    public bool Show9 { get; init; } = true;

    public bool Show7 { get; init; } = true;

    public bool Show6 { get; init; } = true;

    public bool ShowBass { get; init; } = true;

    public bool ShowSus { get; init; } = true;
}