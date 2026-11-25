// The public page is always a guest: its client asks for no session and sends no CSRF token — its entry leaves the
// client's CSRF step off (the app's entry turns it on).

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync, readdirSync } from 'node:fs';
import { answers, calls, reset } from '../testing/browser.ts';
import { api, call } from '../api/client.ts';

test('the public page asks for media with no session request and no CSRF token', async () => {
    reset();
    // a person signed in to the app on the same host: its token cookie is there, and still not used
    document.cookie = 'shelf-csrf=the-apps';
    answers.set('POST /api/share/AbCdEfGhIjKlMnOpQrStUvWx/media', () => ({ body: { stems: [], expiresAt: '2030-01-01T00:00:00Z' } }));
    answers.set('POST /api/media/chords', () => ({ body: { stems: [], expiresAt: '2030-01-01T00:00:00Z' } }));
    await call(api.POST('/api/share/{token}/media', { params: { path: { token: 'AbCdEfGhIjKlMnOpQrStUvWx' } } }));
    await call(api.POST('/api/media/chords', { body: { id: 'ha-1' } }));
    assert.deepEqual(calls.map(c => `${c.method} ${c.path}`), ['POST /api/share/AbCdEfGhIjKlMnOpQrStUvWx/media', 'POST /api/media/chords']);
    assert.ok(calls.every(c => c.headers.get('X-CSRF-Token') === null));
});

test('nothing of the public page turns the CSRF step on', () => {
    const folder = new URL('.', import.meta.url);
    for (const name of readdirSync(folder).filter(x => /\.tsx?$/.test(x) && !x.endsWith('.test.ts')))
        assert.ok(!readFileSync(new URL(name, folder), 'utf8').includes('sendCsrf'), name);
});
