// System.Text.Json's JsonSerializer for the ported models, driven by contracts written beside each type.
// A contract states what System.Text.Json derives from the C# type: the public properties in its order
// (the type's own properties first, then each base type's — as the reflection resolver orders them), their C# names
// (the naming policy applies to them, [JsonPropertyName] overrides it, dictionary keys are never renamed),
// [JsonIgnore] (left out of the contract), JsonIgnoreCondition.WhenWritingNull, and `required` members
// (missing on reading -> JsonException). What the options don't set stays at System.Text.Json's defaults:
// case-sensitive property names, unknown members skipped, nulls written, numbers only from JSON numbers,
// enums as their numbers, max depth 64.

import { fromBase64String, toBase64String } from '../Convert';
import { FormatException, JsonException } from '../exceptions';
import { DateTime } from '../DateTime';
import { tryParseGuidD, type Guid } from '../Guid';
import { parseSingle } from '../Numbers';
import { isUpper, toLowerInvariantChar } from '../text';
import { Uri, UriKind } from '../Uri';
import { JsonElement, JsonValueKind, parseJson, type JsonNode } from './JsonNode';
import { writeJsonDouble, writeJsonNode, writeJsonSingle, writeJsonString } from './JsonWriter';

/** The JsonSerializerOptions settings that differ between the places the original serializes. */
export interface JsonSerializerOptions {
    /** JsonNamingPolicy.CamelCase or none (property names as in C#). */
    readonly propertyNamingPolicy: 'camelCase' | null;
}

/** `new JsonSerializerOptions()` and JsonSerializer's default: names as in C#. */
export const defaultJsonSerializerOptions: JsonSerializerOptions = { propertyNamingPolicy: null };

/** `new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }`. */
export const camelCaseJsonSerializerOptions: JsonSerializerOptions = { propertyNamingPolicy: 'camelCase' };

export interface JsonContract<T> {
    /** The C# type name, for error messages. */
    readonly typeName: string;
    read(node: JsonNode, options: JsonSerializerOptions, path: string): T;
    write(value: T, options: JsonSerializerOptions): string;
}

/** JsonSerializer.Serialize */
export function serialize<T>(value: T, contract: JsonContract<T>, options: JsonSerializerOptions = defaultJsonSerializerOptions): string {
    return contract.write(value, options);
}

/** JsonSerializer.Deserialize */
export function deserialize<T>(json: string, contract: JsonContract<T>, options: JsonSerializerOptions = defaultJsonSerializerOptions): T {
    return contract.read(parseJson(json), options, '$');
}

/** JsonNamingPolicy.CamelCase.ConvertName */
export function toCamelCase(name: string): string {
    if (name.length === 0 || !isUpper(name[0]!)) return name;
    const chars = name.split('');
    for (let i = 0; i < chars.length; i++) {
        if (i === 1 && !isUpper(chars[i]!)) break;
        const hasNext = i + 1 < chars.length;
        if (i > 0 && hasNext && !isUpper(chars[i + 1]!)) {
            if (chars[i + 1] === ' ') chars[i] = toLowerInvariantChar(chars[i]!);
            break;
        }
        chars[i] = toLowerInvariantChar(chars[i]!);
    }
    return chars.join('');
}

function convertError(typeName: string, path: string): JsonException {
    return new JsonException(`The JSON value could not be converted to ${typeName}. Path: ${path}.`);
}

function valueType<T>(typeName: string, read: (node: JsonNode) => T | undefined, write: (value: T) => string): JsonContract<T> {
    return {
        typeName,
        read(node, _options, path) {
            const value = node.kind === JsonValueKind.Null ? undefined : read(node);
            if (value === undefined) throw convertError(typeName, path);
            return value;
        },
        write(value) {
            return value == null ? 'null' : write(value);
        },
    };
}

function referenceType<T>(typeName: string, read: (node: JsonNode, options: JsonSerializerOptions, path: string) => T | undefined, write: (value: T, options: JsonSerializerOptions) => string): JsonContract<T> {
    return {
        typeName,
        read(node, options, path) {
            if (node.kind === JsonValueKind.Null) return null as T;
            const value = read(node, options, path);
            if (value === undefined) throw convertError(typeName, path);
            return value;
        },
        write(value, options) {
            return value == null ? 'null' : write(value, options);
        },
    };
}

/**
 * An integer number token as System.Text.Json reads it into an integer type: digits only (no fraction or exponent),
 * a minus sign only for the signed types (an unsigned type rejects even "-0"), the range checked exactly.
 */
function readInteger(node: JsonNode, min: bigint, max: bigint): number | undefined {
    if (node.kind !== JsonValueKind.Number) return undefined;
    return parseInteger(node.raw, min, max, /^-?[0-9]+$/);
}

function parseInteger(text: string, min: bigint, max: bigint, pattern: RegExp): number | undefined {
    if (!pattern.test(text) || (min >= 0n && text.startsWith('-'))) return undefined;
    const value = BigInt(text);
    if (value < min || value > max) return undefined;
    return Number(value);
}

