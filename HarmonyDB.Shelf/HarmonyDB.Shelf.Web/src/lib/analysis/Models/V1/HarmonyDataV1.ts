import type { IValueEquatable } from '../../../common/dotnet/Equality';
import { keyOf } from '../../../common/dotnet/Equality';
import type { NoteAlteration } from '../../../common/HarmonyDB.Common/NoteAlteration';
import type { ChordType } from '../ChordType';

/** A record: equal by its members. */
export class HarmonyDataV1 implements IValueEquatable {
    constructor(init: { root: number; alteration?: NoteAlteration | null; chordType: ChordType }) {
        this.root = init.root;
        this.alteration = init.alteration ?? null;
        this.chordType = init.chordType;
    }

    readonly root: number;

    readonly alteration: NoteAlteration | null;

    readonly chordType: ChordType;

    equalityKey(): string {
        return `${keyOf(this.root)}|${keyOf(this.alteration)}|${keyOf(this.chordType)}`;
    }
}
