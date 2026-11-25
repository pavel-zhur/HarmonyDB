// OneShelf.Common.Extensions and OneShelf.Common.ExtensionStructOverloads.
// The C# extensions take and give IEnumerable<T>; here they take any Iterable and give arrays (evaluated at once).
// C# value tuples become objects with the same element names.
// The struct overloads (WithPrevious, ToChunks for value types) behave as the class ones; there is one function each.
// Equality where C# uses the default comparer (GroupBy, Equals) is .NET's default equality (dotnet/Equality): by value
// for numbers, strings, booleans, enums, value tuples and records, by reference for other objects.
// AsIReadOnlyList, AsIReadOnlyDictionary, AsNullable, OnceAsNullable only change the C# static type; not ported.

import { Dictionary } from '../dotnet/Collections';
import { csEquals } from '../dotnet/Equality';
import { ArgumentException } from '../dotnet/exceptions';
import { groupBy } from '../dotnet/Linq';

export function selectSingle<TSource, TDestination>(source: TSource, func: (source: TSource) => TDestination): TDestination {
    return func(source);
}

export function once<T>(first: T): T[] {
    return [first];
}

/**
 * AnyDuplicates(source, selector, out firstDuplicate). `out` receives firstDuplicate; when there is none it gets null
 * (C# gives default(TItem), which is 0 for a number).
 */
export function anyDuplicates<TSource, TItem = TSource>(
    source: Iterable<TSource>,
    selector?: (x: TSource) => TItem,
    out?: { firstDuplicate?: TItem | null },
): boolean {
    const select = selector ?? ((x: TSource) => x as unknown as TItem);
    // GroupBy(selector).FirstOrDefault(x => x.Count() > 1): the first group, in the order of first appearance, with a duplicate
    const group = groupBy(source, select).find(x => x.items.length > 1);
    if (group === undefined) {
        if (out) out.firstDuplicate = null;
        return false;
    }
    if (out) out.firstDuplicate = group.key;
    return true;
}

/** Safe(dictionary, key): the value or null. */
export function safe<TKey, TValue>(dictionary: ReadonlyMap<TKey, TValue>, key: TKey): TValue | null {
    return dictionary.has(key) ? dictionary.get(key)! : null;
}

export function withIndices<T>(source: Iterable<T>): { x: T; i: number }[] {
    const result: { x: T; i: number }[] = [];
    let i = 0;
    for (const x of source) result.push({ x, i: i++ });
    return result;
}

export function withIndicesNullable<T>(source: Iterable<T>): { x: T; i: number | null }[] {
    return withIndices(source);
}

export function withIsLast<T>(source: Iterable<T>): { x: T; isLast: boolean }[] {
    const items = Array.from(source);
    return items.map((x, i) => ({ x, isLast: i === items.length - 1 }));
}

export function withIsFirst<T>(source: Iterable<T>): { x: T; isFirst: boolean }[] {
    const result: { x: T; isFirst: boolean }[] = [];
    let isFirst = true;
    for (const x of source) {
        result.push({ x, isFirst });
        isFirst = false;
    }
    return result;
}

export function withIsFirstLast<T>(source: Iterable<T>): { x: T; isFirst: boolean; isLast: boolean }[] {
    return withIsLast(withIsFirst(source)).map(x => ({ x: x.x.x, isFirst: x.x.isFirst, isLast: x.isLast }));
}

export function withPrevious<T>(source: Iterable<T>): { previous: T | null; current: T }[] {
    const result: { previous: T | null; current: T }[] = [];
    let previous: T | null = null;
    for (const x of source) {
        result.push({ previous, current: x });
        previous = x;
    }
    return result;
}

export function asPairs<T>(source: Iterable<T>): { previous: T; current: T }[] {
    const result: { previous: T; current: T }[] = [];
    let previous: T | undefined;
    let isFirst = true;
    for (const x of source) {
        if (isFirst) {
            previous = x;
            isFirst = false;
            continue;
        }
        result.push({ previous: previous!, current: x });
        previous = x;
    }
    return result;
}

/** ToChunks: consecutive runs with an equal criterium (C# Equals; `equals` defaults to .NET's default equality). */
export function toChunks<T, TCriterium>(
    source: Iterable<T>,
    criteriumGetter: (x: T) => TCriterium,
    equals: (a: TCriterium, b: TCriterium) => boolean = csEquals,
): { chunk: T[]; criterium: TCriterium }[] {
    const result: { chunk: T[]; criterium: TCriterium }[] = [];
    let last: { chunk: T[]; criterium: TCriterium } | undefined;
    for (const x of source) {
        const criterium = criteriumGetter(x);
        if (last && equals(last.criterium, criterium)) {
            last.chunk.push(x);
        } else {
            last = { chunk: [x], criterium };
            result.push(last);
        }
    }
    return result;
}

export function toChunksByShouldStartNew<T>(source: Iterable<T>, shouldStartNewGetter: (x: T) => boolean): T[][] {
    const result: T[][] = [];
    let last: T[] | undefined;
    for (const x of source) {
        // counter changes only when shouldStartNew; the first item opens a group either way
        const shouldStartNew = shouldStartNewGetter(x);
        if (!last || shouldStartNew) {
            last = [x];
            result.push(last);
        } else {
            last.push(x);
        }
    }
    return result;
}

/**
 * AddRange for HashSet<T>, and for Dictionary<TKey, TValue> (Add throws on an existing key unless overwrite): a Map, or
 * a dotnet/Collections Dictionary where the keys are value tuples or records.
 */
export function addRange<T>(hashSet: Set<T>, items: Iterable<T>): void;
export function addRange<TKey, TValue>(dictionary: Map<TKey, TValue> | Dictionary<TKey, TValue>, items: Iterable<{ key: TKey; value: TValue }>, overwrite: boolean): void;
export function addRange(target: Set<unknown> | Map<unknown, unknown> | Dictionary<unknown, unknown>, items: Iterable<unknown>, overwrite?: boolean): void {
    if (target instanceof Set) {
        for (const item of items) target.add(item);
        return;
    }
    if (target instanceof Dictionary) {
        for (const { key, value } of items as Iterable<{ key: unknown; value: unknown }>) {
            if (overwrite) {
                target.set(key, value);
            } else {
                target.add(key, value);
            }
        }
        return;
    }
    for (const { key, value } of items as Iterable<{ key: unknown; value: unknown }>) {
        if (!overwrite && target.has(key)) throw new ArgumentException(`An item with the same key has already been added. Key: ${String(key)}`);
        target.set(key, value);
    }
}