const int32Min = -2147483648n, int32Max = 2147483647n, int64Min = -9223372036854775808n, int64Max = 9223372036854775807n;

export const jsonInt32: JsonContract<number> = valueType('System.Int32', n => readInteger(n, int32Min, int32Max), v => String(v));

/** long. Values beyond ±2^53 lose precision in JavaScript numbers (see PORTING.md, Departures). */
export const jsonInt64: JsonContract<number> = valueType('System.Int64', n => readInteger(n, int64Min, int64Max), v => String(v));

export const jsonByte: JsonContract<number> = valueType('System.Byte', n => readInteger(n, 0n, 255n), v => String(v));

/**
 * A float: the number text rounded once, directly to single precision (an overflow reads as an infinity, as .NET's
 * parsing gives).
 */
export const jsonSingle: JsonContract<number> = valueType('System.Single', n => n.kind === JsonValueKind.Number ? parseSingle(n.raw) : undefined, writeJsonSingle);

/** A double (an overflow reads as an infinity, as .NET's parsing gives). */
export const jsonDouble: JsonContract<number> = valueType('System.Double', n => n.kind === JsonValueKind.Number ? Number(n.raw) : undefined, writeJsonDouble);

export const jsonBoolean: JsonContract<boolean> = valueType('System.Boolean', n => n.kind === JsonValueKind.True ? true : n.kind === JsonValueKind.False ? false : undefined, v => v ? 'true' : 'false');

export const jsonString: JsonContract<string> = referenceType('System.String', n => n.kind === JsonValueKind.String ? n.value : undefined, v => writeJsonString(v));

/** An enum without JsonStringEnumConverter: its number (any int32 is accepted, as System.Text.Json does). */
export function jsonEnum<TEnum extends number>(typeName: string): JsonContract<TEnum> {
    return valueType(typeName, n => readInteger(n, int32Min, int32Max) as TEnum | undefined, v => String(v));
}

/** Written as Utf8JsonWriter.WriteStringValue(Guid) writes it: formatted directly, without escaping. */
export const jsonGuid: JsonContract<Guid> = valueType('System.Guid', n => n.kind === JsonValueKind.String ? tryParseGuidD(n.value) ?? undefined : undefined, v => `"${v}"`);

/** Written as Utf8JsonWriter.WriteStringValue(DateTime) writes it: formatted directly, without escaping (a "+" stays). */
export const jsonDateTime: JsonContract<DateTime> = valueType('System.DateTime', n => n.kind === JsonValueKind.String ? DateTime.parseJson(n.value) ?? undefined : undefined, v => `"${v.toJson()}"`);

/**
 * System.Uri, as System.Text.Json's UriConverter: read with Uri.TryCreate(text, UriKind.RelativeOrAbsolute) (a string
 * it rejects is a JsonException), written as its OriginalString.
 */
export const jsonUri: JsonContract<Uri> = referenceType('System.Uri', n => n.kind === JsonValueKind.String ? Uri.tryCreate(n.value, UriKind.RelativeOrAbsolute) ?? undefined : undefined,
    v => writeJsonString(v.originalString));

/** byte[]: base64. */
export const jsonBytes: JsonContract<Uint8Array> = referenceType('System.Byte[]', n => {
    if (n.kind !== JsonValueKind.String) return undefined;
    // Base64 as Convert reads it (white space between the characters is skipped).
    try {
        return fromBase64String(n.value);
    } catch (e) {
        if (e instanceof FormatException) return undefined;
        throw e;
    }
    // Written as Utf8JsonWriter.WriteBase64StringValue writes it: without escaping (a "+" stays).
}, v => `"${toBase64String(v)}"`);

/**
 * `object`: reads any non-null value as a JsonElement; writes a JsonElement back as it was, and the runtime values
 * the original puts into such properties (string, bool) as JSON values.
 */
export const jsonObjectAny: JsonContract<unknown> = referenceType<unknown>('System.Object', n => new JsonElement(n), v => {
    if (v instanceof JsonElement) return writeJsonNode(v.node);
    if (typeof v === 'string') return writeJsonString(v);
    if (typeof v === 'boolean') return v ? 'true' : 'false';
    if (typeof v === 'number') return writeJsonDouble(v);
    throw new Error(`Serialization of ${typeof v} as System.Object is not supported.`);
});

/** T? for a value type T. */
export function jsonNullable<T>(contract: JsonContract<T>): JsonContract<T | null> {
    return {
        typeName: `System.Nullable<${contract.typeName}>`,
        read: (node, options, path) => node.kind === JsonValueKind.Null ? null : contract.read(node, options, path),
        write: (value, options) => value == null ? 'null' : contract.write(value, options),
    };
}

