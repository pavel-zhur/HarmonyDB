using HarmonyDB.Index.Analysis.Em.Models;

namespace HarmonyDB.Index.Analysis.Tests.Em;

public record TestLoopLink : ILoopLink
{
    public required TestLoop Loop { get; init; }
    public required TestSong Song { get; init; }
    public string SongId => Song.Id;
    public string LoopId => Loop.Id;
    public required byte Shift { get; init; }
    public required float Weight { get; init; }
    ISong ILoopLink.Song => Song;
    ILoop ILoopLink.Loop => Loop;
}