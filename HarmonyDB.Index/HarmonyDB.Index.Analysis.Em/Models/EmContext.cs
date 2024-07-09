namespace HarmonyDB.Index.Analysis.Em.Models;

public sealed class EmContext
{
    public required ILookup<string, ILoopLink> LoopLinksBySongId { get; init; }
    public required ILookup<string, ILoopLink> LoopLinksByLoopId { get; init; }
    public required IReadOnlyDictionary<string, int> SongCounts { get; init; }
}