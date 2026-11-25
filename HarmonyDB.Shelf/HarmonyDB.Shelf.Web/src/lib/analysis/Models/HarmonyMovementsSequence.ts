import { at, first } from '../../common/dotnet/Linq';
import type { CompactHarmonyMovement } from './CompactV1/CompactHarmonyMovement';
import { CompactHarmonyMovementsSequence } from './CompactV1/CompactHarmonyMovementsSequence';
import type { HarmonyMovement } from './HarmonyMovement';
import type { ISearchableHarmonyMovementsSequence } from './Interfaces/ISearchableHarmonyMovementsSequence';

export class HarmonyMovementsSequence implements ISearchableHarmonyMovementsSequence {
    constructor(init: { firstRoot: number; movements: readonly HarmonyMovement[] }) {
        this.firstRoot = init.firstRoot;
        this.movements = init.movements;
    }

    readonly firstRoot: number;

    readonly movements: readonly HarmonyMovement[];

    get firstMovementFromIndex(): number {
        return first(this.movements).from.index;
    }

    // ISearchableHarmonyMovementsSequence.MovementsCount
    get movementsCount(): number {
        return this.movements.length;
    }

    // ISearchableHarmonyMovementsSequence.GetMovement
    getMovement(index: number): CompactHarmonyMovement {
        return at(this.movements, index).compact();
    }

    compact(): CompactHarmonyMovementsSequence {
        return new CompactHarmonyMovementsSequence({
            movements: this.movements.map(x => x.compact()),
            firstMovementFromIndex: first(this.movements).from.index,
            firstRoot: this.firstRoot,
        });
    }
}
