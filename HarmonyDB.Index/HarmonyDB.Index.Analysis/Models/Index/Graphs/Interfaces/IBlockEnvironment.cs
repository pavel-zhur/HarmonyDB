using HarmonyDB.Index.Analysis.Models.Index.Blocks.Interfaces;
using HarmonyDB.Index.Analysis.Models.Index.Enums;

namespace HarmonyDB.Index.Analysis.Models.Index.Graphs.Interfaces;

public interface IBlockEnvironment
{
    IIndexedBlock Block { get; }
    IReadOnlyList<IBlockEnvironment> Parents { get; }
    IReadOnlyList<IBlockEnvironment> Children { get; }
    IReadOnlyList<IBlockEnvironment> ChildrenSubtree { get; }
    IReadOnlyList<IBlockJoint> LeftJoints { get; }
    IReadOnlyList<IBlockJoint> RightJoints { get; }
    BlockDetections Detections { get; }
}