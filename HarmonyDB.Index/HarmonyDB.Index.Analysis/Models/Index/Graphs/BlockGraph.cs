using HarmonyDB.Index.Analysis.Models.Index.Blocks.Interfaces;
using HarmonyDB.Index.Analysis.Models.Index.Graphs.Interfaces;

namespace HarmonyDB.Index.Analysis.Models.Index.Graphs;

public class BlockGraph
{
    public required IReadOnlyDictionary<IIndexedBlock, IBlockEnvironment> Environments { get; init; }
    
    public required IReadOnlyList<IBlockJoint> Joints { get; init; }
    
    public required ILookup<int, IIndexedBlock> BlocksAt { get; init; }

    public required ILookup<(int startIndex, int endIndex), IIndexedBlock> BlocksByPositions { get; init; }
}