namespace HarmonyDB.Index.Analysis.Models.Index.Graphs;

public class BlockEnvironment : IBlockEnvironment
{
    public required IBlock Block { get; init; }
    
    public List<BlockEnvironment> Parents { get; } = new();
    public List<BlockEnvironment> Children { get; } = new();
    public List<BlockEnvironment> ChildrenSubtree { get; } = new();
    public List<BlockJoint> LeftJoints { get; } = new();
    public List<BlockJoint> RightJoints { get; } = new();

    IReadOnlyList<IBlockEnvironment> IBlockEnvironment.Parents => Parents;
    IReadOnlyList<IBlockEnvironment> IBlockEnvironment.Children => Children;
    IReadOnlyList<IBlockEnvironment> IBlockEnvironment.ChildrenSubtree => ChildrenSubtree;
    IReadOnlyList<IBlockJoint> IBlockEnvironment.LeftJoints => LeftJoints;
    IReadOnlyList<IBlockJoint> IBlockEnvironment.RightJoints => RightJoints;
}