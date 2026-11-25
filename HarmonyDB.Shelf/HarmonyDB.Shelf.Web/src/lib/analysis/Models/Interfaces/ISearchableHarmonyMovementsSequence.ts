import type { CompactHarmonyMovement } from '../CompactV1/CompactHarmonyMovement';

export interface ISearchableHarmonyMovementsSequence {
    readonly firstMovementFromIndex: number;

    readonly movementsCount: number;

    readonly firstRoot: number;

    getMovement(index: number): CompactHarmonyMovement;
}
