using HarmonyDB.Index.Analysis.Models.Index.Blocks.Interfaces;
using HarmonyDB.Index.Analysis.Models.Index.Enums;
using HarmonyDB.Index.Analysis.Models.Index.Graphs.Interfaces;

namespace HarmonyDB.Index.Analysis.Models.Index.Graphs;

public class BlockJoint : IBlockJoint
{
    public required BlockEnvironment Block1 { get; init; }

    public required BlockEnvironment Block2 { get; init; }

    IBlockEnvironment IBlockJoint.Block1 => Block1;
    IBlockEnvironment IBlockJoint.Block2 => Block2;

    public int OverlapLength => GetOverlapLength(Block1.Block, Block2.Block);
    
    public static int GetOverlapLength(IIndexedBlock block1, IIndexedBlock block2) =>
        block1.EndIndex - block2.StartIndex switch
        {
            var x and >= 0 => x,
            _ => throw new("The overlap cannot be negative."),
        };

    public static string GetNormalization(IIndexedBlock block1, IIndexedBlock block2) =>
        $"{block1.Type}, " +
        $"{block2.Type}, " +
        $"{block1.Normalized}, " +
        $"{block2.Normalized}, " +
        $"{GetOverlapLength(block1, block2)}, " +
        $"{block1.GetNormalizedCoordinate(block2.StartIndex - 1)}, " +
        $"{block2.GetNormalizedCoordinate(block1.EndIndex + 1)}";
    
    public string Normalization => GetNormalization(Block1.Block, Block2.Block);

    public bool IsEdge
        => Block1.Block.Type is BlockType.SequenceStart or BlockType.SequenceEnd
           || Block2.Block.Type is BlockType.SequenceStart or BlockType.SequenceEnd;
}