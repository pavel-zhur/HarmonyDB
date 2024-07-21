using HarmonyDB.Index.Analysis.Models.Index.Blocks.Interfaces;
using HarmonyDB.Index.Analysis.Models.Index.Enums;
using HarmonyDB.Index.Analysis.Models.Index.Graphs.Interfaces;

namespace HarmonyDB.Index.Analysis.Models.Index.Graphs;

public class BlockEnvironment : IBlockEnvironment
{
    public required IBlock Block { get; init; }
    
    public List<BlockEnvironment> Parents { get; } = new();
    public List<BlockEnvironment> Children { get; } = new();
    public List<BlockEnvironment> ChildrenSubtree { get; } = new();
    public List<BlockJoint> LeftJoints { get; } = new();
    public List<BlockJoint> RightJoints { get; } = new();
    
    public BlockDetections Detections { get; init; }

    IReadOnlyList<IBlockEnvironment> IBlockEnvironment.Parents => Parents;
    IReadOnlyList<IBlockEnvironment> IBlockEnvironment.Children => Children;
    IReadOnlyList<IBlockEnvironment> IBlockEnvironment.ChildrenSubtree => ChildrenSubtree;
    IReadOnlyList<IBlockJoint> IBlockEnvironment.LeftJoints => LeftJoints;
    IReadOnlyList<IBlockJoint> IBlockEnvironment.RightJoints => RightJoints;
}