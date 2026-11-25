// Convert.ToBase64String / Convert.FromBase64String. atob is more lenient than .NET (it accepts missing padding), so the
// decoding follows .NET: white space (' ', '\t', '\r', '\n') is ignored, the length must be a multiple of four, '=' may
// only end the string (at most two), any other character is a FormatException.

import { FormatException } from './exceptions';

const alphabet = 'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/';
const values = new Map<string, number>([...alphabet].map((c, i) => [c, i]));

export function toBase64String(bytes: Uint8Array): string {
    let result = '';
    let i = 0;
    for (; i + 2 < bytes.length; i += 3) {
        const n = (bytes[i]! << 16) | (bytes[i + 1]! << 8) | bytes[i + 2]!;
        result += alphabet[(n >> 18) & 63]! + alphabet[(n >> 12) & 63]! + alphabet[(n >> 6) & 63]! + alphabet[n & 63]!;
    }

    const rest = bytes.length - i;
    if (rest === 1) {
        const n = bytes[i]! << 16;
        result += alphabet[(n >> 18) & 63]! + alphabet[(n >> 12) & 63]! + '==';
    } else if (rest === 2) {
        const n = (bytes[i]! << 16) | (bytes[i + 1]! << 8);
        result += alphabet[(n >> 18) & 63]! + alphabet[(n >> 12) & 63]! + alphabet[(n >> 6) & 63]! + '=';
    }

    return result;
}

const invalid = () => new FormatException('The input is not a valid Base-64 string as it contains a non-base 64 character, more than two padding characters, or an illegal character among the padding characters.');

export function fromBase64String(s: string): Uint8Array {
    const chars = [...s].filter(c => c !== ' ' && c !== '\t' && c !== '\r' && c !== '\n');
    if (chars.length % 4 !== 0) throw invalid();

    let padding = 0;
    while (padding < chars.length && chars[chars.length - 1 - padding] === '=') padding++;
    if (padding > 2) throw invalid();

    const data = chars.slice(0, chars.length - padding);
    const sextets = data.map(c => {
        const value = values.get(c);
        if (value === undefined) throw invalid();
        return value;
    });

    const result = new Uint8Array((chars.length / 4) * 3 - padding);
    let o = 0;
    for (let i = 0; i < sextets.length; i += 4) {
        const n = (sextets[i]! << 18) | ((sextets[i + 1] ?? 0) << 12) | ((sextets[i + 2] ?? 0) << 6) | (sextets[i + 3] ?? 0);
        if (o < result.length) result[o++] = (n >> 16) & 0xff;
        if (o < result.length) result[o++] = (n >> 8) & 0xff;
        if (o < result.length) result[o++] = n & 0xff;
    }

    return result;
}
