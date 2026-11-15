import type { IValueEquatable } from '../../../common/dotnet/Equality';
import { keyOf } from '../../../common/dotnet/Equality';

/** A positional record: equal by its members. The float members hold single precision values. */
export class StructureLink implements IValueEquatable {
    constructor(
        readonly normalized: string,
        readonly externalId: string,
        /**
         * Corresponds to the key (it is different for the same normalized progression if and only if it is in different keys).
         * More precisely, it is the root of the first movement of the normalized sequence, wherever its corresponding original sequence movement is.
         */
        readonly normalizationRoot: number,
        readonly occurrences: number,
        readonly successions: number,
        readonly coverage: number,
    ) {}

    equalityKey(): string {
        return [this.normalized, this.externalId, this.normalizationRoot, this.occurrences, this.successions, this.coverage].map(keyOf).join('|');
    }
}
