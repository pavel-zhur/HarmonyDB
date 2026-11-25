// The local-first store of small changes: marks, keys ("mine" and "here"), notes, "opened".
//
// A change is seen at once, kept in a queue in this browser (localStorage, per account: it survives a closed tab and
// a lost connection), and sent in a batch to /api/sync a moment later; the answer is merged (state.ts). After every
// change but "opened" a toast offers "Undo" for a few seconds: before sending it just leaves the queue; after, the
// server's undo token undoes it. A guest's change offers signing in and is made after it.
//
// One person, one state: the store follows the session (attach); every batch names its person, and the server refuses
// it for another (409 wrong_account: signed out and in as someone else in another tab) — then the store follows who
// the browser is now. Two tabs of
// one person share the state through localStorage: each change is merged into what the other tab wrote, not over it.
// Signing out leaves only the changes not sent yet.
//
// The surface is ./index.ts.

import { ApiError, api, call } from '../api/client';
import { errorText } from '../api/errors';
import { i18n } from '../i18n';
import { toast } from '../app/toasts';
import { queryClient } from '../data/queryClient';
import { offerSignIn, registerPending, rememberPending, type SignInWhat } from '../data/signInOffer';
import { effective, emptyState, mergeStates, settle, signedOut, type Base, type Change, type LocalState, type Queued } from './state';
import { attachOffline } from '../offline';
import { trimDrafts } from '../screens/editor/draft';

const maxBatch = 500;
const sendDelay = 800;
const retryDelays = [5_000, 15_000, 60_000];

let account: string | null = null;
let state: LocalState = emptyState;
let view: Base = emptyState.base;
let busy = false;
let failures = 0;
let sendTimer: ReturnType<typeof setTimeout> | undefined;
let lastPull = 0;

/** Undo asked for a change already on its way: done when its answer comes. */
const undoWanted = new Set<string>();
/** The undo tokens of sent changes, while their toast may still be pressed. */
const undoTokens = new Map<string, string>();
/** The changes on their way now. */
const inFlight = new Set<string>();

const listeners = new Set<() => void>();
function emit() {
    view = effective(state);
    updateStatus();
    listeners.forEach(listener => listener());
}

export function subscribe(listener: () => void): () => void {
    listeners.add(listener);
    return () => listeners.delete(listener);
}

export const getView = () => view;
export const getStatus = () => statusSnapshot;
let statusSnapshot = { busy: false, queued: 0 };
function updateStatus() {
    if (statusSnapshot.busy !== busy || statusSnapshot.queued !== state.queue.length) statusSnapshot = { busy, queued: state.queue.length };
}

export const storageKey = (id: string) => `shelf.local.v1.${id}`;

function load(id: string): LocalState {
    try {
        const saved = JSON.parse(localStorage.getItem(storageKey(id)) ?? 'null') as Partial<LocalState> | null;
        return saved?.base && Array.isArray(saved.queue) ? { base: saved.base, queue: saved.queue, sent: saved.sent ?? [] } : emptyState;
    } catch {
        return emptyState;
    }
}

function write(id: string, value: LocalState) {
    try {
        localStorage.setItem(storageKey(id), JSON.stringify(value));
    } catch {
        // no storage: the queue lives until the page closes
    }
}

/** A change of the state: merged into what another tab may have written meanwhile, then kept. */
function update(change: (current: LocalState) => LocalState) {
    if (account === null) return;
    state = change(mergeStates(state, load(account)));
    write(account, state);
    emit();
}

