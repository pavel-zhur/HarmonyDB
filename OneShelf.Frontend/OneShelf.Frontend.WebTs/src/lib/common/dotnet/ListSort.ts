// List<T>.Sort(Comparison<T>): .NET's introspective sort (ArraySortHelper<T>.IntrospectiveSort), which is not stable.
// The order of the elements the comparison calls equal depends on this very algorithm, so it is reproduced step by step.

import type { Comparison } from './Linq';

const introsortSizeThreshold = 16;

function log2(value: number): number {
    return 31 - Math.clz32(value);
}

export function listSort<T>(list: T[], comparison: Comparison<T>): void {
    if (list.length > 1) {
        introSort(list, 0, list.length, 2 * (log2(list.length) + 1), comparison);
    }
}

function swap<T>(keys: T[], i: number, j: number): void {
    const t = keys[i] as T;
    keys[i] = keys[j] as T;
    keys[j] = t;
}

function swapIfGreater<T>(keys: T[], comparison: Comparison<T>, i: number, j: number): void {
    if (comparison(keys[i] as T, keys[j] as T) > 0) {
        swap(keys, i, j);
    }
}

// The span keys[lo .. lo + length) of .NET is (keys, lo, length) here; indices inside are relative to lo.
function introSort<T>(keys: T[], lo: number, length: number, depthLimit: number, comparison: Comparison<T>): void {
    let partitionSize = length;
    while (partitionSize > 1) {
        if (partitionSize <= introsortSizeThreshold) {
            if (partitionSize === 2) {
                swapIfGreater(keys, comparison, lo, lo + 1);
                return;
            }

            if (partitionSize === 3) {
                swapIfGreater(keys, comparison, lo, lo + 1);
                swapIfGreater(keys, comparison, lo, lo + 2);
                swapIfGreater(keys, comparison, lo + 1, lo + 2);
                return;
            }

            insertionSort(keys, lo, partitionSize, comparison);
            return;
        }

        if (depthLimit === 0) {
            heapSort(keys, lo, partitionSize, comparison);
            return;
        }

        depthLimit--;

        const p = pickPivotAndPartition(keys, lo, partitionSize, comparison);

        // Note we've already partitioned around the pivot and do not have to move the pivot again.
        introSort(keys, lo + p + 1, partitionSize - (p + 1), depthLimit, comparison);
        partitionSize = p;
    }
}

function pickPivotAndPartition<T>(keys: T[], lo: number, length: number, comparison: Comparison<T>): number {
    const hi = length - 1;

    // Compute median-of-three. But also partition them, since we've done the comparison.
    const middle = hi >> 1;

    // Sort lo, mid and hi appropriately, then pick mid as the pivot.
    swapIfGreater(keys, comparison, lo, lo + middle); // swap the low with the mid point
    swapIfGreater(keys, comparison, lo, lo + hi); // swap the low with the high
    swapIfGreater(keys, comparison, lo + middle, lo + hi); // swap the middle with the high

    const pivot = keys[lo + middle] as T;
    swap(keys, lo + middle, lo + hi - 1);
    let left = 0;
    let right = hi - 1; // We already partitioned lo and hi and put the pivot in hi - 1. And we pre-increment & decrement below.

    while (left < right) {
        while (comparison(keys[lo + ++left] as T, pivot) < 0) {
            // advance
        }

        while (comparison(pivot, keys[lo + --right] as T) < 0) {
            // advance
        }

        if (left >= right) break;

        swap(keys, lo + left, lo + right);
    }

    // Put the pivot in the right location.
    if (left !== hi - 1) {
        swap(keys, lo + left, lo + hi - 1);
    }

    return left;
}

function heapSort<T>(keys: T[], lo: number, n: number, comparison: Comparison<T>): void {
    for (let i = n >> 1; i >= 1; i--) {
        downHeap(keys, lo, i, n, comparison);
    }

    for (let i = n; i > 1; i--) {
        swap(keys, lo, lo + i - 1);
        downHeap(keys, lo, 1, i - 1, comparison);
    }
}

function downHeap<T>(keys: T[], lo: number, i: number, n: number, comparison: Comparison<T>): void {
    const d = keys[lo + i - 1] as T;
    while (i <= n >> 1) {
        let child = 2 * i;
        if (child < n && comparison(keys[lo + child - 1] as T, keys[lo + child] as T) < 0) {
            child++;
        }

        if (!(comparison(d, keys[lo + child - 1] as T) < 0)) break;

        keys[lo + i - 1] = keys[lo + child - 1] as T;
        i = child;
    }

    keys[lo + i - 1] = d;
}

function insertionSort<T>(keys: T[], lo: number, length: number, comparison: Comparison<T>): void {
    for (let i = 0; i < length - 1; i++) {
        const t = keys[lo + i + 1] as T;

        let j = i;
        while (j >= 0 && comparison(t, keys[lo + j] as T) < 0) {
            keys[lo + j + 1] = keys[lo + j] as T;
            j--;
        }

        keys[lo + j + 1] = t;
    }
}
