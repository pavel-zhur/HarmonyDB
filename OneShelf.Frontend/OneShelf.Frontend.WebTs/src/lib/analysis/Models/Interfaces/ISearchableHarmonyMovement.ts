import type { ChordType } from '../ChordType';

export interface ISearchableHarmonyMovement {
    readonly rootDelta: number;

    readonly fromType: ChordType;

    readonly toType: ChordType;
}
