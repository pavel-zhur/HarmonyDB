namespace HarmonyDB.Index.Analysis.Models.Index;

public record LoopMassiveOverlapsBlock : IBlock
{
    public required IReadOnlyList<LoopBlock> InternalLoops { get; init; }
    
    public required int StartIndex { get; init; }
    
    public required int EndIndex { get; init; }
    
    public int BlockLength => EndIndex - StartIndex + 1;
    
    public required LoopBlock Edge1 { get; init; }
    
    public required LoopBlock Edge2 { get; init; }
}