using HarmonyDB.Index.Analysis.Models.Index.Enums;

namespace HarmonyDB.Index.Analysis.Models.Index;

public static class BlockTypes
{
    public static IReadOnlyList<BlockType> Loop { get; } = new List<BlockType> { BlockType.Loop, BlockType.Sequence, BlockType.SequenceStart, BlockType.SequenceEnd };
    
    public static IReadOnlyList<BlockType> LoopAndJumps { get; } = new List<BlockType> { BlockType.Loop, BlockType.Sequence, BlockType.SequenceStart, BlockType.SequenceEnd, BlockType.LoopSelfJump, BlockType.LoopSelfMultiJump };

    public static IReadOnlyList<BlockType> All { get; } = Enum.GetValues<BlockType>().ToList();
    
    public static IReadOnlyList<BlockType> AllNoPingPongs { get; } = Enum.GetValues<BlockType>().Where(x => x != BlockType.PingPong).ToList();
    
    public static IReadOnlyList<BlockType> PolySequences { get; } = new List<BlockType> { BlockType.PolySequence, BlockType.Sequence, BlockType.SequenceStart, BlockType.SequenceEnd };
    
    public static IReadOnlyList<BlockType> LoopsAndPolySequences { get; } = new List<BlockType> { BlockType.Loop, BlockType.PolySequence, BlockType.Sequence, BlockType.SequenceStart, BlockType.SequenceEnd };
    
    public static IReadOnlyList<BlockType> LoopsAndPolyLoops { get; } = new List<BlockType> { BlockType.Loop, BlockType.PolyLoop, BlockType.Sequence, BlockType.SequenceStart, BlockType.SequenceEnd };
}