import type { IValueEquatable } from '../../../common/dotnet/Equality';
import { keyOf } from '../../../common/dotnet/Equality';
import { ChordType } from '../ChordType';
import type { HarmonyDataV1 } from './HarmonyDataV1';

/** A record: equal by its members. */
export class ChordDataV1 implements IValueEquatable {
    constructor(init: { originalRepresentation: string; harmonyRepresentation: string; harmonyData?: HarmonyDataV1 | null; chordData: string }) {
        this.originalRepresentation = init.originalRepresentation;
        this.harmonyRepresentation = init.harmonyRepresentation;
        this.harmonyData = init.harmonyData ?? null;
        this.chordData = init.chordData;
    }

    readonly originalRepresentation: string;

    readonly harmonyRepresentation: string;

    readonly harmonyData: HarmonyDataV1 | null;

    readonly chordData: string;

    // [JsonIgnore]
    get isStandard(): boolean {
        return this.harmonyData?.chordType === ChordType.Major || this.harmonyData?.chordType === ChordType.Minor;
    }

    equalityKey(): string {
        return `${keyOf(this.originalRepresentation)}|${keyOf(this.harmonyRepresentation)}|${keyOf(this.harmonyData)}|${keyOf(this.chordData)}`;
    }
}
