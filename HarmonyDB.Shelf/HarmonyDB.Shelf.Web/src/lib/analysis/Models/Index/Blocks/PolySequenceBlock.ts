import { ArgumentOutOfRangeException } from '../../../../common/dotnet/exceptions';
import type { CompactHarmonyMovement } from '../../CompactV1/CompactHarmonyMovement';
import { BlockType } from '../Enums/BlockType';
import type { IPolyBlock } from './Interfaces/IPolyBlock';

export class PolySequenceBlock implements IPolyBlock {
    /** `children` is an init-only (not required) property in the C#; every place that creates the block sets it. */
    constructor(init: { sequence: readonly CompactHarmonyMovement[]; normalized: string; normalizationRoot: number; startIndex: number; endIndex: number; children: PolySequenceBlock[] }) {
        this.sequence = init.sequence;
        this.normalized = init.normalized;
        this.normalizationRoot = init.normalizationRoot;
        this.startIndex = init.startIndex;
        this.endIndex = init.endIndex;
        this.children = init.children;
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

    readonly children: readonly PolySequenceBlock[];

    getNormalizedCoordinate(index: number): string | null {
        if (index < this.startIndex || index > this.endIndex) throw new ArgumentOutOfRangeException(undefined, "(Parameter 'index')");
        return null; // sequence blocks do not have normalization shifts
    }

    get type(): BlockType {
        return BlockType.PolySequence;
    }

    /**
     * Whether self overlaps were detected among blocks with the same {@link normalized} and {@link normalizationRoot}.
     */
    selfOverlapsDetected = false;
}
