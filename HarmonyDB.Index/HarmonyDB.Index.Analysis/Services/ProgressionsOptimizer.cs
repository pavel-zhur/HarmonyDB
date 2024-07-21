using HarmonyDB.Index.Analysis.Models.Index.Blocks.Interfaces;
using HarmonyDB.Index.Analysis.Models.Index.Graphs;

namespace HarmonyDB.Index.Analysis.Services;

public class ProgressionsOptimizer
{
    public List<List<IIndexedBlock>> GetAllPossiblePaths(BlockGraph graph)
    {
        var exitsMemory = new Dictionary<IIndexedBlock, List<List<IIndexedBlock>>>();

        var starts = graph.Environments.Where(e => !e.LeftJoints.Any()).Select(e => e.Block).OfType<IIndexedBlock>().ToList();
        
        foreach (var block in starts)
        {
            Traverse(graph, block, exitsMemory);
        }

        return starts.SelectMany(s => exitsMemory[s].Select(e => e.Prepend(s).ToList())).ToList();
    }

    private void Traverse(BlockGraph graph, IIndexedBlock block, Dictionary<IIndexedBlock, List<List<IIndexedBlock>>> exitsMemory)
    {
        if (exitsMemory.ContainsKey(block)) return;

        var environment = graph.EnvironmentsByBlock[block];

        if (!environment.RightJoints.Any())
        {
            exitsMemory[block] = [[]];
            return;
        }

        var nextBlocks = environment.RightJoints.Select(x => x.Block2.Block).OfType<IIndexedBlock>().ToList();
        
        foreach (var next in nextBlocks)
        {
            Traverse(graph, next, exitsMemory);
        }

        exitsMemory[block] = nextBlocks.SelectMany(x => exitsMemory[x].Select(e => e.Prepend(x).ToList())).ToList();
    }
}