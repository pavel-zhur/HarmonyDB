import type { ISearchableHarmonyMovementsSequence } from './ISearchableHarmonyMovementsSequence';

export interface ISearchableChordsProgression {
    readonly harmonySequenceLength: number;

    readonly extendedHarmonyMovementsSequences: readonly ISearchableHarmonyMovementsSequence[];
}
