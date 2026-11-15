// The "uri" suite of dispatch.ts: System.Uri (lib/common/dotnet/Uri.ts) and the string hash (Marvin, hashCode.ts),
// the same calls in the same order as tools/dotnet-reference/DotnetReference/Suites.Uri.cs.
// A string that is not valid UTF-16 (a lone surrogate) travels as the array of its code units, both ways.

import { marvinHash32, marvinSeed, stringHashCode, stringHashCodeOrdinalIgnoreCase } from '../common/dotnet/hashCode';
import { deserialize, jsonUri, serialize, defaultJsonSerializerOptions } from '../common/dotnet/json/JsonSerializer';
import { writeJsonString } from '../common/dotnet/json/JsonWriter';
import { toUpperInvariant } from '../common/dotnet/text';
import { Uri, UriComponents, UriFormat, UriKind } from '../common/dotnet/Uri';

/* eslint-disable @typescript-eslint/no-explicit-any */
type Input = any;
type Output = unknown;

function isValidUtf16(s: string): boolean {
    return !/[\ud800-\udbff](?![\udc00-\udfff])|(?<![\ud800-\udbff])[\udc00-\udfff]/.test(s);
}

/** A string out: as it is, or the array of its code units when it is not valid UTF-16. */
function t(s: string | null): Output {
    if (s === null) return null;
    return isValidUtf16(s) ? s : Array.from({ length: s.length }, (_, i) => s.charCodeAt(i));
}

/** A string in: a string, or the array of its code units. */
function text(e: string | number[]): string {
    return Array.isArray(e) ? String.fromCharCode(...e) : e;
}

export function uriSuite(input: Input, tryRun: (func: () => Output) => Output): Output {
    switch (input.f as string) {
        case 'uri': {
            const strings = ((input.u ?? []) as (string | number[])[]).map(text);
            const parsed: (Uri | null)[] = [];
            const each = strings.map(s => {
                const any = Uri.tryCreate(s, UriKind.RelativeOrAbsolute);
                parsed.push(any);
                const absolute = Uri.tryCreate(s, UriKind.Absolute);
                const relative = Uri.tryCreate(s, UriKind.Relative);
                return [
                    any === null ? null : describeUri(any, tryRun),
                    absolute === null ? null : describeUri(absolute, tryRun),
                    relative === null ? null : describeUri(relative, tryRun),
                    tryRun(() => describeUri(new Uri(s), tryRun)),
                    tryRun(() => describeUri(new Uri(s, UriKind.RelativeOrAbsolute), tryRun)),
                    tryRun(() => {
                        const read = deserialize(writeJsonString(s), jsonUri, defaultJsonSerializerOptions);
                        return [t(read.originalString), t(serialize(read, jsonUri, defaultJsonSerializerOptions)), describeUri(read, tryRun)];
                    }),
                    t(Uri.escapeDataString(s)),
                ];
            });

            const eq: boolean[] = [];
            for (const a of parsed) {
                for (const b of parsed) {
                    if (a !== null && b !== null) eq.push(Uri.equals(a, b));
                }
            }

            return { each, eq };
        }
        case 'combine': {
            const baseText = text(input.b);
            return ((input.r ?? []) as (string | number[] | null)[]).map(r => tryRun(() => describeUri(new Uri(new Uri(baseText, UriKind.RelativeOrAbsolute), r === null ? null : text(r)), tryRun)));
        }
        case 'json':
            return ((input.raw ?? []) as string[]).map(raw => tryRun(() => t(serialize(deserialize(raw, jsonUri, defaultJsonSerializerOptions), jsonUri, defaultJsonSerializerOptions))));
        case 'marvin':
            return ((input.s ?? []) as (string | number[])[]).map(text).map(s => [
                marvinHash32(s, marvinSeed.p0, marvinSeed.p1),
                marvinHash32(toUpperInvariant(s), marvinSeed.p0, marvinSeed.p1),
                true,
                true,
            ]);
        default:
            throw new Error(`Unknown function ${input.f}.`);
    }
}

function describeUri(u: Uri, tryRun: (func: () => Output) => Output): Output {
    const asText = u.toString();
    let key: Output = null;
    let hash: Output;
    if (u.isAbsoluteUri) {
        const components = UriComponents.HttpRequestUrl | (u.scheme === 'mailto' ? UriComponents.UserInfo : 0);
        const keyText = u.getComponents(components, UriFormat.SafeUnescaped);
        key = t(keyText);
        hash = [u.getHashCode() === stringHashCode(keyText), u.getHashCode() === stringHashCodeOrdinalIgnoreCase(keyText)];
    } else {
        hash = [u.getHashCode() === stringHashCode(u.originalString), false];
    }

    return [
        u.isAbsoluteUri,
        t(u.originalString),
        t(asText),
        tryRun(() => t(u.host)),
        tryRun(() => u.port),
        key,
        hash,
        tryRun(() => t(u.getComponents(UriComponents.AbsoluteUri, UriFormat.UriEscaped))),
        tryRun(() => t(new Uri(asText).toString())),
    ];
}
