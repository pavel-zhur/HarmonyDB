// The sources a row shows: the different ones of its chords, as the device knows them for the person.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import '../testing/browser.ts';
import { sourcesOf, type Label } from './labels.ts';

test('a row shows the different sources of its chords, in their order, at most three; chords with no label known are skipped', () => {
    const labels = new Map<string, Label>([
        ['a', { key: 'one', title: 'One' }],
        ['b', { key: 'two', title: 'Two' }],
        ['c', { key: 'one', title: 'One' }],
        ['d', { key: 'three', title: 'Three' }],
        ['e', { key: 'four', title: 'Four' }],
    ]);
    const of = (id: string) => labels.get(id);
    assert.deepEqual(sourcesOf(of, ['a', 'b', 'c', 'x', 'd', 'e']).map(x => x.key), ['one', 'two', 'three']);
    assert.deepEqual(sourcesOf(of, ['x']), []);
});
