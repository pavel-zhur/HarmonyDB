import type { IIndexedBlock } from '../Blocks/Interfaces/IIndexedBlock';
import { BlockType, blockTypeToString } from '../Enums/BlockType';
import type { BlockEnvironment } from './BlockEnvironment';
import type { IBlockJoint } from './Interfaces/IBlockJoint';

export class BlockJoint implements IBlockJoint {
    constructor(init: { block1: BlockEnvironment; block2: BlockEnvironment }) {
        this.block1 = init.block1;
        this.block2 = init.block2;
    }

    readonly block1: BlockEnvironment;

    readonly block2: BlockEnvironment;

    get overlapLength(): number {
        return BlockJoint.getOverlapLength(this.block1.block, this.block2.block);
    }

    /**
     * The C# is `block1.EndIndex - block2.StartIndex switch { var x and >= 0 => x, _ => throw ... }`: the switch binds
     * tighter than the minus, so it is block2.StartIndex that may not be negative, and the result may be.
     */
    static getOverlapLength(block1: IIndexedBlock, block2: IIndexedBlock): number {
        const x = block2.startIndex;
        if (!(x >= 0)) throw new Error('The overlap cannot be negative.');
        return block1.endIndex - x;
    }

    static getNormalization(block1: IIndexedBlock, block2: IIndexedBlock): string {
        return `${blockTypeToString(block1.type)}, `
            + `${blockTypeToString(block2.type)}, `
            + `${block1.normalized}, `
            + `${block2.normalized}, `
            + `${BlockJoint.getOverlapLength(block1, block2)}, `
            + `${block1.getNormalizedCoordinate(block2.startIndex - 1) ?? ''}, `
            + `${block2.getNormalizedCoordinate(block1.endIndex + 1) ?? ''}`;
    }

    get normalization(): string {
        return BlockJoint.getNormalization(this.block1.block, this.block2.block);
    }

    get isEdge(): boolean {
        return this.block1.block.type === BlockType.SequenceStart || this.block1.block.type === BlockType.SequenceEnd
            || this.block2.block.type === BlockType.SequenceStart || this.block2.block.type === BlockType.SequenceEnd;
    }
}
