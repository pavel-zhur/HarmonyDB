using HarmonyDB.Common;

namespace HarmonyDB.Playground.Web.Models;

public record SongBetaModel
{
    public required string ExternalId { get; init; }
    
    public bool IncludeTrace { get; init; }

    public int Transpose { get; init; }

    public NoteAlteration? Alteration { get; init; }

    public bool Show9 { get; init; } = true;
    
    public bool Show7 { get; init; } = true;

    public bool Show6 { get; init; } = true;

    public bool ShowBass { get; init; } = true;

    public bool ShowSus { get; init; } = true;

    [Obsolete]
    public int? LoopId { get; init; }

    [Obsolete]
    public int? SelfJumpId { get; init; }
}