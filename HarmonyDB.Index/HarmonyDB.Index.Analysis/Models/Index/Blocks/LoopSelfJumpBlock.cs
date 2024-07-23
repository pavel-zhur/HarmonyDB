using HarmonyDB.Common.Representations.OneShelf;
using HarmonyDB.Index.Analysis.Models.CompactV1;
using HarmonyDB.Index.Analysis.Models.Index.Blocks.Interfaces;
using HarmonyDB.Index.Analysis.Models.Index.Enums;

namespace HarmonyDB.Index.Analysis.Models.Index.Blocks;

public class LoopSelfJumpBlock : IBlock
{
    public int LoopLength => Loop1.LoopLength;

    public string Normalized => Loop1.Normalized;

    public int StartIndex => Loop1.StartIndex;

    public int EndIndex => Loop2.EndIndex;

    public bool IsModulation => Loop1.NormalizationRoot != Loop2.NormalizationRoot;

    public required LoopBlock Loop1 { get; init; }

    public required LoopBlock Loop2 { get; init; }

    public required LoopBlock? JointLoop { get; init; }

    public required CompactHarmonyMovement? JointMovement { get; set; }

    public int BlockLength => EndIndex - StartIndex + 1;

    public IEnumerable<IBlock> Children => JointLoop == null ? [Loop1, Loop2] : [Loop1, JointLoop!, Loop2];

    BlockType IBlock.Type => BlockType.LoopSelfJump;

    public LoopSelfJumpType Type
    {
        get
        {
            if (!IsModulation)
                return LoopSelfJumpType.SameKeyJoint;

            if (Loop1.EndIndex >= Loop2.StartIndex)
                return LoopSelfJumpType.ModulationOverlap;

            return JointMovement != null
                    ? LoopSelfJumpType.ModulationJointMovement
                    : LoopSelfJumpType.ModulationAmbiguousChord;
        }
    }

    public byte ModulationDelta => Note.Normalize(Loop2.NormalizationRoot - Loop1.NormalizationRoot);

    /// <summary>
    /// For <see cref="LoopSelfJumpType.SameKeyJoint"/> and <see cref="LoopSelfJumpType.ModulationJointMovement"/> type, the meanings are (fromNormalizedRootIndex, toNormalizedRootIndex).
    /// For <see cref="LoopSelfJumpType.ModulationAmbiguousChord"/> type, the meanings are (equalNormalizedLoop1RootIndex, equalNormalizedLoop2RootIndex).
    /// For <see cref="LoopSelfJumpType.ModulationOverlap"/> type, returns null.
    /// </summary>
    public (int root1Index, int root2Index)? JumpPoints
        => Type == LoopSelfJumpType.ModulationOverlap
            ? null
            : ((Loop1.EndIndex
                - Loop1.StartIndex
                + Loop1.NormalizationShift
                + 1
                ) % Loop1.LoopLength, Loop2.NormalizationShift);

    public (int overlapStartRoot1Index, int overlapStartRoot2Index, int overlapLength)? OverlapJumpPoints
        => Type != LoopSelfJumpType.ModulationOverlap
            ? null
            : ((Loop2.StartIndex
                - Loop1.StartIndex
                + Loop1.NormalizationShift
                ) % Loop1.LoopLength, Loop2.NormalizationShift, Loop1.EndIndex - Loop2.StartIndex + 1);
}