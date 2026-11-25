// PriorityQueue<TElement, TPriority> with the default comparer: .NET's quaternary min-heap. It is not stable: which of
// the elements with equal priorities comes out first depends on this very algorithm, so it is reproduced step by step.

import { InvalidOperationException } from './exceptions';
import type { Comparison } from './Linq';

const log2Arity = 2;
const arity = 4;

export class PriorityQueue<TElement, TPriority> {
    private readonly nodes: [TElement, TPriority][] = [];
    private size = 0;

    constructor(private readonly comparison: Comparison<TPriority>) {}

    get count(): number {
        return this.size;
    }

    enqueue(element: TElement, priority: TPriority): void {
        const currentSize = this.size;
        this.size = currentSize + 1;
        this.moveUp([element, priority], currentSize);
    }

    dequeue(): TElement {
        if (this.size === 0) throw new InvalidOperationException('Queue empty.');
        const element = this.nodes[0]![0];
        this.removeRootNode();
        return element;
    }

    private removeRootNode(): void {
        const lastNodeIndex = --this.size;
        if (lastNodeIndex > 0) {
            const lastNode = this.nodes[lastNodeIndex]!;
            this.moveDown(lastNode, 0);
        }

        this.nodes.length = this.size;
    }

    private static getParentIndex(index: number): number {
        return (index - 1) >> log2Arity;
    }

    private static getFirstChildIndex(index: number): number {
        return (index << log2Arity) + 1;
    }

    private moveUp(node: [TElement, TPriority], nodeIndex: number): void {
        const nodes = this.nodes;
        while (nodeIndex > 0) {
            const parentIndex = PriorityQueue.getParentIndex(nodeIndex);
            const parent = nodes[parentIndex]!;
            if (this.comparison(node[1], parent[1]) < 0) {
                nodes[nodeIndex] = parent;
                nodeIndex = parentIndex;
            } else {
                break;
            }
        }

        nodes[nodeIndex] = node;
    }

    private moveDown(node: [TElement, TPriority], nodeIndex: number): void {
        const nodes = this.nodes;
        const size = this.size;
        let i: number;
        while ((i = PriorityQueue.getFirstChildIndex(nodeIndex)) < size) {
            // Find the child node with the minimal priority
            let minChild = nodes[i]!;
            let minChildIndex = i;

            const childIndexUpperBound = Math.min(i + arity, size);
            while (++i < childIndexUpperBound) {
                const nextChild = nodes[i]!;
                if (this.comparison(nextChild[1], minChild[1]) < 0) {
                    minChild = nextChild;
                    minChildIndex = i;
                }
            }

            // Heap property is satisfied; insert node in this location.
            if (this.comparison(node[1], minChild[1]) <= 0) {
                break;
            }

            // Move the minimal child up by one node and continue recursively from its location.
            nodes[nodeIndex] = minChild;
            nodeIndex = minChildIndex;
        }

        nodes[nodeIndex] = node;
    }
}
