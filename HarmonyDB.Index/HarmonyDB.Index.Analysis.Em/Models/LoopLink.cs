namespace HarmonyDB.Index.Analysis.Em.Models;

public record LoopLink
{
    public required Loop Loop { get; init; }
    public required Song Song { get; init; }
    public string SongId => Song.Id;
    public string LoopId => Loop.Id;
    public required byte Shift { get; init; }
    public required float Weight { get; init; }
}