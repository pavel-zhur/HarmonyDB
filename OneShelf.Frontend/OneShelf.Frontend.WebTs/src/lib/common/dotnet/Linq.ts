// LINQ and list operators with .NET's semantics: ordering, stability, equality (keyOf) and the exceptions they throw.

import type { Grouping } from './Collections';
import { keyOf } from './Equality';
import { ArgumentOutOfRangeException, IndexOutOfRangeException, InvalidOperationException, OverflowException } from './exceptions';
import { toFloat } from './Numbers';

export type Comparison<T> = (a: T, b: T) => number;

/** list[index], throwing as the List<T> / array indexer does. */
export function at<T>(list: readonly T[], index: number): T {
    if (!Number.isInteger(index) || index < 0 || index >= list.length) {
        throw new ArgumentOutOfRangeException(undefined, 'Index was out of range. Must be non-negative and less than the size of the collection.');
    }

    return list[index] as T;
}

/** span[index] (a ReadOnlyMemory<T>.Span or an array), throwing as their indexer does. */
export function spanAt<T>(span: readonly T[], index: number): T {
    if (!Number.isInteger(index) || index < 0 || index >= span.length) throw new IndexOutOfRangeException();
    return span[index] as T;
}

/** list[^fromEnd]. */
export function atFromEnd<T>(list: readonly T[], fromEnd: number): T {
    return at(list, list.length - fromEnd);
}

/** Enumerable.Range. */
export function range(start: number, count: number): number[] {
    if (count < 0 || start + count - 1 > 2147483647) throw new ArgumentOutOfRangeException(undefined, 'count');
    return Array.from({ length: count }, (_, i) => start + i);
}

/** Enumerable.Repeat. */
export function repeat<T>(element: T, count: number): T[] {
    if (count < 0) throw new ArgumentOutOfRangeException(undefined, 'count');
    return Array.from({ length: count }, () => element);
}

/** Enumerable.TakeWhile. */
export function takeWhile<T>(source: Iterable<T>, predicate: (item: T) => boolean): T[] {
    const result: T[] = [];
    for (const item of source) {
        if (!predicate(item)) break;
        result.push(item);
    }

    return result;
}

/** Enumerable.SkipWhile. */
export function skipWhile<T>(source: Iterable<T>, predicate: (item: T) => boolean): T[] {
    const result: T[] = [];
    let skipping = true;
    for (const item of source) {
        if (skipping && predicate(item)) continue;
        skipping = false;
        result.push(item);
    }

    return result;
}

/** Enumerable.GroupBy: groups in the order their keys first appear, elements in the source order. */
export function groupBy<T, K>(source: Iterable<T>, keySelector: (item: T) => K): Grouping<K, T>[] {
    const groups = new Map<string, Grouping<K, T>>();
    for (const item of source) {
        const key = keySelector(item);
        const k = keyOf(key);
        let group = groups.get(k);
        if (!group) {
            group = { key, items: [] };
            groups.set(k, group);
        }

        group.items.push(item);
    }

    return [...groups.values()];
}

/** Enumerable.Distinct: the first occurrences, in the source order. */
export function distinct<T>(source: Iterable<T>): T[] {
    const seen = new Set<string>();
    const result: T[] = [];
    for (const item of source) {
        const k = keyOf(item);
        if (!seen.has(k)) {
            seen.add(k);
            result.push(item);
        }
    }

    return result;
}

/** Enumerable.Except: the distinct elements of the first sequence that are not in the second, in the first's order. */
export function except<T>(first: Iterable<T>, second: Iterable<T>): T[] {
    const excluded = new Set<string>();
    for (const item of second) excluded.add(keyOf(item));
    const result: T[] = [];
    for (const item of first) {
        const k = keyOf(item);
        if (!excluded.has(k)) {
            excluded.add(k);
            result.push(item);
        }
    }

    return result;
}

/** Enumerable.Contains / List.Contains with the default equality. */
export function contains<T>(source: Iterable<T>, value: T): boolean {
    const k = keyOf(value);
    for (const item of source) {
        if (keyOf(item) === k) return true;
    }

    return false;
}

/** Enumerable.First. */
export function first<T>(source: Iterable<T>, predicate?: (item: T) => boolean): T {
    for (const item of source) {
        if (!predicate || predicate(item)) return item;
    }

    throw new InvalidOperationException(predicate ? 'Sequence contains no matching element' : 'Sequence contains no elements');
}

/** Enumerable.Single. */
export function single<T>(source: Iterable<T>, predicate?: (item: T) => boolean): T {
    let found = false;
    let result: T | undefined;
    for (const item of source) {
        if (predicate && !predicate(item)) continue;
        if (found) throw new InvalidOperationException(predicate ? 'Sequence contains more than one matching element' : 'Sequence contains more than one element');
        found = true;
        result = item;
    }

    if (!found) throw new InvalidOperationException(predicate ? 'Sequence contains no matching element' : 'Sequence contains no elements');
    return result as T;
}

/** Enumerable.SingleOrDefault (for reference types: null when empty). */
export function singleOrDefault<T>(source: Iterable<T>): T | null {
    let found = false;
    let result: T | null = null;
    for (const item of source) {
        if (found) throw new InvalidOperationException('Sequence contains more than one element');
        found = true;
        result = item;
    }

    return result;
}

