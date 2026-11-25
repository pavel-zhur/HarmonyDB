import { at } from '../../../common/dotnet/Linq';
import type { ISearchableHarmonyMovementsSequence } from '../Interfaces/ISearchableHarmonyMovementsSequence';
import type { CompactHarmonyMovement } from './CompactHarmonyMovement';

export class CompactHarmonyMovementsSequence implements ISearchableHarmonyMovementsSequence {
    constructor(init: { firstMovementFromIndex: number; firstRoot: number; movements: readonly CompactHarmonyMovement[] }) {
        this.firstMovementFromIndex = init.firstMovementFromIndex;
        this.firstRoot = init.firstRoot;
        this.movements = init.movements;
    }

    readonly firstMovementFromIndex: number;

    readonly firstRoot: number;

    readonly movements: readonly CompactHarmonyMovement[];

    // ISearchableHarmonyMovementsSequence.MovementsCount
    get movementsCount(): number {
        return this.movements.length;
    }

    // ISearchableHarmonyMovementsSequence.GetMovement
    getMovement(index: number): CompactHarmonyMovement {
        return at(this.movements, index);
    }
}