/** List<T>, IReadOnlyList<T>, T[] (except byte[]). */
export function jsonList<T>(item: JsonContract<T>): JsonContract<T[]> {
    return referenceType(`List<${item.typeName}>`, (n, options, path) => {
        if (n.kind !== JsonValueKind.Array) return undefined;
        return n.items.map((x, i) => item.read(x, options, `${path}[${i}]`));
    }, (v, options) => '[' + v.map(x => item.write(x, options)).join(',') + ']');
}

export type JsonDictionaryKey = 'string' | 'int32' | 'int64';

/** Dictionary<TKey, TValue>, IReadOnlyDictionary<TKey, TValue>: a Map in the order of the JSON. Keys are not renamed. */
export function jsonDictionary<K extends string | number, V>(keyKind: JsonDictionaryKey, value: JsonContract<V>): JsonContract<Map<K, V>> {
    const parseKey = (key: string, path: string): K => {
        if (keyKind === 'string') return key as K;
        // A number key: an optional sign (a "+" too), digits.
        const parsed = parseInteger(key, keyKind === 'int32' ? int32Min : int64Min, keyKind === 'int32' ? int32Max : int64Max, /^[-+]?[0-9]+$/);
        if (parsed === undefined) throw convertError(keyKind === 'int32' ? 'System.Int32' : 'System.Int64', `${path}.${key}`);
        return parsed as K;
    };
    return referenceType(`Dictionary<${keyKind}, ${value.typeName}>`, (n, options, path) => {
        if (n.kind !== JsonValueKind.Object) return undefined;
        const result = new Map<K, V>();
        for (const [key, child] of n.entries) {
            result.set(parseKey(key, path), value.read(child, options, `${path}.${key}`));
        }
        return result;
    }, (v, options) => {
        const parts: string[] = [];
        for (const [key, child] of v) parts.push(writeJsonString(String(key)) + ':' + value.write(child, options));
        return '{' + parts.join(',') + '}';
    });
}

export interface JsonPropertyDefinition {
    /** The C# property name. The TypeScript member is its camelCase. */
    readonly name: string;
    readonly contract: () => JsonContract<any>;
    /** [JsonPropertyName] */
    readonly jsonName?: string;
    /** a C# `required` member */
    readonly required?: boolean;
    /** [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] */
    readonly ignoreWhenWritingNull?: boolean;
    /** [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]: the type's default (null, false, 0) left out */
    readonly ignoreWhenWritingDefault?: boolean;
}

export function jsonProperty(name: string, contract: JsonContract<any> | (() => JsonContract<any>), settings?: Omit<JsonPropertyDefinition, 'name' | 'contract'>): JsonPropertyDefinition {
    return { name, contract: typeof contract === 'function' ? contract : () => contract, ...settings };
}

/**
 * A class or record. `create` gets the members found in the JSON (by their TypeScript names) and makes the object
 * as System.Text.Json does: a missing member keeps the C# initializer's value, a present one is set, even to null.
 */
export function jsonClass<T>(typeName: string, create: (init: Record<string, unknown>) => T, properties: () => readonly JsonPropertyDefinition[]): JsonContract<T> {
    let resolved: readonly (JsonPropertyDefinition & { field: string })[] | undefined;
    const byJsonName = new Map<string | null, Map<string, JsonPropertyDefinition & { field: string }>>();
    const all = () => resolved ??= properties().map(p => ({ ...p, field: toCamelCase(p.name) }));
    const jsonName = (p: JsonPropertyDefinition, options: JsonSerializerOptions) =>
        p.jsonName ?? (options.propertyNamingPolicy === 'camelCase' ? toCamelCase(p.name) : p.name);

    return referenceType(typeName, (n, options, path) => {
        if (n.kind !== JsonValueKind.Object) return undefined;
        let lookup = byJsonName.get(options.propertyNamingPolicy);
        if (!lookup) {
            lookup = new Map(all().map(p => [jsonName(p, options), p]));
            byJsonName.set(options.propertyNamingPolicy, lookup);
        }
        const init: Record<string, unknown> = {};
        for (const [key, child] of n.entries) {
            const property = lookup.get(key);
            if (!property) continue;
            init[property.field] = property.contract().read(child, options, `${path}.${key}`);
        }
        const missing = all().filter(p => p.required && !(p.field in init));
        if (missing.length > 0) {
            throw new JsonException(`JSON deserialization for type '${typeName}' was missing required properties, including the following: ${missing.map(p => jsonName(p, options)).join(', ')}`);
        }
        return create(init);
    }, (v, options) => {
        const parts: string[] = [];
        for (const property of all()) {
            const value = (v as Record<string, unknown>)[property.field];
            if (property.ignoreWhenWritingNull && value == null) continue;
            if (property.ignoreWhenWritingDefault && (value == null || value === false || value === 0)) continue;
            parts.push(writeJsonString(jsonName(property, options)) + ':' + property.contract().write(value, options));
        }
        return '{' + parts.join(',') + '}';
    });
}
