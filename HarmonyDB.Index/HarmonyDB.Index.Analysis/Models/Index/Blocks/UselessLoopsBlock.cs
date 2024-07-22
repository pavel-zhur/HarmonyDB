using HarmonyDB.Index.Analysis.Models.Index.Blocks.Interfaces;
using HarmonyDB.Index.Analysis.Models.Index.Enums;
using HarmonyDB.Index.Analysis.Models.Index.Graphs;
using OneShelf.Common;

namespace HarmonyDB.Index.Analysis.Models.Index.Blocks;

public class UselessChunkBlock : IIndexedBlock
{
    public IReadOnlyList<IIndexedBlock> Children { get; init; }

    IEnumerable<IIndexedBlock> IIndexedBlock.Children => Children;

    public IndexedBlockType Type => IndexedBlockType.UselessChunk;
    
    public string Normalized => string.Join(", ", Children.AsPairs().Aggregate(string.Empty, (a, p) => a + BlockJoint.GetNormalization(p.previous, p.current)));

    public byte NormalizationRoot => throw new InvalidOperationException(
        "The useless chunk block does not currently participate in other higher level blocks, and probably will not. So this property is not implemented.");

    public int StartIndex => Children[0].StartIndex;
    
    public int EndIndex => Children[^1].EndIndex;
    
    public int BlockLength => EndIndex - StartIndex + 1;

    public string? GetNormalizedCoordinate(int index)
    {
        var matches = Children.Where(c => index >= c.StartIndex && index <= c.EndIndex).ToList();
        if (!matches.Any())
            throw new ArgumentOutOfRangeException(nameof(index));

        return string.Join(", ", matches.Select(m => $"{m.Normalized}, {m.GetNormalizedCoordinate(index)}"));
    }
}