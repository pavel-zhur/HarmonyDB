namespace HarmonyDB.Index.Analysis.Em.Models;

public sealed class EmContext
{
    public required ILookup<string, LoopLink> LoopLinksBySongId { get; init; }
    public required ILookup<string, LoopLink> LoopLinksByLoopId { get; init; }
    public required IReadOnlyDictionary<string, int> SongCounts { get; init; }
}