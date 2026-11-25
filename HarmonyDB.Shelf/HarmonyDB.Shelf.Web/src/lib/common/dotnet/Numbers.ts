import { parseInteger } from './text';
import { DivideByZeroException, FormatException, OverflowException } from './exceptions';

/** A C# `float` (single precision) result: every float operation rounds to single precision. */
export const toFloat = Math.fround;

/** The unchecked C# cast to `byte`. */
export function toByte(value: number): number {
    return value & 0xff;
}

/** The unchecked C# cast to `short`. */
export function toShort(value: number): number {
    return (value << 16) >> 16;
}

/** The C# integer division (truncating), throwing on a zero divisor. */
export function intDiv(a: number, b: number): number {
    if (b === 0) throw new DivideByZeroException();
    return Math.trunc(a / b);
}

/** The C# integer remainder (with the sign of the dividend), throwing on a zero divisor. */
export function intMod(a: number, b: number): number {
    if (b === 0) throw new DivideByZeroException();
    const result = a % b;
    return result === 0 ? 0 : result;
}

/** The C# Comparer<int>.Default / int.CompareTo: -1, 0 or 1. */
export function compareInts(a: number, b: number): number {
    return a < b ? -1 : a > b ? 1 : 0;
}

/** byte.Parse(string) under the invariant culture. */
export function byteParse(value: string): number {
    return parseInteger(value, 0, 255, 'an unsigned byte');
}

/** round(value * 10^decimals) of the exact binary value, ties to even, as .NET Core 3.0+ formatting rounds. */
function roundScaled(value: number, decimals: number): bigint {
    const view = new DataView(new ArrayBuffer(8));
    view.setFloat64(0, Math.abs(value));
    const bits = view.getBigUint64(0);
    const exponentBits = Number((bits >> 52n) & 0x7ffn);
    const fraction = bits & 0xfffffffffffffn;
    const mantissa = exponentBits === 0 ? fraction : fraction | (1n << 52n);
    const exponent = (exponentBits === 0 ? 1 : exponentBits) - 1075;
    const numerator = mantissa * 10n ** BigInt(decimals);
    if (exponent >= 0) return numerator << BigInt(exponent);

    const denominator = 1n << BigInt(-exponent);
    const quotient = numerator / denominator;
    const twiceRemainder = (numerator % denominator) * 2n;
    if (twiceRemainder > denominator || (twiceRemainder === denominator && (quotient & 1n) === 1n)) return quotient + 1n;
    return quotient;
}

function formatFixedGrouped(value: number, decimals: number, scaleDecimals: number): string {
    const scaled = roundScaled(value, decimals + scaleDecimals).toString().padStart(decimals + 1, '0');
    const integerPart = scaled.slice(0, scaled.length - decimals);
    const fractionPart = scaled.slice(scaled.length - decimals);
    const grouped = integerPart.replace(/\B(?=(\d{3})+(?!\d))/g, ',');
    return decimals > 0 ? `${grouped}.${fractionPart}` : grouped;
}

/** value.ToString("N{decimals}", CultureInfo.InvariantCulture) for a float or a double. */
export function formatN(value: number, decimals: number): string {
    if (Number.isNaN(value)) return 'NaN';
    if (!Number.isFinite(value)) return value > 0 ? 'Infinity' : '-Infinity';
    const negative = value < 0 || Object.is(value, -0);
    return `${negative ? '-' : ''}${formatFixedGrouped(value, decimals, 0)}`;
}

/** value.ToString("P{decimals}", CultureInfo.InvariantCulture) for a float or a double: "n %", "-n %". */
export function formatP(value: number, decimals: number): string {
    if (Number.isNaN(value)) return 'NaN';
    if (!Number.isFinite(value)) return value > 0 ? 'Infinity' : '-Infinity';
    const negative = value < 0 || Object.is(value, -0);
    return `${negative ? '-' : ''}${formatFixedGrouped(value, decimals, 2)} %`;
}

const floatView = new DataView(new ArrayBuffer(4));

function floatBits(value: number): number {
    floatView.setFloat32(0, value);
    return floatView.getUint32(0);
}

function floatOfBits(bits: number): number {
    floatView.setUint32(0, bits >>> 0);
    return floatView.getFloat32(0);
}

/** A float's value for arithmetic: an infinity stands for the power of two it rounds up from, ±2^128. */
function finiteValue(value: number): number {
    return Number.isFinite(value) ? value : Math.sign(value) * 2 ** 128;
}

/** |value| (a finite double) as mantissa * 2^exponent2, exactly. */
function decompose(value: number): { mantissa: bigint; exponent2: number } {
    const view = new DataView(new ArrayBuffer(8));
    view.setFloat64(0, Math.abs(value));
    const bits = view.getBigUint64(0);
    const exponentBits = Number((bits >> 52n) & 0x7ffn);
    const fraction = bits & 0xfffffffffffffn;
    return {
        mantissa: exponentBits === 0 ? fraction : fraction | (1n << 52n),
        exponent2: (exponentBits === 0 ? 1 : exponentBits) - 1075,
    };
}

/** Whether |value| (a finite double) is exactly (digits - 1/2) * 10^exponent10. */
export function isExactlyHalfwayBelow(value: number, digits: bigint, exponent10: number): boolean {
    const { mantissa, exponent2 } = decompose(value);
    let left = 2n * mantissa;
    let right = 2n * digits - 1n;
    if (exponent10 >= 0) right *= 10n ** BigInt(exponent10);
    else left *= 10n ** BigInt(-exponent10);
    if (exponent2 >= 0) left <<= BigInt(exponent2);
    else right <<= BigInt(-exponent2);
    return left === right;
}

/** Compares |text| (a decimal number: digits, an optional fraction and exponent) with |value| (a finite double), exactly. */
function compareMagnitudes(text: string, value: number): number {
    const m = /^-?([0-9]*)(?:\.([0-9]*))?(?:[eE]([+-]?[0-9]+))?$/.exec(text)!;
    let digits = BigInt((m[1] ?? '') + (m[2] ?? '') || '0');
    let exponent10 = Number(m[3] ?? '0') - (m[2] ?? '').length;

    let { mantissa, exponent2 } = decompose(value);

    if (exponent10 >= 0) {
        digits *= 10n ** BigInt(exponent10);
        exponent10 = 0;
    } else {
        mantissa *= 10n ** BigInt(-exponent10);
    }

    if (exponent2 >= 0) mantissa <<= BigInt(exponent2);
    else digits <<= BigInt(-exponent2);

    return digits < mantissa ? -1 : digits > mantissa ? 1 : 0;
}

/**
 * float.Parse (as System.Text.Json reads a float): the decimal text rounded once, directly to single precision.
 * Math.fround(Number(text)) rounds twice (to double, then to float) and gives the other float when the double lands
 * exactly halfway between two floats; that case is decided here on the exact decimal value.
 */
export function parseSingle(text: string): number {
    const double = Number(text);
    const single = Math.fround(double);
    if (single === double || Number.isNaN(double)) return single;

    // The neighbouring float on the other side of the double.
    const other = floatOfBits(Math.abs(double) > Math.abs(single) ? floatBits(single) + 1 : floatBits(single) - 1);
    const halfway = (finiteValue(single) + finiteValue(other)) / 2;
    if (double !== halfway) return single;

    const comparison = compareMagnitudes(text, halfway);
    const [smaller, larger] = Math.abs(single) < Math.abs(other) ? [single, other] : [other, single];
    if (comparison > 0) return larger;
    if (comparison < 0) return smaller;
    return (floatBits(single) & 1) === 0 ? single : other;
}