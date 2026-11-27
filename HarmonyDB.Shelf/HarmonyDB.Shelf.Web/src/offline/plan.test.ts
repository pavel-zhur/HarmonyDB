// Taking the library's chords: what is still to take, the pages, what an answer does to the copy.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import type { Schemas } from '../api/client';
import { applyContents, backoff, chunks, keptChords, toTake } from './plan.ts';

test('only what the copy lacks is taken, in the library’s order — a taking stopped goes on, never from zero', () => {
    const held = new Set(['b', 'd']);
    assert.deepEqual(toTake(['a', 'b', 'c', 'd'], id => held.has(id), {}, {}), ['a', 'c']);
    assert.deepEqual(toTake(['a', 'b'], () => true, {}, {}), []);
    assert.deepEqual(toTake(['a', 'b', 'c'], () => false, {}, {}, new Set(['b'])), ['a', 'c']);
});

test('chords held by another address than the library’s now (their author edited them) are new: taken; the rest never again', () => {
    const keptBy = { own: 'https://own.example/?v=1', other: 'https://site.example/1' };
    const now = { own: 'https://own.example/?v=2', other: 'https://site.example/1' };
    assert.deepEqual(toTake(['own', 'other'], () => true, keptBy, now), ['own']);
    assert.deepEqual(toTake(['own', 'other'], () => true, keptBy, keptBy), []);
});

test('pages of the server’s size', () => {
    const ids = Array.from({ length: 120 }, (_, i) => `c${i}`);
    assert.deepEqual(chunks(ids, 50).map(x => x.length), [50, 50, 20]);
    assert.deepEqual(chunks([], 50), []);
});

test('an answer: open kept, not open and not in the library dropped, unavailable asked later', () => {
    const header = { id: 'a' } as Schemas['ChordsHeader'];
    const result = applyContents([
        { chords: 'a', state: 'open', header, content: { sheet: undefined } },
        { chords: 'b', state: 'notOpen' },
        { chords: 'c', state: 'notInLibrary' },
        { chords: 'd', state: 'unavailable' },
    ] as Schemas['OfflineContent'][]);
    assert.deepEqual(result.keep.map(x => x.key), ['chords:a']);
    assert.equal(result.keep[0]!.value.header, header);
    assert.deepEqual(result.drop, ['chords:b', 'chords:c']);
    assert.deepEqual(result.later, ['d']);
    assert.deepEqual(result.closed, ['b']);
    assert.deepEqual(result.gone, ['c']);
});

test('kept chords read as the chords’ own reading', () => {
    const kept = keptChords({ chords: 'x', state: 'open' } as Schemas['OfflineContent']);
    assert.equal(kept.id, 'x');
    assert.equal(kept.state, 'open');
    assert.deepEqual([kept.marks, kept.keys, kept.myNotes], [[], [], []]);
});

test('the pause after too many requests grows, to a minute at most', () => {
    assert.deepEqual([0, 1, 2, 10].map(backoff), [2000, 4000, 8000, 60_000]);
});
