namespace HarmonyDB.Index.Analysis.Em.Models;

public interface ILoopLink
{
    ILoop Loop { get; }
    ISong Song { get; }
    string SongId { get; }
    string LoopId { get; }
    byte Shift { get; }
    float Weight { get; }
}