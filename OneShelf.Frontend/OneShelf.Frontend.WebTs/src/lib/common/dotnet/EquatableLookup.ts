// ILookup<TKey, TElement> made by ToLookup with EqualityComparer<TKey>.Default, for a key type with its own Equals
// and GetHashCode (as System.Uri): the groups are found as .NET's Lookup finds them — by the hash code, then by
// Equals with the group's key; among groups with the same hash code, the one made last is tried first.

/** IEquatable<T> with its GetHashCode. */
export interface IEquatable<T> {
    equals(other: T | null): boolean;
    getHashCode(): number;
}

export interface EquatableGrouping<K, T> {
    readonly key: K;
    readonly elements: T[];
}

export class EquatableLookup<K extends IEquatable<K>, T> implements Iterable<EquatableGrouping<K, T>> {
    private readonly byHash = new Map<number, EquatableGrouping<K, T>[]>();
    private readonly groups: EquatableGrouping<K, T>[] = [];

    /** Enumerable.ToLookup(source, keySelector, elementSelector) */
    static from<TSource, K extends IEquatable<K>, T>(source: Iterable<TSource>, keySelector: (x: TSource) => K, elementSelector: (x: TSource) => T): EquatableLookup<K, T> {
        const lookup = new EquatableLookup<K, T>();
        for (const x of source) {
            const key = keySelector(x);
            let group = lookup.find(key);
            if (group === undefined) {
                group = { key, elements: [] };
                const hash = key.getHashCode() & 0x7fffffff;
                const sameHash = lookup.byHash.get(hash);
                if (sameHash === undefined) lookup.byHash.set(hash, [group]);
                else sameHash.push(group);
                lookup.groups.push(group);
            }

            group.elements.push(elementSelector(x));
        }

        return lookup;
    }

    private find(key: K): EquatableGrouping<K, T> | undefined {
        const sameHash = this.byHash.get(key.getHashCode() & 0x7fffffff);
        if (sameHash === undefined) return undefined;
        for (let i = sameHash.length - 1; i >= 0; i--) {
            if (sameHash[i]!.key.equals(key)) return sameHash[i];
        }

        return undefined;
    }

    /** `lookup[key]`: the elements, none for a key not there. */
    get(key: K): readonly T[] {
        return this.find(key)?.elements ?? [];
    }

    contains(key: K): boolean {
        return this.find(key) !== undefined;
    }

    get count(): number {
        return this.groups.length;
    }

    [Symbol.iterator](): Iterator<EquatableGrouping<K, T>> {
        return this.groups[Symbol.iterator]();
    }
}
