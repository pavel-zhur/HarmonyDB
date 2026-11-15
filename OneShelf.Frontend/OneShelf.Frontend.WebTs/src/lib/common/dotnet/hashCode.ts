// GetHashCode of the .NET primitive types, for the DeepHash methods of the models.
// Numbers hash exactly as in .NET. Strings (and what .NET hashes as strings) hash with .NET's own algorithm, Marvin,
// but with a fixed seed: .NET seeds it at random in each process, so no two runs agree on a string's hash, and the
// original only compares DeepHash sequences (and keys hashes) within one run. What it relies on is the quality of the
// hash — a well-mixed 32-bit value, so that different strings practically never collide — and that it gives.

import { toUpperInvariant } from './text';

/** int.GetHashCode (also byte, and an enum's underlying int). */
export function int32HashCode(value: number): number {
    return value | 0;
}

/** long.GetHashCode: (int)value ^ (int)(value >> 32). */
export function int64HashCode(value: number): number {
    const v = BigInt.asIntN(64, BigInt(value));
    return Number(BigInt.asIntN(32, v) ^ BigInt.asIntN(32, v >> 32n));
}

/** long.GetHashCode for a value already held as 64-bit ticks. */
export function bigInt64HashCode(value: bigint): number {
    const v = BigInt.asIntN(64, value);
    return Number(BigInt.asIntN(32, v) ^ BigInt.asIntN(32, v >> 32n));
}

const floatView = new DataView(new ArrayBuffer(4));

/** float.GetHashCode: the bits, with 0/-0 and all NaNs normalized. */
export function singleHashCode(value: number): number {
    floatView.setFloat32(0, value);
    let bits = floatView.getInt32(0);
    // NaNs and zeros only, as .NET 9's float.GetHashCode (an infinity keeps its sign).
    if ((((bits - 1) & 0x7fffffff) >>> 0) >= 0x7f800000) {
        bits &= 0x7f800000;
    }
    return bits;
}

/** The fixed seed of string hashes here (Marvin.DefaultSeed, which .NET makes at random): its low and high halves. */
export const marvinSeed = { p0: 0x9e3779b9, p1: 0x7f4a7c15 } as const;

/**
 * Marvin.ComputeHash32 over the UTF-16 code units of a string (little-endian bytes, as .NET hashes a string): the
 * data read as 32-bit words, each mixed in with the block function; the tail padded with 0x80 as there.
 */
export function marvinHash32(value: string, p0: number, p1: number): number {
    const length = value.length;
    let s0 = p0 | 0;
    let s1 = p1 | 0;
    const block = () => {
        s1 ^= s0;
        s0 = ((s0 << 20) | (s0 >>> 12)) + s1 | 0;
        s1 = (s1 << 9) | (s1 >>> 23);
        s1 ^= s0;
        s0 = ((s0 << 27) | (s0 >>> 5)) + s1 | 0;
        s1 = (s1 << 19) | (s1 >>> 13);
    };

    // Two code units make one 32-bit word.
    let i = 0;
    for (; i + 1 < length; i += 2) {
        s0 = s0 + (value.charCodeAt(i) | (value.charCodeAt(i + 1) << 16)) | 0;
        block();
    }

    // The final word: the code unit left over (if any), then the 0x80 byte.
    s0 = s0 + (i < length ? value.charCodeAt(i) | 0x800000 : 0x80) | 0;
    block();
    block();
    return (s1 ^ s0) | 0;
}

/** string.GetHashCode (Marvin with the fixed seed, see above). */
export function stringHashCode(value: string): number {
    return marvinHash32(value, marvinSeed.p0, marvinSeed.p1);
}

/** StringComparer.OrdinalIgnoreCase.GetHashCode: the hash of the string upper-cased as OrdinalIgnoreCase compares. */
export function stringHashCodeOrdinalIgnoreCase(value: string): number {
    return stringHashCode(toUpperInvariant(value));
}
