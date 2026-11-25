import { test } from 'node:test';
import assert from 'node:assert/strict';
import { effective, emptyState, keyKey, markKey, mergeBase, settle, type LocalState, type SyncResponse } from './state.ts';

const answer = (over: Partial<SyncResponse> = {}): SyncResponse =>
    ({ results: [], watermark: 10, marks: [], keys: [], notes: [], recent: [], playlists: [], ...over });

test('the queue is seen on top of the base, in its order', () => {
    const state: LocalState = {
        base: { ...emptyState.base, marks: { [markKey('c1', 'star')]: { chords: 'c1', kind: 'star', on: true, at: 't0' } } },
        queue: [
            { id: 'a', change: { type: 'mark', chords: 'c1', kind: 'star', on: false, at: 't1' } },
            { id: 'b', change: { type: 'key', chords: 'c1', semitones: 2, at: 't2' } },
            { id: 'c', change: { type: 'key', chords: 'c1', semitones: null, at: 't3' } },
            { id: 'd', change: { type: 'key', chords: 'c1', playlistId: 'p', semitones: -3, at: 't4' } },
        ],
        sent: [],
    };
    const view = effective(state);
    assert.equal(view.marks[markKey('c1', 'star')], undefined);
    assert.equal(view.keys[keyKey('c1', null)], undefined);
    assert.equal(view.keys[keyKey('c1', 'p')]?.semitones, -3);
    assert.ok(state.base.marks[markKey('c1', 'star')], 'the base is not touched');
});

test('a stale change leaves the queue; the base keeps the newer state; changes made meanwhile stay', () => {
    const state: LocalState = {
        base: { ...emptyState.base, watermark: 5, marks: { [markKey('c1', 'star')]: { chords: 'c1', kind: 'star', on: true, at: 't0' } } },
        queue: [
            { id: 'a', change: { type: 'mark', chords: 'c1', kind: 'star', on: false, at: 't1' } },
            { id: 'later', change: { type: 'mark', chords: 'c2', kind: 'fire', on: true, at: 't3' } },
        ],
        sent: [],
    };
    const sent = state.queue.slice(0, 1);
    const { state: next, outcomes } = settle(state, sent, answer({ results: [{ applied: false, code: 'stale' }] }), 5);
    assert.equal(outcomes[0]?.result.code, 'stale');
    assert.deepEqual(next.queue.map(x => x.id), ['later']);
    assert.ok(next.base.marks[markKey('c1', 'star')], 'the newer state elsewhere (still on) is what is seen');
    assert.equal(next.base.watermark, 10);
});

test('an answer since a watermark changes only what it names; removed things leave', () => {
    const base = { ...emptyState.base, watermark: 5,
        marks: { [markKey('c1', 'star')]: { chords: 'c1', kind: 'star', on: true, at: 't0' }, [markKey('c2', 'fire')]: { chords: 'c2', kind: 'fire', on: true, at: 't0' } } };
    const next = mergeBase(base, answer({ marks: [{ chords: 'c1', kind: 'star', on: false, at: 't5' }], keys: [{ chords: 'c3', playlistId: null, semitones: 4 }] }), 5);
    assert.deepEqual(Object.keys(next.marks), [markKey('c2', 'fire')]);
    assert.equal(next.keys[keyKey('c3', null)]?.semitones, 4);
});

test('an answer from the start replaces the base', () => {
    const base = { ...emptyState.base, watermark: 5, marks: { [markKey('c1', 'star')]: { chords: 'c1', kind: 'star', on: true, at: 't0' } } };
    const next = mergeBase(base, answer({ marks: [{ chords: 'c9', kind: 'moon', on: true, at: 't5' }] }), null);
    assert.deepEqual(Object.keys(next.marks), [markKey('c9', 'moon')]);
});

test('a watermark the server never gave (a database made anew) is answered with everything: the base is replaced', () => {
    const base = { ...emptyState.base, watermark: 50, marks: { [markKey('c1', 'star')]: { chords: 'c1', kind: 'star', on: true, at: 't0' } } };
    const next = mergeBase(base, answer({ watermark: 10 }), 50);
    assert.deepEqual(next.marks, {});
});
