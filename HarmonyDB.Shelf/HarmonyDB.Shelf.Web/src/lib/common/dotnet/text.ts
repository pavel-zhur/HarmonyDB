// .NET string and char semantics the ported code depends on, where JavaScript differs.
// The original site runs with <InvariantGlobalization>true</InvariantGlobalization>, so culture-sensitive
// comparisons (StartsWith(string), EndsWith(string), InvariantCulture*) behave ordinally there,
// and casing is the invariant simple (one char to one char) case mapping.

import { ArgumentException, ArgumentOutOfRangeException, FormatException, IndexOutOfRangeException, OverflowException } from './exceptions';
import { lowerRanges, upperRanges } from './invariantCasing';

const whiteSpace = /[\p{Zs}\p{Zl}\p{Zp}\t\n\v\f\r\u0085]/u;
const upper = /\p{Lu}/u;
const digit = /\p{Nd}/u;

/** char.IsWhiteSpace. Differs from JavaScript's \s: includes U+0085, excludes U+FEFF. */
export function isWhiteSpace(c: string): boolean {
    return whiteSpace.test(c);
}

/** char.IsUpper. */
export function isUpper(c: string): boolean {
    return upper.test(c);
}

/** char.IsDigit (any Unicode decimal digit, not only 0-9). */
export function isDigit(c: string): boolean {
    return digit.test(c);
}

/** string.IsNullOrWhiteSpace. */
export function isNullOrWhiteSpace(s: string | null | undefined): boolean {
    if (s == null) return true;
    for (let i = 0; i < s.length; i++) {
        if (!isWhiteSpace(s[i]!)) return false;
    }
    return true;
}

/** string.IsNullOrEmpty. */
export function isNullOrEmpty(s: string | null | undefined): boolean {
    return s == null || s.length === 0;
}

/** string.Trim() — trims char.IsWhiteSpace characters. */
export function trim(s: string): string {
    let start = 0;
    let end = s.length - 1;
    while (start <= end && isWhiteSpace(s[start]!)) start++;
    while (end >= start && isWhiteSpace(s[end]!)) end--;
    return s.substring(start, end + 1);
}

/** The code point a run table (start, count, step, delta) maps the code point to. */
function mapCodePoint(ranges: readonly number[], cp: number): number {
    let low = 0;
    let high = ranges.length / 4 - 1;
    while (low <= high) {
        const middle = (low + high) >> 1;
        const start = ranges[middle * 4]!;
        if (cp < start) {
            high = middle - 1;
            continue;
        }

        const count = ranges[middle * 4 + 1]!;
        const step = ranges[middle * 4 + 2]!;
        if (cp > start + (count - 1) * step) {
            low = middle + 1;
            continue;
        }

        return (cp - start) % step === 0 ? cp + ranges[middle * 4 + 3]! : cp;
    }

    return cp;
}

/** Maps each code point (a surrogate pair as one, a lone surrogate stays) by the table, as .NET's invariant casing does. */
function mapString(ranges: readonly number[], s: string): string {
    let result = '';
    let changed = false;
    for (let i = 0; i < s.length; i++) {
        const c = s.charCodeAt(i);
        let cp = c;
        if (c >= 0xd800 && c <= 0xdbff && i + 1 < s.length) {
            const next = s.charCodeAt(i + 1);
            if (next >= 0xdc00 && next <= 0xdfff) cp = (c - 0xd800) * 0x400 + (next - 0xdc00) + 0x10000;
        }

        const mapped = mapCodePoint(ranges, cp);
        if (mapped !== cp) changed = true;
        result += String.fromCodePoint(mapped);
        if (cp > 0xffff) i++;
    }

    return changed ? result : s;
}

/** char.ToLowerInvariant (a lone UTF-16 code unit). */
export function toLowerInvariantChar(c: string): string {
    const code = c.charCodeAt(0);
    return code >= 0xd800 && code <= 0xdfff ? c : String.fromCharCode(mapCodePoint(lowerRanges, code));
}

/**
 * string.ToLowerInvariant: .NET's invariant simple case mapping, code point by code point (one to one), from its own
 * table (invariantCasing.ts) rather than the browser's, which differs in places and with the Unicode version.
 */
export function toLowerInvariant(s: string): string {
    return mapString(lowerRanges, s);
}

