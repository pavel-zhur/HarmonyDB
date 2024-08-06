using HarmonyDB.Index.Analysis.Models.Index.Blocks.Interfaces;
using HarmonyDB.Index.Analysis.Models.Index.Enums;

namespace HarmonyDB.Index.Analysis.Models.Index.Blocks;

public class LoopSelfMultiJumpBlock : IBlock
{
    public int LoopLength => ChildLoops[0].LoopLength;

    public string Normalized => ChildLoops[0].Normalized;

    public int StartIndex => ChildLoops[0].StartIndex;

    public int EndIndex => ChildLoops[^1].EndIndex;

    /// <summary>
    /// Shows the key(s) the main loop exists in.
    /// </summary>
    public required IReadOnlyList<byte> NormalizationRootsFlow { get; init; }

    public bool HasModulations => NormalizationRootsFlow.Count > 1;

    public bool IsModulation => NormalizationRootsFlow[0] != NormalizationRootsFlow[^1];

    public required IReadOnlyList<LoopBlock> ChildLoops { get; init; }

    public required IReadOnlyList<LoopSelfJumpBlock> ChildJumps { get; init; }

    public int BlockLength => EndIndex - StartIndex + 1;

    public IEnumerable<IBlock> Children => ChildJumps;

    BlockType IBlock.Type => BlockType.LoopSelfMultiJump;
}