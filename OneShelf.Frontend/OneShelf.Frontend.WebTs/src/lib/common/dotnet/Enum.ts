// System.Enum's Parse and ToString for TypeScript numeric enums.

import { ArgumentException, FormatException } from './exceptions';
import { isWhiteSpace, parseInteger, trim } from './text';

type NumericEnum = Record<string, string | number>;

/**
 * Enum.Parse<TEnum>(value), as .NET does it: leading white space skipped; a value starting with a digit or a sign is
 * first read as an integer (trailing ASCII white space and '\0's allowed; out of the int range is an
 * OverflowException); otherwise, or when that fails, case-sensitive names, comma-separated ones OR'ed, each trimmed.
 */
export function enumParse<TEnum extends number>(enumType: NumericEnum, value: string): TEnum {
    let start = 0;
    while (start < value.length && isWhiteSpace(value[start]!)) start++;
    const rest = value.substring(start);
    if (rest.length === 0) throw new ArgumentException('Must specify valid information for parsing in the string.');
    const first = rest[0]!;
    if ((first >= '0' && first <= '9') || first === '-' || first === '+') {
        try {
            return parseInteger(rest, -2147483648, 2147483647, 'an Int32') as TEnum;
        } catch (e) {
            if (!(e instanceof FormatException)) throw e;
        }
    }

    let result = 0;
    for (const part of rest.split(',')) {
        const name = trim(part);
        const found = Object.prototype.hasOwnProperty.call(enumType, name) ? enumType[name] : undefined;
        if (typeof found !== 'number') throw new ArgumentException(`Requested value '${value}' was not found.`);
        result |= found;
    }
    return result as TEnum;
}
/** Enum.ToString() for a non-[Flags] enum: the name, or the number when the value has no name. */
export function enumToString(enumType: NumericEnum, value: number): string {
    const name = enumType[value];
    return typeof name === 'string' ? name : value.toString();
}
