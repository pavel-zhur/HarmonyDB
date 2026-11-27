// The copy's pure part: what the library holds, the pages joined, a list answered from the rows kept.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import type { Schemas } from '../api/client';
import { answerList, joinPages, keys, membership } from './lists.ts';

type SongRow = Schemas['SongRow'];

const row = (title: string, artist: string, extra: Partial<SongRow> = {}): SongRow => ({
    title, artists: [artist], myMarks: [], otherMarks: [], otherPeople: 0, chordsCount: 1, chordsIds: [], recordingsCount: 0,
    addedAt: '2026-01-01T00:00:00Z', ...extra,
});

const rows = [
    row('Звезда по имени Солнце', 'Кино', { songId: 's1', playlistId: 'p1', chordsIds: ['c1', 'c2'], myMarks: ['heart_red'] }),
    row('Ёлка', 'Алиса', { songId: 's2', playlistId: 'p1', chordsIds: ['c3'], otherMarks: ['fire'] }),
    row('Alone', 'Band', { chords: { id: 'c4' } as SongRow['chords'], chordsIds: ['c4'] }),
];
const kept = { rows, people: [] };

test('what the library holds: its songs by key, its chords by id', () => {
    const held = membership([rows]);
    assert.deepEqual([...held.songs].sort(), [keys.song('p1', 's1'), keys.song('p1', 's2')]);
    assert.deepEqual([...held.chords].sort(), ['c1', 'c2', 'c3', 'c4']);
});

test('pages are joined, each person once', () => {
    const person = { id: 'a', name: 'A', gone: false } as Schemas['PersonRef'];
    const joined = joinPages([{ total: 3, offset: 0, rows: rows.slice(0, 2), people: [person] }, { total: 3, offset: 2, rows: rows.slice(2), people: [person] }]);
    assert.equal(joined.rows.length, 3);
    assert.equal(joined.people.length, 1);
});

test('the search finds titles and artists, ё as е', () => {
    assert.deepEqual(answerList(kept, { q: 'елка' })!.rows.map(x => x.title), ['Ёлка']);
    assert.deepEqual(answerList(kept, { q: 'кино' })!.rows.map(x => x.title), ['Звезда по имени Солнце']);
});

test('a mark filters by my marks; with "all" by everyone’s', () => {
    assert.deepEqual(answerList(kept, { mark: 'heart_red' })!.rows.map(x => x.title), ['Звезда по имени Солнце']);
    assert.deepEqual(answerList(kept, { mark: 'fire', who: 'all' })!.rows.map(x => x.title), ['Ёлка']);
    assert.equal(answerList(kept, { mark: 'fire' })!.total, 0);
});

test('a filter by another person is not answered from the copy', () => {
    assert.equal(answerList(kept, { who: '1b6f0c9e-0000-0000-0000-000000000000' }), null);
});

test('sorted by title here, paged', () => {
    const page = answerList(kept, { sort: 'title', offset: 1, limit: 1 })!;
    assert.equal(page.total, 3);
    assert.equal(page.offset, 1);
    assert.deepEqual(page.rows.map(x => x.title), ['Ёлка']);
});

test('rows as the device can open them: a kept song page, else kept chords alone, else not kept', async () => {
    const { deviceRows } = await import('./lists.ts');
    const { rows: out, notKept } = deviceRows(rows, key => key === keys.song('p1', 's1'), id => id === 'c3');
    assert.equal(out[0]!.songId, 's1');
    assert.equal(out[1]!.songId, null);
    assert.equal(out[1]!.chords?.id, 'c3');
    assert.deepEqual(notKept.map(x => x.title), ['Alone']);
});
