// How Utf8JsonWriter writes values with the default options (no indentation, JavaScriptEncoder.Default).

import { ArgumentException } from '../exceptions';
import { isExactlyHalfwayBelow, parseSingle } from '../Numbers';
import { JsonValueKind, type JsonNode } from './JsonNode';

/**
 * A JSON string literal as the default encoder escapes it: printable ASCII stays except " \ & ' + < > `;
 * " is ", \ is \\, \b \f \n \r \t keep their short forms, everything else (controls, non-ASCII) is \uXXXX
 * (uppercase hex, UTF-16 code units).
 */
export function writeJsonString(value: string): string {
    let result = '"';
    let start = 0;
    for (let i = 0; i < value.length; i++) {
        const c = value.charCodeAt(i);
        if (c >= 0x20 && c <= 0x7e && c !== 0x22 && c !== 0x5c && c !== 0x26 && c !== 0x27 && c !== 0x2b && c !== 0x3c && c !== 0x3e && c !== 0x60) {
            continue;
        }
        result += value.substring(start, i);
        start = i + 1;
        switch (c) {
            case 0x5c: result += '\\\\'; break;
            case 0x0a: result += '\\n'; break;
            case 0x0d: result += '\\r'; break;
            case 0x09: result += '\\t'; break;
            case 0x08: result += '\\b'; break;
            case 0x0c: result += '\\f'; break;
            default: result += '\\u' + c.toString(16).toUpperCase().padStart(4, '0'); break;
        }
    }
    return result + value.substring(start) + '"';
}

/**
 * How Utf8JsonWriter writes a double or float (the shortest round-trippable digits, laid out by the "G" rules with
 * the round-trip precision, 17 for a double and 9 for a float, which double.ToString() does not use):
 * scientific when the decimal point would be more than max(digits, precision) to the right or more than 3 zeros
 * to the left; the exponent has a sign and at least two digits. `shortest` is the value in JavaScript's
 * exponential form with the shortest digits.
 */
function formatGeneral(negative: boolean, shortest: string, precision: number): string {
    const m = /^([0-9])(?:\.([0-9]+))?e([+-][0-9]+)$/.exec(shortest)!;
    const digits = (m[1]! + (m[2] ?? '')).replace(/0+$/, '') || '0';
    const exponent = Number(m[3]);
    const sign = negative ? '-' : '';
    if (digits === '0') return sign + '0';
    const decimalPosition = exponent + 1;
    const maxDigits = Math.max(digits.length, precision);
    if (decimalPosition > maxDigits || decimalPosition < -3) {
        const mantissa = digits.length > 1 ? `${digits[0]}.${digits.substring(1)}` : digits;
        const absExponent = Math.abs(exponent).toString().padStart(2, '0');
        return `${sign}${mantissa}E${exponent < 0 ? '-' : '+'}${absExponent}`;
    }
    if (decimalPosition <= 0) return `${sign}0.${'0'.repeat(-decimalPosition)}${digits}`;
    if (decimalPosition >= digits.length) return sign + digits + '0'.repeat(decimalPosition - digits.length);
    return `${sign}${digits.substring(0, decimalPosition)}.${digits.substring(decimalPosition)}`;
}

function assertFinite(value: number) {
    if (!Number.isFinite(value)) throw new ArgumentException('.NET number values such as positive and negative infinity cannot be written as valid JSON.');
}

/** A double (System.Double) as Utf8JsonWriter writes it. */
export function writeJsonDouble(value: number): string {
    assertFinite(value);
    return formatGeneral(Object.is(value, -0) || value < 0, Math.abs(value).toExponential(), 17);
}

/**
 * value.toExponential(fractionDigits), except that where the value lies exactly halfway between two candidates
 * (toExponential takes the larger) the one with the even last digit is taken, as .NET's digit generation rounds.
 */
function toExponentialHalfEven(value: number, fractionDigits: number): string {
    const candidate = value.toExponential(fractionDigits);
    const m = /^([0-9])(?:\.([0-9]+))?e([+-][0-9]+)$/.exec(candidate)!;
    const digits = BigInt(m[1]! + (m[2] ?? ''));
    if ((digits & 1n) === 0n || !isExactlyHalfwayBelow(value, digits, Number(m[3]) - fractionDigits)) return candidate;
    const even = (digits - 1n).toString();
    return `${even[0]}${even.length > 1 ? `.${even.substring(1)}` : ''}e${m[3]}`;
}

/** A float (System.Single) as Utf8JsonWriter writes it: the shortest text that reads back as the same float. */
export function writeJsonSingle(value: number): string {
    assertFinite(value);
    const single = Math.fround(value);
    const abs = Math.abs(single);
    let shortest = abs.toExponential();
    for (let precision = 1; precision <= 9; precision++) {
        const candidate = toExponentialHalfEven(abs, precision - 1);
        if (parseSingle(candidate) === abs) {
            shortest = candidate;
            break;
        }
    }
    return formatGeneral(Object.is(single, -0) || single < 0, shortest, 9);
}

/** A JsonElement written back: its value re-written, as Utf8JsonWriter does it. */
export function writeJsonNode(node: JsonNode): string {
    switch (node.kind) {
        case JsonValueKind.Object:
            return '{' + node.entries.map(([name, value]) => writeJsonString(name) + ':' + writeJsonNode(value)).join(',') + '}';
        case JsonValueKind.Array:
            return '[' + node.items.map(writeJsonNode).join(',') + ']';
        case JsonValueKind.String:
            return writeJsonString(node.value);
        case JsonValueKind.Number:
            return node.raw;
        case JsonValueKind.True:
            return 'true';
        case JsonValueKind.False:
            return 'false';
        case JsonValueKind.Null:
            return 'null';
    }
}
