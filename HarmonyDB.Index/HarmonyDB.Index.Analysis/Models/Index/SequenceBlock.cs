using HarmonyDB.Index.Analysis.Models.CompactV1;

namespace HarmonyDB.Index.Analysis.Models.Index;

public record SequenceBlock : IBlock
{
    public required ReadOnlyMemory<CompactHarmonyMovement> Sequence { get; init; }

    public required string Normalized { get; init; }

    /// <summary>
    /// Corresponds to the key (it is different for the same normalized progression if and only if it is in different keys).
    /// More precisely, it is the root of the first movement of the normalized sequence, wherever its corresponding original sequence movement is.
    /// </summary>
    public required byte NormalizationRoot { get; init; }

    public required int StartIndex { get; init; }

    public required int EndIndex { get; init; }

    public int BlockLength => EndIndex - StartIndex + 1;
}