namespace HarmonyDB.Index.Analysis.Models.Index.Graphs;

public interface IBlockEnvironment
{
    IBlock Block { get; init; }
    IReadOnlyList<IBlockEnvironment> Parents { get; }
    IReadOnlyList<IBlockEnvironment> Children { get; }
    IReadOnlyList<IBlockEnvironment> ChildrenSubtree { get; }
    IReadOnlyList<IBlockJoint> LeftJoints { get; }
    IReadOnlyList<IBlockJoint> RightJoints { get; }
}