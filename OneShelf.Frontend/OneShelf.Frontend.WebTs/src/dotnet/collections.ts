// The LINQ and collection behaviour the site's services rely on, where JavaScript's differs.

import { ArgumentException, ArgumentOutOfRangeException, KeyNotFoundException } from '../lib/common/dotnet/exceptions';

/** ILookup<TKey, TElement> (ToLookup): groups in the order their keys first appear; a missing key gives no elements. */
export class Lookup<TKey, TElement> implements Iterable<{ key: TKey; elements: readonly TElement[] }> {
    private readonly groups = new Map<unknown, { key: TKey; elements: TElement[] }>();

    /** `normalize` maps a key to what its equality compares (the identity for numbers and strings). */
    constructor(private readonly normalize: (key: TKey) => unknown = key => key) {
    }

    static from<TSource, TKey, TElement>(source: Iterable<TSource>, keySelector: (x: TSource) => TKey, elementSelector: (x: TSource) => TElement, normalize?: (key: TKey) => unknown): Lookup<TKey, TElement> {
        const lookup = new Lookup<TKey, TElement>(normalize);
        for (const x of source) {
            const key = keySelector(x);
            const normalized = lookup.normalize(key);
            let group = lookup.groups.get(normalized);
            if (group === undefined) {
                group = { key, elements: [] };
                lookup.groups.set(normalized, group);
            }

            group.elements.push(elementSelector(x));
        }

        return lookup;
    }

    /** `lookup[key]` */
    get(key: TKey): readonly TElement[] {
        return this.groups.get(this.normalize(key))?.elements ?? [];
    }

    contains(key: TKey): boolean {
        return this.groups.has(this.normalize(key));
    }

    get count(): number {
        return this.groups.size;
    }

    [Symbol.iterator](): Iterator<{ key: TKey; elements: readonly TElement[] }> {
        return this.groups.values();
    }
}

/** `ToDictionary(keySelector, elementSelector)`: a duplicate key throws. */
export function toDictionary<TSource, TKey, TValue>(source: Iterable<TSource>, keySelector: (x: TSource) => TKey, elementSelector: (x: TSource) => TValue): Map<TKey, TValue> {
    const result = new Map<TKey, TValue>();
    for (const x of source) {
        const key = keySelector(x);
        if (result.has(key)) {
            throw new ArgumentException(`An item with the same key has already been added. Key: ${String(key)}`);
        }

        result.set(key, elementSelector(x));
    }

    return result;
}

/** `dictionary[key]` (reading): a missing key throws. */
export function getRequired<TKey, TValue>(dictionary: ReadonlyMap<TKey, TValue>, key: TKey): TValue {
    if (!dictionary.has(key)) {
        throw new KeyNotFoundException(key);
    }

    return dictionary.get(key)!;
}

/** `list[index] = value`: an index outside the list throws. */
export function listSet<T>(list: T[], index: number, value: T): void {
    if (index < 0 || index >= list.length) {
        throw new ArgumentOutOfRangeException('index', 'Index was out of range. Must be non-negative and less than the size of the collection. (Parameter \'index\')');
    }

    list[index] = value;
}

/** `list.Remove(item)`: the first occurrence (by reference for the site's classes). */
export function listRemove<T>(list: T[], item: T): boolean {
    const index = list.indexOf(item);
    if (index < 0) return false;
    list.splice(index, 1);
    return true;
}

/** `list.RemoveAll(predicate)`, in place. */
export function listRemoveAll<T>(list: T[], predicate: (x: T) => boolean): number {
    let kept = 0;
    let removed = 0;
    for (let i = 0; i < list.length; i++) {
        const x = list[i]!;
        if (predicate(x)) {
            removed++;
        } else {
            list[kept++] = x;
        }
    }

    list.length = kept;
    return removed;
}

/** `first.SequenceEqual(second)` with the default equality (numbers, strings). */
export function sequenceEqual<T>(first: Iterable<T>, second: Iterable<T>): boolean {
    const a = [...first];
    const b = [...second];
    if (a.length !== b.length) return false;
    for (let i = 0; i < a.length; i++) {
        if (a[i] !== b[i]) return false;
    }

    return true;
}

/** `OrderBy(x => x)` of numbers: a stable ascending sort. */
export function orderNumbers(values: Iterable<number>): number[] {
    return [...values].sort((a, b) => a < b ? -1 : a > b ? 1 : 0);
}

/** `Chunk(size)` */
export function chunk<T>(source: readonly T[], size: number): T[][] {
    const result: T[][] = [];
    for (let i = 0; i < source.length; i += size) {
        result.push(source.slice(i, i + size));
    }

    return result;
}

/** Ordinal string comparison (`OrderBy(x => x)` of strings with the default comparer under InvariantGlobalization). */
export function compareOrdinal(a: string, b: string): number {
    return a < b ? -1 : a > b ? 1 : 0;
}
