// The local-first store with a server the test answers: one person's changes never go as another's, signing out keeps
// only what was not sent, undo before and after sending.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { answers, calls, reset } from '../testing/browser.ts';
import { attach, enqueue, exchange, getStatus, getView, storageKey, undoChange } from './store.ts';
import { markKey, type SyncResponse } from './state.ts';

const empty: SyncResponse = { results: [], watermark: 1, marks: [], keys: [], notes: [], recent: [], playlists: [] };

function server(accountId: string) {
    answers.set('GET /api/auth/session', () => ({ body: { accountId, permissions: [] } }));
    answers.set('POST /api/sync', call => {
        const body = call.body as { accountId: string; changes: unknown[] };
        // the server refuses a batch of another person than the session's
        if (body.accountId !== accountId) return { status: 409, body: { code: 'wrong_account' } };
        const changes = body.changes;
        return { body: { ...empty, watermark: 2, results: changes.map((_, i) => ({ applied: true, undo: `undo-${i}` })) } };
    });
    answers.set('POST /api/sync/undo', () => ({ status: 204 }));
}

const star = { type: 'mark', chords: 'c1', kind: 'star', on: true } as const;

test('a change is seen at once and sent; the server\'s undo token undoes it after', async () => {
    reset();
    server('A');
    attach('A');
    await exchange();
    const id = enqueue(star)!;
    assert.ok(getView().marks[markKey('c1', 'star')], 'seen at once');
    await exchange();
    assert.equal(getStatus().queued, 0);
    const sync = calls.filter(c => c.path === '/api/sync').at(-1)!;
    assert.equal((sync.body as { changes: unknown[] }).changes.length, 1);
    await undoChange(id);
    const undo = calls.find(c => c.path === '/api/sync/undo');
    assert.deepEqual(undo?.body, { token: 'undo-0' });
    attach(null);
});

test('undo before sending: the change leaves the queue, nothing is sent', async () => {
    reset();
    server('A');
    attach('A');
    await exchange();
    const id = enqueue(star)!;
    await undoChange(id);
    assert.equal(getStatus().queued, 0);
    assert.equal(getView().marks[markKey('c1', 'star')], undefined);
    calls.length = 0;
    await exchange();
    assert.equal((calls.find(c => c.path === '/api/sync')?.body as { changes: unknown[] }).changes.length, 0);
    attach(null);
});

test('the browser became another person\'s: the queue is not sent as them, and keeps only what was not sent', async () => {
    reset();
    server('A');
    attach('A');
    await exchange();
    enqueue(star);
    // in another tab A signed out and B signed in
    server('B');
    calls.length = 0;
    await exchange();
    await exchange();
    const syncs = calls.filter(c => c.path === '/api/sync').map(c => c.body as { accountId: string; changes: unknown[] });
    assert.ok(syncs.some(b => b.accountId === 'B'), 'the store follows B');
    assert.ok(syncs.every(b => b.accountId === 'A' || b.changes.length === 0), 'nothing of A\'s goes as B');
    const kept = JSON.parse(localStorage.getItem(storageKey('A'))!);
    assert.equal(kept.queue.length, 1, 'A\'s unsent change waits for A');
    assert.deepEqual(kept.base.marks, {}, 'A\'s marks, keys and notes are not kept');
    attach(null);
});

test('two tabs of one person: a change is merged into what the other tab wrote', async () => {
    reset();
    server('A');
    attach('A');
    await exchange();
    // the other tab queued a change meanwhile
    const other = JSON.parse(localStorage.getItem(storageKey('A'))!);
    other.queue.push({ id: 'other', change: { type: 'mark', chords: 'c2', kind: 'fire', on: true, at: new Date().toISOString() } });
    localStorage.setItem(storageKey('A'), JSON.stringify(other));
    enqueue(star);
    const kept = JSON.parse(localStorage.getItem(storageKey('A'))!);
    assert.equal(kept.queue.length, 2);
    attach(null);
});

test('anyone else this browser held keeps only their unsent changes', async () => {
    reset();
    server('A');
    const marks = { [markKey('c9', 'moon')]: { chords: 'c9', kind: 'moon', on: true, at: 't' } };
    localStorage.setItem(storageKey('Y'), JSON.stringify({ base: { marks, keys: {}, notes: {}, recent: {}, watermark: 5 }, queue: [], sent: [] }));
    localStorage.setItem(storageKey('Z'), JSON.stringify({ base: { marks, keys: {}, notes: {}, recent: {}, watermark: 5 }, queue: [{ id: 'z1', change: star }], sent: [] }));
    attach('A');
    await exchange();
    assert.equal(localStorage.getItem(storageKey('Y')), null, 'nothing unsent: nothing kept');
    const z = JSON.parse(localStorage.getItem(storageKey('Z'))!);
    assert.deepEqual(z.base.marks, {});
    assert.equal(z.queue.length, 1);
    attach(null);
});
