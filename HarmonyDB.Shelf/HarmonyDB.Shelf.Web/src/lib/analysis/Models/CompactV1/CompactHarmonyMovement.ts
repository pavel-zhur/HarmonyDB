import type { IValueEquatable } from '../../../common/dotnet/Equality';
import { range } from '../../../common/dotnet/Linq';
import { intMod } from '../../../common/dotnet/Numbers';
import { once } from '../../../common/OneShelf.Common/Extensions';
import type { ChordType } from '../ChordType';
import type { ISearchableHarmonyMovement } from '../Interfaces/ISearchableHarmonyMovement';

/** A record struct: equal by its members. A ReadOnlyMemory<CompactHarmonyMovement> is a readonly array of them. */
export class CompactHarmonyMovement implements ISearchableHarmonyMovement, IValueEquatable {
    constructor(init: { rootDelta: number; fromType: ChordType; toType: ChordType }) {
        this.rootDelta = init.rootDelta;
        this.fromType = init.fromType;
        this.toType = init.toType;
    }

    readonly rootDelta: number;

    readonly fromType: ChordType;

    readonly toType: ChordType;

    equalityKey(): string {
        return `${this.rootDelta}|${this.fromType}|${this.toType}`;
    }

    /** The record struct's Equals(CompactHarmonyMovement). */
    equals(other: CompactHarmonyMovement): boolean {
        return this.rootDelta === other.rootDelta && this.fromType === other.fromType && this.toType === other.toType;
    }

    /** The static Equals(ref readonly first, ref readonly second). */
    static equals(first: CompactHarmonyMovement, second: CompactHarmonyMovement): boolean {
        return first.equals(second);
    }

    static areNormalizedEqual(progression1: readonly CompactHarmonyMovement[], progression2: readonly CompactHarmonyMovement[]): boolean {
        const result = CompactHarmonyMovement.areEqual(progression1, progression2, null, 0);
        switch (result) {
            case 0:
                return true;
            case null:
                return false;
            default:
                throw new Error('Could not have happened.');
        }
    }

    static areEqual(
        progression1: readonly CompactHarmonyMovement[],
        progression2: readonly CompactHarmonyMovement[],
        shifts: number | null = null,
        fixedShift: number | null = null,
    ): number | null {
        const length = progression1.length;
        if (length !== progression2.length) return null;

        shifts ??= length;

        for (const shift of fixedShift !== null ? once(fixedShift) : range(0, shifts)) {
            let failed = false;
            for (let i = 0; i < length; i++) {
                const j = intMod(i + shift, length);
                if (!CompactHarmonyMovement.equals(progression2[j]!, progression1[i]!)) {
                    failed = true;
                    break;
                }
            }

            if (!failed) {
                return shift;
            }
        }

        return null;
    }
}
