// NavigationManagerExtensions (Microsoft.AspNetCore.Components 9.0): GetUriWithQueryParameter(s) over the current
// navigationManager.uri.
//
// The values are formatted as there: a string as it is, a boolean "True"/"False", a number in the invariant format;
// null removes the parameter. The names are compared ignoring case (ordinal) on their encoded form; the first existing
// occurrence is replaced in place (with the new name's spelling), later ones dropped; parameters not present are
// appended in the order given; the #hash is kept. Names and values are escaped with Uri.EscapeDataString.

import { Uri } from '../lib/common/dotnet/Uri';
import { navigationManager } from './navigation';

/** A parameter value: what the original passes in its `Dictionary<string, object?>`. */
export type QueryParameterValue = string | number | boolean | null | undefined;

/** Uri.EscapeDataString */
export function escapeDataString(value: string): string {
    return Uri.escapeDataString(value);
}

function format(value: string | number | boolean): string {
    if (typeof value === 'boolean') return value ? 'True' : 'False';
    return String(value);
}

function upperInvariant(value: string): string {
    let result = '';
    for (const c of value) {
        const upper = c.toUpperCase();
        result += upper.length === 1 ? upper : c;
    }

    return result;
}

interface ParsedUri {
    uriWithoutQueryStringAndHash: string;
    pairs: { encodedName: string; encodedValue: string }[] | null;
    hash: string;
}

/** TryRebuildExistingQueryFromUri with QueryStringEnumerable. */
function parse(uri: string): ParsedUri {
    const hashIndex = uri.indexOf('#');
    const hash = hashIndex < 0 ? '' : uri.substring(hashIndex);
    const beforeHash = hashIndex > 0 ? uri.substring(0, hashIndex) : uri;
    const queryIndex = beforeHash.indexOf('?');
    if (queryIndex < 0) {
        return { uriWithoutQueryStringAndHash: hashIndex < 0 ? uri : uri.substring(0, hashIndex), pairs: null, hash };
    }

    const queryEnd = hashIndex < 0 ? uri.length : hashIndex;
    const query = uri.substring(queryIndex + 1, queryEnd);
    const pairs: { encodedName: string; encodedValue: string }[] = [];
    for (const segment of query.split('&')) {
        const equals = segment.indexOf('=');
        if (equals >= 0) {
            pairs.push({ encodedName: segment.substring(0, equals), encodedValue: segment.substring(equals + 1) });
        } else if (segment.length > 0) {
            pairs.push({ encodedName: segment, encodedValue: '' });
        }
    }

    return { uriWithoutQueryStringAndHash: uri.substring(0, queryIndex), pairs, hash };
}

class QueryStringBuilder {
    private hasNewParameters = false;

    constructor(private result: string) {
    }

    appendParameter(encodedName: string, encodedValue: string): void {
        this.result += (this.hasNewParameters ? '&' : '?') + encodedName + '=' + encodedValue;
        this.hasNewParameters = true;
    }

    appendHash(hash: string): string {
        return this.result + hash;
    }
}

/** `NavigationManager.GetUriWithQueryParameters(parameters)` — the parameters in the order of the C# dictionary. */
export function getUriWithQueryParameters(parameters: ReadonlyArray<readonly [name: string, value: QueryParameterValue]> | Readonly<Record<string, QueryParameterValue>>): string {
    return getUriWithQueryParametersOf(navigationManager.uri, parameters);
}

export function getUriWithQueryParametersOf(uri: string, parameters: ReadonlyArray<readonly [name: string, value: QueryParameterValue]> | Readonly<Record<string, QueryParameterValue>>): string {
    const entries = Array.isArray(parameters) ? parameters : Object.entries(parameters);
    const sources = entries.map(([name, value]) => {
        if (name.length === 0) throw new Error('Cannot have empty query parameter names.');
        return { encodedName: escapeDataString(name), encodedValue: value == null ? null : escapeDataString(format(value)) };
    });

    const parsed = parse(uri);
    const builder = new QueryStringBuilder(parsed.uriWithoutQueryStringAndHash);
    if (parsed.pairs === null) {
        for (const source of sources) {
            if (source.encodedValue !== null) builder.appendParameter(source.encodedName, source.encodedValue);
        }

        return builder.appendHash(parsed.hash);
    }

    const byName = new Map<string, { encodedName: string; encodedValue: string | null }>();
    for (const source of sources) {
        const key = upperInvariant(source.encodedName);
        if (byName.has(key)) throw new Error(`An item with the same key has already been added. Key: ${source.encodedName}`);
        byName.set(key, source);
    }

    for (const pair of parsed.pairs) {
        const source = byName.get(upperInvariant(pair.encodedName));
        if (source !== undefined) {
            if (source.encodedValue !== null) {
                builder.appendParameter(source.encodedName, source.encodedValue);
                source.encodedValue = null;
            }
        } else {
            builder.appendParameter(pair.encodedName, pair.encodedValue);
        }
    }

    for (const source of byName.values()) {
        if (source.encodedValue !== null) {
            builder.appendParameter(source.encodedName, source.encodedValue);
            source.encodedValue = null;
        }
    }

    return builder.appendHash(parsed.hash);
}

/** `NavigationManager.GetUriWithQueryParameter(name, value)` */
export function getUriWithQueryParameter(name: string, value: QueryParameterValue): string {
    if (name.length === 0) throw new Error('Cannot have empty query parameter names.');
    const uri = navigationManager.uri;
    const parsed = parse(uri);
    if (value == null) {
        if (parsed.pairs === null) return uri;
        const encodedName = upperInvariant(escapeDataString(name));
        const builder = new QueryStringBuilder(parsed.uriWithoutQueryStringAndHash);
        for (const pair of parsed.pairs) {
            if (upperInvariant(pair.encodedName) !== encodedName) builder.appendParameter(pair.encodedName, pair.encodedValue);
        }

        return builder.appendHash(parsed.hash);
    }

    const encodedName = escapeDataString(name);
    const encodedValue = escapeDataString(format(value));
    const builder = new QueryStringBuilder(parsed.uriWithoutQueryStringAndHash);
    if (parsed.pairs === null) {
        builder.appendParameter(encodedName, encodedValue);
        return builder.appendHash(parsed.hash);
    }

    let found = false;
    for (const pair of parsed.pairs) {
        if (upperInvariant(pair.encodedName) === upperInvariant(encodedName)) {
            found = true;
            builder.appendParameter(encodedName, encodedValue);
            continue;
        }

        builder.appendParameter(pair.encodedName, pair.encodedValue);
    }

    if (!found) builder.appendParameter(encodedName, encodedValue);
    return builder.appendHash(parsed.hash);
}
