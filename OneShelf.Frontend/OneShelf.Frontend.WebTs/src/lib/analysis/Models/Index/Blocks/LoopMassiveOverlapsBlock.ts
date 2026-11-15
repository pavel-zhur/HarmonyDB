import { BlockType } from '../Enums/BlockType';
import type { IBlock } from './Interfaces/IBlock';
import type { LoopBlock } from './LoopBlock';

export class LoopMassiveOverlapsBlock implements IBlock {
    constructor(init: { internalLoops: readonly LoopBlock[]; startIndex: number; endIndex: number; edge1: LoopBlock; edge2: LoopBlock }) {
        this.internalLoops = init.internalLoops;
        this.startIndex = init.startIndex;
        this.endIndex = init.endIndex;
        this.edge1 = init.edge1;
        this.edge2 = init.edge2;
    }

    readonly internalLoops: readonly LoopBlock[];

    readonly startIndex: number;

    readonly endIndex: number;

    get blockLength(): number {
        return this.endIndex - this.startIndex + 1;
    }

    readonly edge1: LoopBlock;

    readonly edge2: LoopBlock;

    get type(): BlockType {
        return BlockType.MassiveOverlaps;
    }
}
