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
}