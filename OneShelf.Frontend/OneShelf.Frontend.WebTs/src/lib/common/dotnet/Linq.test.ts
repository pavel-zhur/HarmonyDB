import { describe, expect, it } from 'vitest';
import { compareNumbers, orderByKey } from './Linq';

describe('orderByKey', () => {
    it('computes each key once, in order, also for one element; sorts stably', () => {
        const trace: number[] = [];
        const key = (x: { i: number; k: number }) => {
            trace.push(x.i);
            return x.k;
        };

        const items = [{ i: 0, k: 2 }, { i: 1, k: 1 }, { i: 2, k: 2 }, { i: 3, k: 1 }];
        expect(orderByKey(items, key, compareNumbers).map(x => x.i)).toEqual([1, 3, 0, 2]);
        expect(trace).toEqual([0, 1, 2, 3]);

        trace.length = 0;
        expect(orderByKey([{ i: 5, k: 0 }], key, compareNumbers).map(x => x.i)).toEqual([5]);
        expect(trace).toEqual([5]);
    });

    it('fails when any key fails, as the browser\'s .NET sorts the whole sequence', () => {
        expect(() => orderByKey([1, 2], x => {
            if (x === 2) throw new Error('key');
            return x;
        }, compareNumbers)[0]).toThrow('key');
    });
});
