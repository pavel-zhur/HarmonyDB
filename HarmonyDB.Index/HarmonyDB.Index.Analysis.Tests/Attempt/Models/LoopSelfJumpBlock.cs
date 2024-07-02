using HarmonyDB.Index.Analysis.Models.CompactV1;

namespace HarmonyDB.Index.Analysis.Tests.Attempt.Models;

public record LoopSelfJumpBlock : IBlock
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

    public (int fromNormalizedRootIndex, int toNormalizedRootIndex)? SameKeyJumpPoints
        => IsModulation
            ? null
            : ((
                Loop1.EndIndex
                - Loop1.StartIndex
                + Loop1.NormalizationShift
                + 1
            ) % Loop1.LoopLength, Loop2.NormalizationShift);
}