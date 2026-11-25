import { EdgeBlock } from '../EdgeBlock';
import { LoopBlockBase } from '../LoopBlockBase';
import { PingPongBlock } from '../PingPongBlock';
import { PolySequenceBlock } from '../PolySequenceBlock';
import { RoundRobinBlock } from '../RoundRobinBlock';
import { SequenceBlock } from '../SequenceBlock';
import type { IBlock } from './IBlock';

export interface IIndexedBlock extends IBlock {
    readonly children: readonly IIndexedBlock[];

    getNormalizedCoordinate(index: number): string | null;

    readonly normalized: string;

    readonly normalizationRoot: number;
}

/** The default interface member IIndexedBlock.Score ([Obsolete]). */
export function indexedBlockScore(_block: IIndexedBlock): number {
    return 1;
}

/** `block is IIndexedBlock`: the classes that implement it. */
export function isIIndexedBlock(block: IBlock): block is IIndexedBlock {
    return block instanceof EdgeBlock
        || block instanceof LoopBlockBase
        || block instanceof PingPongBlock
        || block instanceof PolySequenceBlock
        || block instanceof RoundRobinBlock
        || block instanceof SequenceBlock;
}
