import { ChordType } from './ChordType';
import type { ChordDataV1 } from './V1/ChordDataV1';
import type { HarmonyDataV1 } from './V1/HarmonyDataV1';

export class HarmonyGroup {
    constructor(init: {
        index: number;
        originalSequence: readonly ChordDataV1[];
        harmonyData: HarmonyDataV1 | null;
        startChordIndex: number;
        endChordIndex: number;
        harmonyRepresentation: string;
    }) {
        this.index = init.index;
        this.originalSequence = init.originalSequence;
        this.harmonyData = init.harmonyData;
        this.startChordIndex = init.startChordIndex;
        this.endChordIndex = init.endChordIndex;
        this.harmonyRepresentation = init.harmonyRepresentation;
    }

    readonly index: number;

    readonly originalSequence: readonly ChordDataV1[];

    readonly harmonyData: HarmonyDataV1 | null;

    readonly startChordIndex: number;

    readonly endChordIndex: number;

    get isStandard(): boolean {
        return this.harmonyData?.chordType === ChordType.Major || this.harmonyData?.chordType === ChordType.Minor;
    }

    readonly harmonyRepresentation: string;
}