/** string.ToUpperInvariant: as toLowerInvariant. */
export function toUpperInvariant(s: string): string {
    return mapString(upperRanges, s);
}
/** string.Substring(start) / Substring(start, length), throwing as .NET does on a bad range. */
export function substring(s: string, start: number, length?: number): string {
    if (start < 0 || start > s.length) throw new ArgumentOutOfRangeException('startIndex');
    if (length === undefined) return s.substring(start);
    if (length < 0 || start + length > s.length) throw new ArgumentOutOfRangeException('length');
    return s.substring(start, start + length);
}

/** s[^1]-style or s[i] access that throws as .NET does (IndexOutOfRangeException). */
export function charAt(s: string, index: number): string {
    if (index < 0 || index >= s.length) throw new IndexOutOfRangeException();
    return s[index]!;
}

export enum StringSplitOptions {
    None = 0,
    RemoveEmptyEntries = 1,
    TrimEntries = 2,
}

/** string.Split(char[] separators, StringSplitOptions options). */
export function split(s: string, separators: readonly string[], options: StringSplitOptions = StringSplitOptions.None): string[] {
    const parts: string[] = [];
    let current = '';
    for (let i = 0; i < s.length; i++) {
        const c = s[i]!;
        if (separators.includes(c)) {
            parts.push(current);
            current = '';
        } else {
            current += c;
        }
    }
    parts.push(current);

    let result = parts;
    if (options & StringSplitOptions.TrimEntries) result = result.map(trim);
    if (options & StringSplitOptions.RemoveEmptyEntries) result = result.filter(x => x.length > 0);
    return result;
}

/** string.Replace(string, string) — ordinal, all occurrences. */
export function replace(s: string, oldValue: string, newValue: string): string {
    return s.split(oldValue).join(newValue);
}

function indexOfOrdinalIgnoreCase(s: string, value: string, startIndex: number): number {
    const target = toUpperInvariant(value);
    const upperS = toUpperInvariant(s);
    return upperS.indexOf(target, startIndex);
}

/** string.Contains(value, StringComparison.InvariantCultureIgnoreCase) — ordinal ignore case in invariant mode. */
export function containsIgnoreCase(s: string, value: string): boolean {
    return indexOfOrdinalIgnoreCase(s, value, 0) !== -1;
}

/** string.StartsWith(value, StringComparison.InvariantCultureIgnoreCase) — ordinal ignore case in invariant mode. */
export function startsWithIgnoreCase(s: string, value: string): boolean {
    return s.length >= value.length && toUpperInvariant(s.substring(0, value.length)) === toUpperInvariant(value);
}

/** string.Replace(oldValue, newValue, StringComparison.InvariantCultureIgnoreCase) — ordinal ignore case in invariant mode. */
export function replaceIgnoreCase(s: string, oldValue: string, newValue: string): string {
    if (oldValue.length === 0) throw new ArgumentException('String cannot be of zero length. (Parameter \'oldValue\')');
    let result = '';
    let position = 0;
    while (true) {
        const found = indexOfOrdinalIgnoreCase(s, oldValue, position);
        if (found === -1) break;
        result += s.substring(position, found) + newValue;
        position = found + oldValue.length;
    }
    return result + s.substring(position);
}

/**
 * .NET's integer parsing with NumberStyles.Integer under the invariant culture: optional surrounding white space
 * (0x09-0x0D, 0x20), an optional sign, ASCII digits, and any number of trailing '\0' characters (which .NET ignores).
 * 'format' when the text is not such a number, 'overflow' when it is out of [min, max].
 */
function parseIntegerStyle(s: string, min: number, max: number): number | 'format' | 'overflow' {
    const match = /^[\t\n\v\f\r ]*([+-]?)([0-9]+)[\t\n\v\f\r ]*\0*$/.exec(s);
    if (!match) return 'format';
    const value = BigInt(match[1] === '-' ? `-${match[2]}` : match[2]!);
    if (value < BigInt(min) || value > BigInt(max)) return 'overflow';
    return Number(value);
}

/** int.TryParse(string) under the invariant culture. */
export function tryParseInt32(s: string | null | undefined): number | null {
    if (s == null) return null;
    const value = parseIntegerStyle(s, -2147483648, 2147483647);
    return typeof value === 'number' ? value : null;
}

/** int.Parse / byte.Parse(string) under the invariant culture: FormatException or OverflowException as .NET throws them. */
export function parseInteger(s: string, min: number, max: number, typeName: string): number {
    const value = parseIntegerStyle(s, min, max);
    if (value === 'format') throw new FormatException(`The input string '${s}' was not in a correct format.`);
    if (value === 'overflow') throw new OverflowException(`Value was either too large or too small for ${typeName}.`);
    return value;
}