/** The person whose changes these are (null: a guest). The one before, if another, keeps only what was not sent. */
export function attach(accountId: string | null): void {
    if (attached && accountId === account) return;
    attached = true;
    clearTimeout(sendTimer);
    if (account !== null) write(account, signedOut(mergeStates(state, load(account))));
    // anyone else this browser held (a tab closed while signed in, a session ended on the server): only what they
    // did not send is kept, not their marks, keys, notes or recent
    trimOthers(accountId);
    account = accountId;
    // the library's copy on the device follows the same person (offline/index.ts)
    void attachOffline(accountId);
    // and the editor's drafts (screens/editor/draft.ts)
    trimDrafts(accountId);
    state = accountId === null ? emptyState : load(accountId);
    undoWanted.clear();
    undoTokens.clear();
    emit();
    if (accountId !== null) void exchange();
}

let attached = false;

function trimOthers(keep: string | null) {
    try {
        const prefix = storageKey('');
        const others = Array.from({ length: localStorage.length }, (_, i) => localStorage.key(i))
            .filter((key): key is string => !!key && key.startsWith(prefix) && key !== (keep === null ? null : storageKey(keep)));
        for (const key of others) {
            const id = key.slice(prefix.length);
            const kept = signedOut(load(id));
            if (kept.queue.length) write(id, kept);
            else localStorage.removeItem(key);
        }
    } catch {
        // no storage: nothing kept anyway
    }
}

/**
 * The person chose to leave their changes not sent yet (signing out on a shared device): they go from this browser,
 * and nothing of them is kept for later.
 */
export function forgetUnsent(): void {
    if (account === null) return;
    clearTimeout(sendTimer);
    state = { ...state, queue: [], sent: [] };
    write(account, state);
    emit();
}

/** Signing out here: the person's state keeps only what was not sent. */
export function signOutLocal(): void {
    attach(null);
}

const what: Record<Change['type'], SignInWhat> = { mark: 'mark', key: 'key', note: 'note', opened: 'change' };

/** Makes a change: seen at once, sent soon. Returns its id (for undo), or null when a guest was offered signing in. */
export function enqueue(change: Change): string | null {
    if (account === null) {
        if (change.type !== 'opened') {
            rememberPending('local.change', change);
            offerSignIn(what[change.type]);
        }
        return null;
    }

    const queued: Queued = { id: crypto.randomUUID(), change: { ...change, at: change.at ?? new Date().toISOString() } };
    update(current => ({ ...current, queue: [...current.queue, queued] }));
    scheduleSend(sendDelay);
    return queued.id;
}

registerPending('local.change', async (change: Change) => {
    const id = enqueue(change);
    if (id !== null) toastFor(change, id);
});

/** "Undo" of a change made here, whatever became of it. */
export async function undoChange(id: string): Promise<void> {
    if (inFlight.has(id)) {
        undoWanted.add(id);
        return;
    }
    if (state.queue.some(x => x.id === id)) {
        update(current => ({ ...current, queue: current.queue.filter(x => x.id !== id) }));
        toast(i18n.t('local.undone'));
        return;
    }
    const token = undoTokens.get(id);
    if (token) await undoByToken(id, token);
}

async function undoByToken(id: string, token: string) {
    undoTokens.delete(id);
    try {
        await call(api.POST('/api/sync/undo', { body: { token } }));
        toast(i18n.t('local.undone'));
    } catch (error) {
        toast(errorText(error));
    }
    await exchange();
}

/** The toast after a change, with its undo. */
export function toastFor(change: Change, id: string): void {
    const text = change.type === 'mark' ? (change.on ? 'local.markOn' : 'local.markOff')
        : change.type === 'key' ? (change.semitones === null || change.semitones === undefined ? 'local.keyRemoved' : 'local.keySet')
            : change.type === 'note' ? (change.text ? 'local.noteSaved' : 'local.noteRemoved')
                : null;
    if (text) toast(i18n.t(text), { undo: () => void undoChange(id) });
}

function scheduleSend(delay: number) {
    clearTimeout(sendTimer);
    sendTimer = setTimeout(() => void exchange(), delay);
}

let running: Promise<void> | null = null;

/**
 * Sends what is queued (or nothing) and takes what changed since the last answer. One exchange at a time; another
 * asked meanwhile runs after it.
 */
