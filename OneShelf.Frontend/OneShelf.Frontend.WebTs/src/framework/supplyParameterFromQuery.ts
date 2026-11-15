// [SupplyParameterFromQuery] (Microsoft.AspNetCore.Components 9.0: QueryParameterValueSupplier, QueryStringEnumerable,
// UrlValueConstraint) — a page's parameters read from the query string of the current address:
//
//     [Parameter] [SupplyParameterFromQuery] public int? ArtistId { get; set; }
//
// becomes
//
//     const query = useQueryParameters();
//     const artistId = query.nullableInt('ArtistId');
//
// As there: the names are compared ignoring case; the first occurrence of a name is used; names and values are decoded
// ('+' is a space, then Uri.UnescapeDataString); a missing parameter gives null (false, 0 for bool, int); for the
// nullable types an empty value gives null; a value that does not parse throws InvalidOperationException, as the
// original does when it sets the parameters.

import { useLocation } from 'react-router-dom';
import { InvalidOperationException } from '../lib/common/dotnet/exceptions';
import { toUpperInvariant, tryParseInt32 } from '../lib/common/dotnet/text';

/** Uri.UnescapeDataString: valid UTF-8 %XX sequences are decoded, anything else is left as it is. */
function unescapeDataString(value: string): string {
    if (!value.includes('%')) return value;
    try {
        return decodeURIComponent(value);
    } catch {
        return value.replace(/(%[0-9A-Fa-f]{2})+/g, run => {
            let result = '';
            const bytes = run.match(/%[0-9A-Fa-f]{2}/g)!;
            let i = 0;
            while (i < bytes.length) {
                // the longest prefix from i that decodes
                let decoded = false;
                for (let j = Math.min(bytes.length, i + 4); j > i; j--) {
                    try {
                        result += decodeURIComponent(bytes.slice(i, j).join(''));
                        i = j;
                        decoded = true;
                        break;
                    } catch {
                        // try a shorter sequence
                    }
                }

                if (!decoded) {
                    result += bytes[i];
                    i++;
                }
            }

            return result;
        });
    }
}

function decode(value: string): string {
    if (!value.includes('%') && !value.includes('+')) return value;
    return unescapeDataString(value.split('+').join(' '));
}

/** bool.TryParse: "True" / "False" in any case, with surrounding white space and nulls trimmed. */
function tryParseBoolean(value: string): boolean | null {
    const trimmed = value.replace(/^[\s\0]+|[\s\0]+$/g, '').toLowerCase();
    if (trimmed === 'true') return true;
    if (trimmed === 'false') return false;
    return null;
}

/** long.TryParse(str, NumberStyles.Integer, CultureInfo.InvariantCulture). */
function tryParseInt64(value: string): number | null {
    const match = /^[\t\n\v\f\r ]*([+-]?)([0-9]+)[\t\n\v\f\r ]*$/.exec(value);
    if (!match) return null;
    const parsed = BigInt((match[1] === '-' ? '-' : '') + match[2]);
    if (parsed < -9223372036854775808n || parsed > 9223372036854775807n) return null;
    return Number(parsed);
}

export class QueryParameters {
    private readonly values = new Map<string, string>();

    constructor(search: string) {
        const query = search.startsWith('?') ? search.substring(1) : search;
        for (const segment of query.split('&')) {
            if (segment.length === 0) continue;
            const equals = segment.indexOf('=');
            const name = equals < 0 ? segment : segment.substring(0, equals);
            if (name.length === 0) continue;
            const value = equals < 0 ? '' : segment.substring(equals + 1);
            const key = toUpperInvariant(decode(name));
            if (!this.values.has(key)) this.values.set(key, decode(value));
        }
    }

    private raw(name: string): string | undefined {
        return this.values.get(toUpperInvariant(name));
    }

    private static fail(value: string, type: string, name: string): never {
        throw new InvalidOperationException(`Cannot parse the value '${value}' as type '${type}' for '${name}'.`);
    }

    /** string? */
    string(name: string): string | null {
        return this.raw(name) ?? null;
    }

    /** bool */
    bool(name: string): boolean {
        const value = this.raw(name);
        if (value === undefined) return false;
        return tryParseBoolean(value) ?? QueryParameters.fail(value, 'System.Boolean', name);
    }

    /** int */
    int(name: string): number {
        const value = this.raw(name);
        if (value === undefined) return 0;
        return tryParseInt32(value) ?? QueryParameters.fail(value, 'System.Int32', name);
    }

    /** int? */
    nullableInt(name: string): number | null {
        const value = this.raw(name);
        if (value === undefined || value.length === 0) return null;
        return tryParseInt32(value) ?? QueryParameters.fail(value, 'System.Nullable`1[System.Int32]', name);
    }

    /** long? */
    nullableLong(name: string): number | null {
        const value = this.raw(name);
        if (value === undefined || value.length === 0) return null;
        return tryParseInt64(value) ?? QueryParameters.fail(value, 'System.Nullable`1[System.Int64]', name);
    }
}

/** The query parameters of the current address; the component renders again when the address changes. */
export function useQueryParameters(): QueryParameters {
    const location = useLocation();
    return new QueryParameters(location.search);
}
