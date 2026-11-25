// The client: the CSRF header on a writing request, and one repeat with the fresh token after csrf_failed. And a
// guest's action remembered for after signing in — not one left from long ago.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { answers, calls, reset } from '../testing/browser.ts';
import { api, call } from './client.ts';
import { registerPending, rememberPending, runPending } from '../data/signInOffer.ts';

test('csrf_failed: repeated once with the fresh token the answer gave', async () => {
    reset();
    document.cookie = 'shelf-csrf=old';
    let first = true;
    answers.set('POST /api/auth/sign-out', () => {
        if (first) {
            first = false;
            return { status: 403, body: { code: 'csrf_failed' }, cookie: 'shelf-csrf=new' };
        }
        return { status: 204 };
    });
    await call(api.POST('/api/auth/sign-out'));
    const sent = calls.filter(c => c.path === '/api/auth/sign-out').map(c => c.headers.get('X-CSRF-Token'));
    assert.deepEqual(sent, ['old', 'new']);
});

test('a remembered action is done after signing in, once; an old one is not', async () => {
    reset();
    const done: string[] = [];
    registerPending('test.action', async (args: string) => { done.push(args); });
    rememberPending('test.action', 'now');
    await runPending();
    await runPending();
    assert.deepEqual(done, ['now']);

    sessionStorage.setItem('shelf.pending', JSON.stringify({ name: 'test.action', args: 'old', at: Date.now() - 60 * 60 * 1000 }));
    await runPending();
    assert.deepEqual(done, ['now']);
});
