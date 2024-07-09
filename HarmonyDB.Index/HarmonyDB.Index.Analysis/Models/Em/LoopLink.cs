using HarmonyDB.Index.Analysis.Em.Models;

namespace HarmonyDB.Index.Analysis.Models.Em;

public record LoopLink : ILoopLink
{
    public required Loop Loop { get; init; }
    public required Song Song { get; init; }
    public string SongId => Song.Id;
    public string LoopId => Loop.Id;
    public required byte Shift { get; init; }
    public float Weight => (Occurrences + Successions * 4) * (Loop.Length == 2 ? 1 : 5) * (Song.KnownTonality.HasValue ? 5 : 1);
    ISong ILoopLink.Song => Song;
    ILoop ILoopLink.Loop => Loop;

    public required short Occurrences { get; init; }
    public required short Successions { get; init; }
}