using HarmonyDB.Index.Analysis.Models.Index.Blocks.Interfaces;
using HarmonyDB.Index.Analysis.Models.Index.Enums;
using HarmonyDB.Index.Analysis.Models.Index.Graphs;

namespace HarmonyDB.Index.Analysis.Services;

public class Dijkstra
{
    public List<IIndexedBlock> GetShortestPath(BlockGraph graph)
    {
        var startBlock = graph.Environments.Values.Single(x => x.Block.Type == IndexedBlockType.SequenceStart).Block;
        var targetBlock = graph.Environments.Values.Single(x => x.Block.Type == IndexedBlockType.SequenceEnd).Block;

        var distances = new Dictionary<IIndexedBlock, float>();
        var previousBlocks = new Dictionary<IIndexedBlock, IIndexedBlock?>();

        foreach (var block in graph.Environments.Keys)
        {
            distances[block] = float.MaxValue; // Initialize all distances to infinity
            previousBlocks[block] = null; // Initialize all previous blocks to null
        }

        distances[startBlock] = 0; // Distance to start block is 0

        var priorityQueue = new PriorityQueue<IIndexedBlock, float>();
        priorityQueue.Enqueue(startBlock, 0);

        while (priorityQueue.Count != 0)
        {
            var currentBlock = priorityQueue.Dequeue();

            if (currentBlock == targetBlock)
                break;

            var currentEnvironment = graph.Environments[currentBlock];
            var neighbors = currentEnvironment.LeftJoints.Select(joint => joint.Block2.Block)
                .Concat(currentEnvironment.RightJoints.Select(joint => joint.Block2.Block))
                .Distinct();

            foreach (var neighbor in neighbors)
            {
                var alt = distances[currentBlock] + neighbor.Score;
                if (alt < distances[neighbor])
                {
                    distances[neighbor] = alt;
                    previousBlocks[neighbor] = currentBlock;
                    priorityQueue.Enqueue(neighbor, alt);
                }
            }
        }

        return ReconstructPath(previousBlocks, startBlock, targetBlock);
    }

    private List<IIndexedBlock> ReconstructPath(Dictionary<IIndexedBlock, IIndexedBlock?> previousBlocks, IIndexedBlock startBlock, IIndexedBlock targetBlock)
    {
        var path = new List<IIndexedBlock>();
        for (var at = targetBlock; at != null; at = previousBlocks[at])
        {
            path.Add(at);
        }
        path.Reverse();
        return path.Contains(startBlock) ? path : throw new("The path from start to end is not found");
    }
}
