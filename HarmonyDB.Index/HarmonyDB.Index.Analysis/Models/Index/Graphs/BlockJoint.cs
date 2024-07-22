using HarmonyDB.Index.Analysis.Models.Index.Graphs.Interfaces;

namespace HarmonyDB.Index.Analysis.Models.Index.Graphs;

public class BlockJoint : IBlockJoint
{
    public required BlockEnvironment Block1 { get; init; }

    public required BlockEnvironment Block2 { get; init; }

    IBlockEnvironment IBlockJoint.Block1 => Block1;
    IBlockEnvironment IBlockJoint.Block2 => Block2;

    public int OverlapLength => Block1.Block.EndIndex - Block2.Block.StartIndex switch
    {
        var x and >= 0 => x,
        _ => throw new("The overlap cannot be negative."),
    };

    public string Normalization =>
        $"{Block1.Block.Type}, " +
        $"{Block2.Block.Type}, " +
        $"{Block1.Block.Normalized}, " +
        $"{Block2.Block.Normalized}, " +
        $"{OverlapLength}, " +
        $"{Block1.Block.GetNormalizedCoordinate(Block2.Block.StartIndex)}, " +
        $"{Block2.Block.GetNormalizedCoordinate(Block1.Block.EndIndex)}";
}