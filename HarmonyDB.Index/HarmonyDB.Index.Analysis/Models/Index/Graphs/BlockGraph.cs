using HarmonyDB.Index.Analysis.Models.Index.Blocks.Interfaces;
using HarmonyDB.Index.Analysis.Models.Index.Graphs.Interfaces;

namespace HarmonyDB.Index.Analysis.Models.Index.Graphs;

public class BlockGraph
{
    public required IReadOnlyList<IBlockEnvironment> Environments { get; init; }
    
    public required IReadOnlyDictionary<IBlock, IBlockEnvironment> EnvironmentsByBlock { get; init; }
    
    public required IReadOnlyList<IBlockJoint> Joints { get; init; }
}