import { CompactChordsProgression } from './CompactV1/CompactChordsProgression';
import type { HarmonyGroup } from './HarmonyGroup';
import type { HarmonyMovementsSequence } from './HarmonyMovementsSequence';
import type { ISearchableChordsProgression } from './Interfaces/ISearchableChordsProgression';
import type { ChordDataV1 } from './V1/ChordDataV1';

/** (ChordDataV1 original, HarmonyGroup harmonyGroup, int indexInHarmonyGroup, HarmonyMovementsSequence? standardMovementsSequence, HarmonyMovementsSequence? extendedMovementsSequence) */
export interface OriginalSequenceItem {
    readonly original: ChordDataV1;
    readonly harmonyGroup: HarmonyGroup;
    readonly indexInHarmonyGroup: number;
    readonly standardMovementsSequence: HarmonyMovementsSequence | null;
    readonly extendedMovementsSequence: HarmonyMovementsSequence | null;
}

/** (HarmonyGroup harmonyGroup, HarmonyMovementsSequence? standardMovementsSequence, HarmonyMovementsSequence? extendedMovementsSequence) */
export interface HarmonySequenceItem {
    readonly harmonyGroup: HarmonyGroup;
    readonly standardMovementsSequence: HarmonyMovementsSequence | null;
    readonly extendedMovementsSequence: HarmonyMovementsSequence | null;
}

export class ChordsProgression implements ISearchableChordsProgression {
    constructor(init: {
        originalSequence: readonly OriginalSequenceItem[];
        harmonySequence: readonly HarmonySequenceItem[];
        standardHarmonyMovementsSequences: readonly HarmonyMovementsSequence[] | null;
        extendedHarmonyMovementsSequences: readonly HarmonyMovementsSequence[];
    }) {
        this.originalSequence = init.originalSequence;
        this.harmonySequence = init.harmonySequence;
        this.standardHarmonyMovementsSequences = init.standardHarmonyMovementsSequences;
        this.extendedHarmonyMovementsSequences = init.extendedHarmonyMovementsSequences;
    }

    readonly originalSequence: readonly OriginalSequenceItem[];

    readonly harmonySequence: readonly HarmonySequenceItem[];

    readonly standardHarmonyMovementsSequences: readonly HarmonyMovementsSequence[] | null;

    readonly extendedHarmonyMovementsSequences: readonly HarmonyMovementsSequence[];

    // ISearchableChordsProgression.HarmonySequenceLength
    get harmonySequenceLength(): number {
        return this.harmonySequence.length;
    }

    compact(): CompactChordsProgression {
        return new CompactChordsProgression({
            harmonySequenceLength: this.harmonySequence.length,
            extendedHarmonyMovementsSequences: this.extendedHarmonyMovementsSequences.map(x => x.compact()),
        });
    }
}
