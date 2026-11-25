import { ArgumentOutOfRangeException } from '../../../../common/dotnet/exceptions';
import type { CompactHarmonyMovement } from '../../CompactV1/CompactHarmonyMovement';
import { BlockType } from '../Enums/BlockType';
import type { IIndexedBlock } from './Interfaces/IIndexedBlock';

export class SequenceBlock implements IIndexedBlock {
    constructor(init: { sequence: readonly CompactHarmonyMovement[]; normalized: string; normalizationRoot: number; startIndex: number; endIndex: number }) {
        this.sequence = init.sequence;
        this.normalized = init.normalized;
        this.normalizationRoot = init.normalizationRoot;
        this.startIndex = init.startIndex;
        this.endIndex = init.endIndex;
    }

    readonly sequence: readonly CompactHarmonyMovement[];

    readonly normalized: string;

    /**
     * Corresponds to the key (it is different for the same normalized progression if and only if it is in different keys).
     * More precisely, it is the root of the first movement of the normalized sequence, wherever its corresponding original sequence movement is.
     */
    readonly normalizationRoot: number;

    readonly startIndex: number;

    readonly endIndex: number;

    get blockLength(): number {
        return this.endIndex - this.startIndex + 1;
    }

    get children(): readonly IIndexedBlock[] {
        return [];
    }

    getNormalizedCoordinate(index: number): string | null {
        if (index < this.startIndex || index > this.endIndex) throw new ArgumentOutOfRangeException(undefined, "(Parameter 'index')");
        return null; // sequence blocks do not have normalization shifts
    }

    get type(): BlockType {
        return BlockType.Sequence;
    }
}
