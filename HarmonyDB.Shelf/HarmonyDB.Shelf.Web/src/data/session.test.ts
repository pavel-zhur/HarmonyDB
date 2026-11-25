// A sign-out made with no connection: told to the server later, and forgotten only once the server confirms it.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { answers, reset } from '../testing/browser.ts';
import { finishSignOut } from './session.ts';

const pending = () => localStorage.getItem('shelf.signOutPending');

test('the server failing or refusing for a while keeps the sign-out to tell again', async () => {
    for (const status of [503, 500, 429]) {
        reset();
        localStorage.setItem('shelf.signOutPending', 'true');
        answers.set('POST /api/auth/sign-out', () => ({ status, body: { code: status === 429 ? 'too_many_requests' : 'unavailable' } }));
        assert.equal(await finishSignOut(), false, `${status}`);
        assert.equal(pending(), 'true', `${status}`);
    }
});

test('the server’s yes, or no session there any more, forgets it', async () => {
    for (const answer of [{ status: 204 }, { status: 401, body: { code: 'sign_in_required' } }]) {
        reset();
        localStorage.setItem('shelf.signOutPending', 'true');
        answers.set('POST /api/auth/sign-out', () => answer);
        assert.equal(await finishSignOut(), true);
        assert.equal(pending(), null);
    }
});
