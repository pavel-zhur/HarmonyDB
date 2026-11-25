import { keyOf } from './Equality';
import { ArgumentException, KeyNotFoundException } from './exceptions';
import type { Out } from './Out';

/**
 * Dictionary<TKey, TValue> with .NET's default key equality (see keyOf), for keys that a JavaScript Map would compare
 * differently (value tuples, records). Enumerates in insertion order, as a .NET Dictionary does when nothing is removed
 * (nothing is removed from the dictionaries of this library, so Remove is not provided).
 */
export class Dictionary<K, V> implements Iterable<[K, V]> {
    private readonly entries = new Map<string, [K, V]>();

    get count(): number {
        return this.entries.size;
    }

    /** The indexer's getter: throws KeyNotFoundException. */
    get(key: K): V {
        const entry = this.entries.get(keyOf(key));
        if (!entry) throw new KeyNotFoundException(key);
        return entry[1];
    }

    /** The indexer's setter: replaces the value in place or adds. */
    set(key: K, value: V): void {
        const k = keyOf(key);
        const entry = this.entries.get(k);
        if (entry) {
            entry[1] = value;
        } else {
            this.entries.set(k, [key, value]);
        }
    }

    /** Add: throws ArgumentException when the key exists. */
    add(key: K, value: V): void {
        const k = keyOf(key);
        if (this.entries.has(k)) throw new ArgumentException(`An item with the same key has already been added. Key: ${String(key)}`);
        this.entries.set(k, [key, value]);
    }

    /** TryAdd. */
    tryAdd(key: K, value: V): boolean {
        const k = keyOf(key);
        if (this.entries.has(k)) return false;
        this.entries.set(k, [key, value]);
        return true;
    }

    containsKey(key: K): boolean {
        return this.entries.has(keyOf(key));
    }

    tryGetValue(key: K, value: Out<V | undefined>): boolean {
        const entry = this.entries.get(keyOf(key));
        value.value = entry?.[1];
        return entry !== undefined;
    }

    getValueOrDefault(key: K, defaultValue: V): V {
        const entry = this.entries.get(keyOf(key));
        return entry ? entry[1] : defaultValue;
    }

    clear(): void {
        this.entries.clear();
    }

    keys(): K[] {
        return [...this.entries.values()].map(e => e[0]);
    }

    values(): V[] {
        return [...this.entries.values()].map(e => e[1]);
    }

    [Symbol.iterator](): Iterator<[K, V]> {
        return [...this.entries.values()].map(e => [e[0], e[1]] as [K, V])[Symbol.iterator]();
    }
}

/** A grouping (IGrouping<TKey, TElement>): the key and the elements in their source order. */
export interface Grouping<K, T> {
    readonly key: K;
    readonly items: T[];
}

/** ILookup<TKey, TElement>: groups in the order their keys first appear; the indexer gives an empty list for a missing key. */
export class Lookup<K, T> implements Iterable<Grouping<K, T>> {
    private readonly groups = new Map<string, Grouping<K, T>>();

    /** ToLookup: the pairs of a key and an element, in the source order. */
    constructor(pairs: Iterable<readonly [K, T]>) {
        for (const [key, item] of pairs) {
            const k = keyOf(key);
            let group = this.groups.get(k);
            if (!group) {
                group = { key, items: [] };
                this.groups.set(k, group);
            }

            group.items.push(item);
        }
    }

    get count(): number {
        return this.groups.size;
    }

    get(key: K): readonly T[] {
        return this.groups.get(keyOf(key))?.items ?? [];
    }

    contains(key: K): boolean {
        return this.groups.has(keyOf(key));
    }

    [Symbol.iterator](): Iterator<Grouping<K, T>> {
        return this.groups.values();
    }
}
