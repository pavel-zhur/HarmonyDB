import { KeyNotFoundException } from '../../common/dotnet/exceptions';
import { compareNumbers, single } from '../../common/dotnet/Linq';
import { toFloat } from '../../common/dotnet/Numbers';
import { PriorityQueue } from '../../common/dotnet/PriorityQueue';
import type { IIndexedBlock } from '../Models/Index/Blocks/Interfaces/IIndexedBlock';
import { indexedBlockScore } from '../Models/Index/Blocks/Interfaces/IIndexedBlock';
import { BlockType } from '../Models/Index/Enums/BlockType';
import type { BlockGraph } from '../Models/Index/Graphs/BlockGraph';
import type { IBlockJoint } from '../Models/Index/Graphs/Interfaces/IBlockJoint';
import { blockJointScore } from '../Models/Index/Graphs/Interfaces/IBlockJoint';

/** float.MaxValue */
const floatMaxValue = 3.4028234663852886e38;

function getOrThrow<K, V>(dictionary: ReadonlyMap<K, V>, key: K): V {
    if (!dictionary.has(key)) throw new KeyNotFoundException(key);
    return dictionary.get(key)!;
}

export class PathsSearcher {
    dijkstra(graph: BlockGraph, startBlock: IIndexedBlock | null = null, targetBlock: IIndexedBlock | null = null): IBlockJoint[] {
        startBlock ??= single(graph.environments.values(), x => x.block.type === BlockType.SequenceStart).block;
        targetBlock ??= single(graph.environments.values(), x => x.block.type === BlockType.SequenceEnd).block;

        const distances = new Map<IIndexedBlock, number>();
        const previousJoints = new Map<IIndexedBlock, IBlockJoint | null>();

        for (const block of graph.environments.keys()) {
            distances.set(block, floatMaxValue); // Initialize all distances to infinity
            previousJoints.set(block, null); // Initialize all previous joints to null
        }

        distances.set(startBlock, 0); // Distance to start block is 0

        const priorityQueue = new PriorityQueue<IIndexedBlock, number>(compareNumbers);
        priorityQueue.enqueue(startBlock, 0);

        while (priorityQueue.count !== 0) {
            const currentBlock = priorityQueue.dequeue();

            if (currentBlock === targetBlock)
                break;

            const currentEnvironment = getOrThrow(graph.environments, currentBlock);
            const neighbors = currentEnvironment.rightJoints;

            for (const joint of neighbors) {
                const neighborBlock = joint.block2.block;
                const alt = toFloat(toFloat(getOrThrow(distances, currentBlock) + indexedBlockScore(neighborBlock)) + blockJointScore(joint));
                if (alt < getOrThrow(distances, neighborBlock)) {
                    distances.set(neighborBlock, alt);
                    previousJoints.set(neighborBlock, joint);
                    priorityQueue.enqueue(neighborBlock, alt);
                }
            }
        }

        return this.reconstructPath(previousJoints, targetBlock);
    }

    private reconstructPath(previousJoints: Map<IIndexedBlock, IBlockJoint | null>, targetBlock: IIndexedBlock): IBlockJoint[] {
        const pathJoints: IBlockJoint[] = [];
        let current = targetBlock;

        let joint: IBlockJoint | null;
        while ((joint = getOrThrow(previousJoints, current)) !== null) {
            pathJoints.push(joint);
            current = joint.block1.block; // Move to the previous block in the path
        }

        pathJoints.reverse(); // The joints were added from target to start, so we need to reverse them
        if (pathJoints.length > 0) return pathJoints;
        throw new Error('The path from start to end is not found');
    }
}
