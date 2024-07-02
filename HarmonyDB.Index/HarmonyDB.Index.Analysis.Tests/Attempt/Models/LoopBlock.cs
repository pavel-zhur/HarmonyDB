using HarmonyDB.Index.Analysis.Models.CompactV1;

namespace HarmonyDB.Index.Analysis.Tests.Attempt.Models;

public record LoopBlock : IBlock
{
    public required ReadOnlyMemory<CompactHarmonyMovement> Loop { get; init; }

    public int LoopLength => Loop.Length;

    public required string Normalized { get; init; }

    public required int NormalizationShift { get; init; }

    /// <summary>
    /// Corresponds to the key (it is different for the same normalized progression if and only if it is in different keys).
    /// More precisely, it is the root of the first movement of the normalized sequence, wherever its corresponding original sequence movement is.
    /// </summary>
    public required byte NormalizationRoot { get; init; }

    public required int StartIndex { get; init; }

    public required int EndIndex { get; init; }
}