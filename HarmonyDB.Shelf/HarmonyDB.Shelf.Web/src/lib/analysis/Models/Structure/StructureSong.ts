import type { IValueEquatable } from '../../../common/dotnet/Equality';
import { keyOf } from '../../../common/dotnet/Equality';

/** A positional record: equal by its members. */
export class StructureSong implements IValueEquatable {
    constructor(
        readonly externalId: string,
        readonly totalLoops: number,
    ) {}

    equalityKey(): string {
        return [this.externalId, this.totalLoops].map(keyOf).join('|');
    }
}
