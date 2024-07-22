using HarmonyDB.Index.Analysis.Models.Index.Blocks.Interfaces;
using HarmonyDB.Index.Analysis.Models.Index.Enums;

namespace HarmonyDB.Index.Analysis.Models.Index.Blocks;

public class EdgeBlock(IndexedBlockType type) : IIndexedBlock
{
    public string Normalized => throw new InvalidOperationException();

    public byte NormalizationRoot => throw new InvalidOperationException();

    public int StartIndex => throw new InvalidOperationException();

    public int EndIndex => throw new InvalidOperationException();

    public int BlockLength => 0;
    
    public IEnumerable<IIndexedBlock> Children => [];

    public string? GetNormalizedCoordinate(int index) => null; // song start blocks do not have normalization shifts

    public IndexedBlockType Type { get; } = type;
}