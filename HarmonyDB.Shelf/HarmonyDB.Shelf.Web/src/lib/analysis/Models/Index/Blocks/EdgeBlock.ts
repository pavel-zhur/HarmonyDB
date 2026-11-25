import { ArgumentOutOfRangeException } from '../../../../common/dotnet/exceptions';
import { BlockType, blockTypeToString } from '../Enums/BlockType';
import type { IIndexedBlock } from './Interfaces/IIndexedBlock';

export class EdgeBlock implements IIndexedBlock {
    constructor(type: BlockType, sequenceLength: number) {
        this.normalized = blockTypeToString(type);
        this.type = type;

        switch (type) {
            case BlockType.SequenceStart:
                [this.startIndex, this.endIndex] = [-1, -1];
                break;
            case BlockType.SequenceEnd:
                [this.startIndex, this.endIndex] = [sequenceLength, sequenceLength];
                break;
            default:
                throw new ArgumentOutOfRangeException(undefined, `(Parameter 'type')\nActual value was ${blockTypeToString(type)}.`);
        }
    }

    readonly normalized: string;

    get normalizationRoot(): number {
        return 0;
    }

    readonly startIndex: number;

    readonly endIndex: number;

    get blockLength(): number {
        return 1;
    }

    get children(): readonly IIndexedBlock[] {
        return [];
    }

    getNormalizedCoordinate(_index: number): string | null {
        return null; // song start blocks do not have normalization shifts
    }

    readonly type: BlockType;
}
