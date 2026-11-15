import type { ChordType } from './ChordType';
import { CompactHarmonyMovement } from './CompactV1/CompactHarmonyMovement';
import type { HarmonyGroup } from './HarmonyGroup';
import type { ISearchableHarmonyMovement } from './Interfaces/ISearchableHarmonyMovement';

export class HarmonyMovement implements ISearchableHarmonyMovement {
    constructor(init: { from: HarmonyGroup; to: HarmonyGroup; rootDelta: number; fromType: ChordType; toType: ChordType; title: string }) {
        this.from = init.from;
        this.to = init.to;
        this.rootDelta = init.rootDelta;
        this.fromType = init.fromType;
        this.toType = init.toType;
        this.title = init.title;
    }

    readonly from: HarmonyGroup;

    readonly to: HarmonyGroup;

    readonly rootDelta: number;

    readonly fromType: ChordType;

    readonly toType: ChordType;

    readonly title: string;

    compact(): CompactHarmonyMovement {
        return new CompactHarmonyMovement({
            rootDelta: this.rootDelta,
            fromType: this.fromType,
            toType: this.toType,
        });
    }
}
