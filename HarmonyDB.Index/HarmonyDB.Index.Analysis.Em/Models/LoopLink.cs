namespace HarmonyDB.Index.Analysis.Em.Models;

public record LoopLink
{
    public required ILoop Loop { get; init; }
    public required ISong Song { get; init; }
    public string SongId => Song.Id;
    public string LoopId => Loop.Id;
    public required int Shift { get; init; }
    public required int Count { get; init; }
}