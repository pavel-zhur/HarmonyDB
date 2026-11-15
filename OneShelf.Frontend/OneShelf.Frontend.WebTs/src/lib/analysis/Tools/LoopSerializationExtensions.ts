import { fromBase64String, toBase64String } from '../../common/dotnet/Convert';
import type { ChordType } from '../Models/ChordType';
import { CompactHarmonyMovement } from '../Models/CompactV1/CompactHarmonyMovement';

export function deserializeLoop(progression: string): readonly CompactHarmonyMovement[] {
    const bytes = fromBase64String(progression);
    const result: CompactHarmonyMovement[] = [];
    const length = Math.trunc(bytes.length / 3);
    for (let i = 0; i < length; i++) {
        result.push(new CompactHarmonyMovement({
            rootDelta: bytes[i * 3]!,
            fromType: bytes[i * 3 + 1]! as ChordType,
            toType: bytes[i * 3 + 2]! as ChordType,
        }));
    }

    return result;
}

export function serializeLoop(progression: readonly CompactHarmonyMovement[]): string {
    const bytes = new Uint8Array(progression.length * 3);
    for (let index = 0; index < progression.length; index++) {
        bytes[index * 3] = progression[index]!.rootDelta;
        bytes[index * 3 + 1] = progression[index]!.fromType;
        bytes[index * 3 + 2] = progression[index]!.toType;
    }

    return toBase64String(bytes);
}
