using HarmonyDB.Index.Analysis.Models.Index.Blocks.Interfaces;
using HarmonyDB.Index.Analysis.Models.Index.Enums;
using HarmonyDB.Index.Analysis.Models.Index.Graphs;
using HarmonyDB.Index.Analysis.Models.Index.Graphs.Interfaces;

namespace HarmonyDB.Index.Analysis.Services;

public class PathsSearcher
{
    public List<IBlockJoint> Dijkstra(BlockGraph graph, IIndexedBlock? startBlock = null, IIndexedBlock? targetBlock = null)
    {
        startBlock ??= graph.Environments.Values.Single(x => x.Block.Type == BlockType.SequenceStart).Block;
        targetBlock ??= graph.Environments.Values.Single(x => x.Block.Type == BlockType.SequenceEnd).Block;

        var distances = new Dictionary<IIndexedBlock, float>();
        var previousJoints = new Dictionary<IIndexedBlock, IBlockJoint?>();

        foreach (var block in graph.Environments.Keys)
        {
            distances[block] = float.MaxValue; // Initialize all distances to infinity
            previousJoints[block] = null; // Initialize all previous joints to null
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
            var neighbors = currentEnvironment.RightJoints;

            foreach (var joint in neighbors)
            {
                var neighborBlock = joint.Block2.Block;
                var alt = distances[currentBlock] + neighborBlock.Score + joint.Score;
                if (alt < distances[neighborBlock])
                {
                    distances[neighborBlock] = alt;
                    previousJoints[neighborBlock] = joint;
                    priorityQueue.Enqueue(neighborBlock, alt);
                }
            }
        }

        return ReconstructPath(previousJoints, targetBlock);
    }

    private List<IBlockJoint> ReconstructPath(Dictionary<IIndexedBlock, IBlockJoint?> previousJoints, IIndexedBlock targetBlock)
    {
        var pathJoints = new List<IBlockJoint>();
        var current = targetBlock;

        while (previousJoints[current] is { } joint)
        {
            pathJoints.Add(joint);
            current = joint.Block1.Block; // Move to the previous block in the path
        }

        pathJoints.Reverse(); // The joints were added from target to start, so we need to reverse them
        return pathJoints.Count > 0 ? pathJoints : throw new("The path from start to end is not found");
    }
}