/** Comparer<float>.Default / Comparer<double>.Default: NaN is less than any number. */
export function compareNumbers(a: number, b: number): number {
    if (a < b) return -1;
    if (a > b) return 1;
    if (a === b) return 0;
    if (Number.isNaN(a)) return Number.isNaN(b) ? 0 : -1;
    return 1;
}

/**
 * Comparer<string>.Default. It is culture-sensitive in .NET, and the frontend runs with InvariantGlobalization, where
 * culture-sensitive comparisons are ordinal: UTF-16 code units, as JavaScript's < compares.
 */
export function compareStrings(a: string | null, b: string | null): number {
    if (a === b) return 0;
    if (a === null) return -1;
    if (b === null) return 1;
    return a < b ? -1 : 1;
}

/** Comparer<bool>.Default: false < true. */
export function compareBooleans(a: boolean, b: boolean): number {
    return a === b ? 0 : a ? 1 : -1;
}

/** OrderBy(...).ThenBy(...): a stable sort by the comparison into a new array. */
export function orderBy<T>(source: Iterable<T>, comparison: Comparison<T>): T[] {
    return [...source].sort(comparison);
}

/**
 * OrderBy(keySelector) where the key selector matters beyond its value (it can throw, or costs): the key of every
 * element is computed once, in order, then a stable sort by the keys. The original runs in the browser, where .NET's
 * LINQ is size-optimized: an ordering is sorted whole for whatever consumes it (First() and Last() too), every level's
 * key computed for every element, level by level (established with a Blazor WebAssembly 9.0 application; the server
 * runtime's First() computes the further levels' keys only for ties and new minimums).
 */
export function orderByKey<T, K>(source: Iterable<T>, keySelector: (item: T) => K, comparison: Comparison<K>): T[] {
    const items = [...source];
    const keys = items.map(keySelector);
    return items.map((_, i) => i).sort((a, b) => comparison(keys[a]!, keys[b]!) || a - b).map(i => items[i]!);
}

/** MinBy: the first element with the minimal key. */
export function minBy<T, K>(source: Iterable<T>, keySelector: (item: T) => K, comparison: Comparison<K>): T {
    let result: T | undefined;
    let resultKey: K | undefined;
    let any = false;
    for (const item of source) {
        const key = keySelector(item);
        if (!any || comparison(key, resultKey as K) < 0) {
            result = item;
            resultKey = key;
            any = true;
        }
    }

    if (!any) throw new InvalidOperationException('Sequence contains no elements');
    return result as T;
}

/** MaxBy: the first element with the maximal key. */
export function maxBy<T, K>(source: Iterable<T>, keySelector: (item: T) => K, comparison: Comparison<K>): T {
    return minBy(source, keySelector, (a, b) => comparison(b, a));
}

/** Enumerable.Min() of ints. */
export function minInt(source: Iterable<number>): number {
    let result: number | undefined;
    for (const item of source) {
        if (result === undefined || item < result) result = item;
    }

    if (result === undefined) throw new InvalidOperationException('Sequence contains no elements');
    return result;
}

/** Enumerable.Max() of ints. */
export function maxInt(source: Iterable<number>): number {
    let result: number | undefined;
    for (const item of source) {
        if (result === undefined || item > result) result = item;
    }

    if (result === undefined) throw new InvalidOperationException('Sequence contains no elements');
    return result;
}

/** Enumerable.Max() of nullable ints: null when there are no non-null values. */
export function maxNullableInt(source: Iterable<number | null>): number | null {
    let result: number | null = null;
    for (const item of source) {
        if (item !== null && (result === null || item > result)) result = item;
    }

    return result;
}

/** Enumerable.Max() of floats: NaN values are skipped unless all are NaN. */
export function maxFloat(source: Iterable<number>): number {
    const iterator = source[Symbol.iterator]();
    let next = iterator.next();
    if (next.done) throw new InvalidOperationException('Sequence contains no elements');
    let value = next.value;
    while (Number.isNaN(value)) {
        next = iterator.next();
        if (next.done) return value;
        value = next.value;
    }

    for (next = iterator.next(); !next.done; next = iterator.next()) {
        if (next.value > value) value = next.value;
    }

    return value;
}

/** Enumerable.Sum() of ints (checked). */
export function sumInt(source: Iterable<number>): number {
    let sum = 0;
    for (const item of source) {
        sum += item;
        if (sum > 2147483647 || sum < -2147483648) throw new OverflowException();
    }

    return sum;
}

/** Enumerable.Sum() of floats: accumulated as a double, returned as a float. */
export function sumFloat(source: Iterable<number>): number {
    let sum = 0;
    for (const item of source) sum += item;
    return toFloat(sum);
}

/** Enumerable.Average() of floats: summed and divided as doubles, returned as a float. */
export function averageFloat(source: Iterable<number>): number {
    let sum = 0;
    let count = 0;
    for (const item of source) {
        sum += item;
        count++;
    }

    if (count === 0) throw new InvalidOperationException('Sequence contains no elements');
    return toFloat(sum / count);
}
