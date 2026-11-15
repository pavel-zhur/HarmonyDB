import { PolyLoopBlock } from '../PolyLoopBlock';
import { PolySequenceBlock } from '../PolySequenceBlock';
import type { IBlock } from './IBlock';
import type { IIndexedBlock } from './IIndexedBlock';

export interface IPolyBlock extends IIndexedBlock {
    /**
     * Whether self overlaps were detected among blocks with the same {@link IIndexedBlock.normalized} and {@link IIndexedBlock.normalizationRoot}.
     */
    selfOverlapsDetected: boolean;
}

/** `block is IPolyBlock`: the classes that implement it. */
export function isIPolyBlock(block: IBlock): block is IPolyBlock {
    return block instanceof PolyLoopBlock || block instanceof PolySequenceBlock;
}
