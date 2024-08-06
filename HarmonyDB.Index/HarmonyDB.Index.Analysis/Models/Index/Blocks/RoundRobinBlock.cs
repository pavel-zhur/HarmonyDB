using HarmonyDB.Index.Analysis.Models.Index.Blocks.Interfaces;
using HarmonyDB.Index.Analysis.Models.Index.Enums;
using HarmonyDB.Index.Analysis.Models.Index.Graphs;
using OneShelf.Common;

namespace HarmonyDB.Index.Analysis.Models.Index.Blocks;

public class RoundRobinBlock : IIndexedBlock
{
    public RoundRobinBlock(IReadOnlyList<IIndexedBlock> children, int childrenPeriodLength)
    {
        Children = children;
        ChildrenPeriodLength = childrenPeriodLength;

        Level = children.Select(x => x is RoundRobinBlock roundRobinBlock ? roundRobinBlock.Level : 0).Max() + 1;
        Type = Level == 1 ? BlockType.RoundRobin : BlockType.RoundRobinLx;

        Normalized = GetNormalization(Children, ChildrenPeriodLength, out var normalizationRoot, Level);
        NormalizationRoot = normalizationRoot;
    }

    public IReadOnlyList<IIndexedBlock> Children { get; }

    IEnumerable<IIndexedBlock> IIndexedBlock.Children => Children;

    public BlockType Type { get; }

    public string Normalized { get; }

    public byte NormalizationRoot { get; }

    public int StartIndex => Children[0].StartIndex;
    
    public int EndIndex => Children[^1].EndIndex;
    
    public int BlockLength => EndIndex - StartIndex + 1;
    
    public int ChildrenPeriodLength { get; }
    
    public int Level { get; }

    public string? GetNormalizedCoordinate(int index)
    {
        return string.Join(", ", Children
            .Where(c => index >= c.StartIndex && index <= c.EndIndex)
            .Select(m => $"{m.Normalized}, {m.GetNormalizedCoordinate(index)}"));
    }

    /// <summary>
    /// Returns joint-points-aware normalization, however, invariant against rotation.
    /// </summary>
    /// <param name="children"></param>
    /// <param name="distinctCount"></param>
    /// <returns></returns>
    public static string GetNormalization(IReadOnlyList<IIndexedBlock> children, int distinctCount, out byte normalizationRoot, int? level = null)
    {
        string GetNormalizationAt(int i) => BlockJoint.GetNormalization(children[i], children[i + 1]);

        var minChild = children
            .Take(distinctCount)
            .WithIndices()
            .MinBy(x => x.x.Normalized)
            .i;

        normalizationRoot = children[minChild].NormalizationRoot;

        return string.Join(", ", Enumerable
            .Range(
                minChild,
                distinctCount)
            .Select(i => i % distinctCount)
            .Select(GetNormalizationAt)
            .Prepend(level?.ToString()));
    }
}