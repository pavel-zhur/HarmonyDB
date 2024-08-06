using HarmonyDB.Index.Analysis.Models.Index.Blocks.Interfaces;
using HarmonyDB.Index.Analysis.Models.Index.Enums;
using HarmonyDB.Index.Analysis.Models.Index.Graphs;
using OneShelf.Common;

namespace HarmonyDB.Index.Analysis.Models.Index.Blocks;

public class PingPongBlock : IIndexedBlock
{
    public required IReadOnlyList<IIndexedBlock> Children { get; init; }

    IEnumerable<IIndexedBlock> IIndexedBlock.Children => Children;

    public BlockType Type => BlockType.PingPong;
    
    public string Normalized =>
        string.Join(", ", 
            BlockJoint.GetNormalization(Children[0], Children[1]).Once()
                .Append(BlockJoint.GetNormalization(Children[1], Children[2]))
                .OrderBy(x => x));

    public byte NormalizationRoot => throw new InvalidOperationException(
        "The ping pong block does not currently participate in other higher level blocks, and probably will not. So this property is not implemented.");

    public int StartIndex => Children[0].StartIndex;
    
    public int EndIndex => Children[^1].EndIndex;
    
    public int BlockLength => EndIndex - StartIndex + 1;

    public string? GetNormalizedCoordinate(int index)
    {
        var matches = Children.Where(c => index >= c.StartIndex && index <= c.EndIndex).ToList();
        if (matches.Count is not (1 or 2))
            throw new ArgumentOutOfRangeException(nameof(index));

        return string.Join(", ", matches.Select(m => $"{m.Normalized}, {m.GetNormalizedCoordinate(index)}"));
    }
}