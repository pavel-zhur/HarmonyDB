// The guest's preview: honoured only when the page is framed by the app's own preview page; then the person's storage
// is neither read nor written, and every request goes without credentials.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { MemoryStorage, enterGuestPreview, guestPreviewOf, runsIn } from './guestMode.ts';

const origin = 'https://shelf.example';
const here = (pathname: string) => ({ origin, pathname });

/** A window: on its own (top), or framed by a parent at an address (of this origin unless said). */
function framed(parentPath: string | null, parentOrigin = origin) {
    const w = { location: here('/s/AbC') } as { location: { origin: string; pathname: string }; parent: unknown };
    if (parentPath === null) {
        w.parent = w;
    } else if (parentOrigin === origin) {
        w.parent = { location: here(parentPath) };
    } else {
        // another origin's window: its address cannot be read
        w.parent = { get location(): never { throw new Error('SecurityError: cross-origin'); } };
    }
    return w as unknown as Pick<Window, 'parent' | 'location'>;
}

test('a page on its own is never the preview, whatever its address', () => {
    assert.equal(guestPreviewOf(framed(null)), false);
});

test('framed by the app\'s preview page, it is the preview', () => {
    assert.equal(guestPreviewOf(framed('/s/AbCdEf/guest')), true);
});

test('framed by another page of the app, or by another site, it is not', () => {
    assert.equal(guestPreviewOf(framed('/s/AbCdEf')), false);
    assert.equal(guestPreviewOf(framed('/library')), false);
    assert.equal(guestPreviewOf(framed('/s/AbCdEf/guest/x')), false);
    assert.equal(guestPreviewOf(framed('/s/AbCdEf/guest', 'https://evil.example')), false);
});

test('in the preview the person\'s storage is neither read nor written, and requests go without credentials', async () => {
    const theirs = new MemoryStorage();
    theirs.setItem('shelf.look', '{"theme":"dark"}');
    const sent: Request[] = [];
    const w = {
        localStorage: theirs,
        sessionStorage: theirs,
        indexedDB: { open() { throw new Error('the person\'s database touched'); } },
        caches: { keys: async () => ['offline-cache-x'] },
        fetch: async (input: Request) => {
            sent.push(input);
            return new Response('{}');
        },
    } as unknown as Window & typeof globalThis;

    enterGuestPreview(w);

    assert.equal(w.localStorage.getItem('shelf.look'), null);
    w.localStorage.setItem('k', 'v');
    w.sessionStorage.setItem('s', 'v');
    assert.equal(theirs.getItem('k'), null);
    assert.equal(theirs.getItem('s'), null);
    assert.equal(theirs.getItem('shelf.look'), '{"theme":"dark"}');
    assert.equal(w.indexedDB, undefined);
    assert.equal(w.caches, undefined);

    await w.fetch(`${origin}/api/auth/session`);
    await w.fetch(new Request(`${origin}/api/share/AbC`, { credentials: 'same-origin' }));
    await w.fetch(`${origin}/api/marks`, { method: 'POST', body: '{}', credentials: 'include' });
    assert.deepEqual(sent.map(x => x.credentials), ['omit', 'omit', 'omit']);
});

test('the app framed is only ever the guest preview: framed otherwise it does not run', () => {
    // on its own: the app
    assert.equal(runsIn(framed(null)), 'app');
    // framed by the app's preview page: the guest's preview
    assert.equal(runsIn(framed('/s/AbCdEf/guest')), 'guest');
    // framed by any other page of the app (the consent, the profile, "Your AI" put in a frame) or by another site:
    // refused — nothing of the app runs there
    for (const parent of ['/', '/connect', '/profile', '/ai', '/s/AbCdEf', '/s/AbCdEf/guest/x'])
        assert.equal(runsIn(framed(parent)), 'refused', parent);
    assert.equal(runsIn(framed('/s/AbCdEf/guest', 'https://evil.example')), 'refused');
});