export function exchange(): Promise<void> {
    if (running) return running.then(() => exchange());
    running = doExchange().finally(() => {
        running = null;
    });
    return running;
}

async function doExchange(): Promise<void> {
    if (account === null) return;
    const forAccount = account;
    busy = true;
    emit();
    let sent: Queued[] = [];
    try {
        state = mergeStates(state, load(forAccount));
        sent = state.queue.slice(0, maxBatch);
        sent.forEach(x => inFlight.add(x.id));
        const since = state.base.watermark;
        const response = await call(api.POST('/api/sync', { body: { accountId: forAccount, since, changes: sent.map(x => x.change) } }));
        if (account !== forAccount) return;
        const outcomes = settleAndKeep(sent, response, since);
        failures = 0;
        lastPull = Date.now();

        let changedMine = false;
        for (const { id, result } of outcomes) {
            if (result.applied) changedMine = true;
            else if (result.code !== 'stale') toast(errorText(new ApiError(0, result.code ?? 'internal_error')));
            if (result.undo) {
                if (undoWanted.delete(id)) void undoByToken(id, result.undo);
                else rememberUndo(id, result.undo);
            }
        }

        // what others changed in my playlists, and lists that show my marks, are read anew
        response.playlists.forEach(id => void queryClient.invalidateQueries({ queryKey: ['playlist', id] }));
        if (changedMine || response.marks.length || response.keys.length || response.notes.length || response.playlists.length)
            void queryClient.invalidateQueries({ queryKey: ['songs'] });
        if (response.recent.length) void queryClient.invalidateQueries({ queryKey: ['recent'] });

        if (state.queue.length) scheduleSend(0);
    } catch (error) {
        if (error instanceof ApiError && error.isSignInRequired) return; // the session ended: the queue waits for the person
        if (error instanceof ApiError && error.code === 'wrong_account') {
            // the browser is another person's by now (signed out and in elsewhere): nothing went as them; follow it
            await followSession();
            return;
        }
        failures++;
        scheduleSend(retryDelays[Math.min(failures, retryDelays.length) - 1]!);
    } finally {
        sent.forEach(x => inFlight.delete(x.id));
        busy = false;
        emit();
    }
}

/** Who the browser is now, from the server: the store follows it. */
async function followSession(): Promise<void> {
    try {
        const session = await call(api.GET('/api/auth/session'));
        queryClient.setQueryData(['session'], session);
        const before = account;
        attach(session.accountId ?? null);
        // the person is told: this tab is someone else's now
        if (before !== null && account !== before) toast(i18n.t(account === null ? 'local.signedOutElsewhere' : 'local.switchedElsewhere'));
    } catch {
        // asked again at the next exchange
    }
}

function settleAndKeep(sent: Queued[], response: Parameters<typeof settle>[2], since: number | null) {
    let outcomes: ReturnType<typeof settle>['outcomes'] = [];
    update(current => {
        const settled = settle(current, sent, response, since);
        outcomes = settled.outcomes;
        return settled.state;
    });
    return outcomes;
}

function rememberUndo(id: string, token: string) {
    undoTokens.set(id, token);
    // the toast lives a few seconds; the token is not needed after it
    setTimeout(() => undoTokens.delete(id), 10_000);
}

/** Back online, the page shown again after a while, or another tab changed the state: exchange, or take it in. */
export function installTriggers(): void {
    addEventListener('online', () => void exchange());
    document.addEventListener('visibilitychange', () => {
        if (document.visibilityState !== 'visible') return;
        // who the browser is may have changed in another tab
        void queryClient.invalidateQueries({ queryKey: ['session'] });
        if (Date.now() - lastPull > 60_000) void exchange();
    });
    addEventListener('storage', event => {
        if (account === null || event.key !== storageKey(account)) return;
        state = mergeStates(state, load(account));
        emit();
    });
}
