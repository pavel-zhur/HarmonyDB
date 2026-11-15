import type { IValueEquatable } from '../../../common/dotnet/Equality';
import { keyOf } from '../../../common/dotnet/Equality';

/** A positional record: equal by its members. The float members hold single precision values. */
export class StructureLoop implements IValueEquatable {
    constructor(
        readonly normalized: string,
        readonly length: number,
        readonly totalOccurrences: number,
        readonly totalSuccessions: number,
        readonly averageCoverage: number,
        readonly totalSongs: number,
    ) {}

    equalityKey(): string {
        return [this.normalized, this.length, this.totalOccurrences, this.totalSuccessions, this.averageCoverage, this.totalSongs].map(keyOf).join('|');
    }
}
