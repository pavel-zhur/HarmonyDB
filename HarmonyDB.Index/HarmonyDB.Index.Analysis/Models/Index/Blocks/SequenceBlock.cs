using HarmonyDB.Index.Analysis.Models.CompactV1;
using HarmonyDB.Index.Analysis.Models.Index.Blocks.Interfaces;
using HarmonyDB.Index.Analysis.Models.Index.Enums;

namespace HarmonyDB.Index.Analysis.Models.Index.Blocks;

public class SequenceBlock : IIndexedBlock
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

    public IEnumerable<IIndexedBlock> Children => [];

    public string? GetNormalizedCoordinate(int index)
        => index < StartIndex || index > EndIndex
            ? throw new ArgumentOutOfRangeException(nameof(index))
            : null; // sequence blocks do not have normalization shifts

    public BlockType Type => BlockType.Sequence;
}