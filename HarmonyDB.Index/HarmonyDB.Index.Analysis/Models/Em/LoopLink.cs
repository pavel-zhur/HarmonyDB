using HarmonyDB.Index.Analysis.Em.Models;

namespace HarmonyDB.Index.Analysis.Models.Em;

public record LoopLink : ILoopLink
{
    public required Loop Loop { get; init; }
    public required Song Song { get; init; }
    public string SongId => Song.Id;
    public string LoopId => Loop.Id;
    public required byte Shift { get; init; }
    public required float Weight { get; init; }
    ISong ILoopLink.Song => Song;
    ILoop ILoopLink.Loop => Loop;
}