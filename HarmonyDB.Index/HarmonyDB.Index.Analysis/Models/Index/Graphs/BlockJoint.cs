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
}