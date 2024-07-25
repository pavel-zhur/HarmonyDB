using HarmonyDB.Index.Analysis.Models.Index.Blocks.Interfaces;
using HarmonyDB.Index.Analysis.Models.Index.Enums;
using HarmonyDB.Index.Analysis.Models.Index.Graphs;
using OneShelf.Common;

namespace HarmonyDB.Index.Analysis.Models.Index.Blocks;

public class RoundRobinBlock : IIndexedBlock
{
    public required IReadOnlyList<IIndexedBlock> Children { get; init; }

    IEnumerable<IIndexedBlock> IIndexedBlock.Children => Children;

    public BlockType Type => BlockType.RoundRobin;

    public string Normalized => GetNormalization(Children, ChildrenPeriodLength);

    /// <summary>
    /// Returns joint-points-aware normalization, however, invariant against rotation.
    /// </summary>
    /// <param name="children"></param>
    /// <param name="distinctCount"></param>
    /// <returns></returns>
    public static string GetNormalization(IReadOnlyList<IIndexedBlock> children, int distinctCount)
    {
        string GetNormalizationAt(int i) => BlockJoint.GetNormalization(children[i], children[i + 1]);

        return string.Join(", ", Enumerable
            .Range(
                children
                    .Take(distinctCount)
                    .WithIndices()
                    .MinBy(x => x.x.Normalized)
                    .i,
                distinctCount)
            .Select(i => i % distinctCount)
            .Select(GetNormalizationAt));
    }

    public byte NormalizationRoot => throw new InvalidOperationException(
        "The round robin block does not currently participate in other higher level blocks, and probably will not. So this property is not implemented.");

    public int StartIndex => Children[0].StartIndex;
    
    public int EndIndex => Children[^1].EndIndex;
    
    public int BlockLength => EndIndex - StartIndex + 1;
    
    public required int ChildrenPeriodLength { get; init; }

    public string? GetNormalizedCoordinate(int index)
    {
        return string.Join(", ", Children
            .Where(c => index >= c.StartIndex && index <= c.EndIndex)
            .Select(m => $"{m.Normalized}, {m.GetNormalizedCoordinate(index)}"));
    }
}