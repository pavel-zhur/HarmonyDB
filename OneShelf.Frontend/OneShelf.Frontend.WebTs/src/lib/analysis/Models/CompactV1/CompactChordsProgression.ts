import { BinaryReader, BinaryWriter } from '../../../common/dotnet/BinaryIO';
import { repeat } from '../../../common/dotnet/Linq';
import type { ChordType } from '../ChordType';
import type { ISearchableChordsProgression } from '../Interfaces/ISearchableChordsProgression';
import { CompactHarmonyMovement } from './CompactHarmonyMovement';
import { CompactHarmonyMovementsSequence } from './CompactHarmonyMovementsSequence';

/** BinaryData is a Uint8Array here. */
export class CompactChordsProgression implements ISearchableChordsProgression {
    constructor(init: { harmonySequenceLength: number; extendedHarmonyMovementsSequences: readonly CompactHarmonyMovementsSequence[] }) {
        this.harmonySequenceLength = init.harmonySequenceLength;
        this.extendedHarmonyMovementsSequences = init.extendedHarmonyMovementsSequences;
    }

    readonly harmonySequenceLength: number;

    readonly extendedHarmonyMovementsSequences: readonly CompactHarmonyMovementsSequence[];

    static deserialize(data: Uint8Array): CompactChordsProgression {
        const reader = new BinaryReader(data);
        const harmonySequenceLength = reader.readInt32();
        return new CompactChordsProgression({
            harmonySequenceLength,
            extendedHarmonyMovementsSequences: repeat(0, reader.readInt32())
                .map(() => {
                    const firstMovementFromIndex = reader.readInt32();
                    const firstRoot = reader.readByte();
                    return new CompactHarmonyMovementsSequence({
                        firstMovementFromIndex,
                        firstRoot,
                        movements: repeat(0, reader.readInt32())
                            .map(() => {
                                const rootDelta = reader.readByte();
                                const fromType = reader.readByte() as ChordType;
                                const toType = reader.readByte() as ChordType;
                                return new CompactHarmonyMovement({ rootDelta, fromType, toType });
                            }),
                    });
                }),
        });
    }

    serialize(): Uint8Array {
        const writer = new BinaryWriter();
        writer.writeInt32(this.harmonySequenceLength);
        writer.writeInt32(this.extendedHarmonyMovementsSequences.length);
        for (const extendedHarmonyMovementsSequence of this.extendedHarmonyMovementsSequences) {
            writer.writeInt32(extendedHarmonyMovementsSequence.firstMovementFromIndex);
            writer.writeByte(extendedHarmonyMovementsSequence.firstRoot);
            writer.writeInt32(extendedHarmonyMovementsSequence.movements.length);
            for (const compactHarmonyMovement of extendedHarmonyMovementsSequence.movements) {
                writer.writeByte(compactHarmonyMovement.rootDelta);
                writer.writeByte(compactHarmonyMovement.fromType);
                writer.writeByte(compactHarmonyMovement.toType);
            }
        }

        return writer.toArray();
    }
}
