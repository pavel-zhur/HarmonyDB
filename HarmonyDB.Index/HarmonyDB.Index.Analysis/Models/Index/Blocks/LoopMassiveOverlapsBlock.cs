using HarmonyDB.Index.Analysis.Models.Index.Blocks.Interfaces;
using HarmonyDB.Index.Analysis.Models.Index.Enums;

namespace HarmonyDB.Index.Analysis.Models.Index.Blocks;

public class LoopMassiveOverlapsBlock : IBlock
{
    public required IReadOnlyList<LoopBlock> InternalLoops { get; init; }

    public required int StartIndex { get; init; }

    public required int EndIndex { get; init; }

    public int BlockLength => EndIndex - StartIndex + 1;

    public required LoopBlock Edge1 { get; init; }

    public required LoopBlock Edge2 { get; init; }

    public BlockType Type => BlockType.MassiveOverlaps;
}