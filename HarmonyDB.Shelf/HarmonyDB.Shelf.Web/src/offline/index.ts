// The library on this device ("Chords offline"): the person's copy of their library — its lists, their playlists, the
// songs and the chords — kept in IndexedDB (offline/db.ts), one copy per account. The one surface the app uses:
//
//   readThrough(keys.chords(id), () => call(…))      // a reading: the server's answer, kept fresh in the copy when
//                                                    // the copy holds it; with no connection, the copy's
//   readList(keys.library, query, () => call(…))     // a page of a list: with no connection, answered from the rows kept
//   attachOffline(accountId)                         // with the local-first store: whose copy this browser has
//   const offline = useOffline()                     // the copy's state, for the page /offline
//   await downloadLists(); await removeCopy(); await setKeeping(on)
//
// What the copy holds:
// - the library's rows, my playlists, each playlist's details and rows — taken whole by downloadLists() (the shelf's
//   own lists: nothing goes to a source of chords);
// - the song pages and the chords of the library, each as it is read while the library is kept (a song or chords
//   opened online stay for offline). The whole library at once comes with the server's batch reading.
// With no connection, what is not in the copy answers `not_downloaded` (it opens once the connection is back).
//
// One person, one copy: attaching another account deletes every other copy on the device; signing out and deleting
// the profile delete them all.

import { useSyncExternalStore } from 'react';
import { ApiError, api, call, type Schemas } from '../api/client';
import { Copy, deleteCopies, emptyCopyState, hasCopy, type CopyState } from './db';
import { answerList, joinPages, keys, membership, type KeptList, type ListQuery } from './lists';

export { keys } from './lists';

type SongList = Schemas['SongList'];

export interface OfflineState {
    /** the copy is known (the account's copy opened, or none) */
    ready: boolean;
    keeping: boolean;
    downloadedAt: number | null;
    busy: boolean;
    /** of the lists being taken: done and all, once known */
    progress: { done: number; all: number } | null;
    /** what the copy holds against what the library holds */
    songs: { kept: number; all: number };
    chords: { kept: number; all: number };
    bytes: number;
    failed: unknown;
}

const initial: OfflineState = { ready: false, keeping: false, downloadedAt: null, busy: false, progress: null, songs: { kept: 0, all: 0 }, chords: { kept: 0, all: 0 }, bytes: 0, failed: null };

let account: string | null = null;
let copy: Copy | null = null;
let copyState: CopyState = emptyCopyState;
let songs = new Set<string>();
let chords = new Set<string>();
let state: OfflineState = initial;
const listeners = new Set<() => void>();

function set(patch: Partial<OfflineState>): void {
    state = { ...state, ...patch };
    listeners.forEach(listener => listener());
}

export function useOffline(): OfflineState {
    return useSyncExternalStore(listener => {
        listeners.add(listener);
        return () => listeners.delete(listener);
    }, () => state);
}

const isOffline = (error: unknown) => error instanceof ApiError && error.code === 'network';
/** The server's no to a thing (gone, or not open to the person any more): it leaves the copy too. */
const isRefused = (error: unknown) => error instanceof ApiError && (error.status === 403 || error.status === 404);

async function forget(key: string): Promise<void> {
    await copy?.delete([key]).catch(() => undefined);
    await recount();
}

// What the screens show from the copy, with no connection: the oldest of it, for "kept on this device, as of …". It is
// started anew at each change of page (useServedFromCopy).
let servedAt: number | null = null;
const servedListeners = new Set<() => void>();

function served(at: number): void {
    if (servedAt !== null && servedAt <= at) return;
    servedAt = at;
    servedListeners.forEach(listener => listener());
}

/** When what the screens show from the copy was kept (the oldest of it), or null: nothing shown from the copy. */
export function useServedFromCopy(): number | null {
    return useSyncExternalStore(listener => {
        servedListeners.add(listener);
        return () => servedListeners.delete(listener);
    }, () => servedAt);
}

/** A new page: what it shows from the copy is told anew. */
export function forgetServed(): void {
    if (servedAt === null) return;
    servedAt = null;
    servedListeners.forEach(listener => listener());
}

/** Whose copy this browser has: every other account's is deleted; none (signed out) deletes all. */
export function attachOffline(accountId: string | null): Promise<void> {
    if (accountId === account && state.ready) return attaching;
    // readings answered meanwhile wait for the copy of the person now (a song read as the page opens is kept, or
    // forgotten, in the right copy)
    attaching = attachCopy(accountId);
    return attaching;
}

let attaching: Promise<void> = Promise.resolve();

async function attachCopy(accountId: string | null): Promise<void> {
    account = accountId;
    copy?.close();
    copy = null;
    copyState = emptyCopyState;
    songs = new Set();
    chords = new Set();
    set(initial);
    if (typeof indexedDB === 'undefined') return;
    await deleteCopies(accountId);
    if (accountId === null || !(await hasCopy(accountId))) {
        set({ ready: true });
        return;
    }
    const opened = await Copy.open(accountId);
    if (account !== accountId) {
        opened.close();
        return;
    }
    copy = opened;
    copyState = await opened.getState();
    songs = new Set(copyState.songs);
    chords = new Set(copyState.chords);
    await recount();
    set({ ready: true });
}

/** The copy's sizes and counts, read again. */
async function recount(): Promise<void> {
    if (!copy) return;
    const sizes = await copy.sizes();
    const kept = (prefix: string) => sizes.filter(x => x.key.startsWith(prefix)).length;
    set({
        keeping: copyState.enabled,
        downloadedAt: copyState.downloadedAt,
        songs: { kept: kept('song:'), all: songs.size },
        chords: { kept: kept('chords:'), all: chords.size },
        bytes: sizes.reduce((sum, x) => sum + x.bytes, 0),
    });
}

/** Whether an answer under this key belongs in the copy now. */
function belongs(key: string): boolean {
    if (!copyState.enabled) return false;
    if (key.startsWith('song:')) return songs.has(key);
    if (key.startsWith('chords:')) return chords.has(key.slice('chords:'.length));
    if (key.startsWith('playlist:')) return copyState.playlists.includes(key.slice('playlist:'.length));
    return key === keys.myPlaylists;
}

/**
 * A reading through the copy: the server's answer (kept in the copy when it belongs there); with no connection, the
 * copy's answer, or `not_downloaded` when the library is kept and this is not in the copy.
 */
export async function readThrough<T>(key: string, run: () => Promise<T>): Promise<T> {
    try {
        const value = await run();
        // kept once the person's copy is open; the screen does not wait for it
        void attaching.then(() => (copy && belongs(key) ? copy.put([{ key, value }]).then(recount) : undefined)).catch(() => undefined);
        return value;
    } catch (error) {
        await attaching;
        if (copy && isRefused(error)) await forget(key);
        if (!isOffline(error) || !copy || !copyState.enabled) throw error;
        const kept = await copy.get<T>(key);
        if (kept) {
            served(kept.at);
            return kept.value;
        }
        throw new ApiError(0, 'not_downloaded');
    }
}

/** A page of a list: the server's; with no connection, from the rows kept (or `offline_unanswerable`). */
export async function readList(key: string, query: ListQuery, run: () => Promise<SongList>): Promise<SongList> {
    try {
        return await run();
    } catch (error) {
        await attaching;
        if (copy && isRefused(error)) await forget(key);
        if (!isOffline(error) || !copy || !copyState.enabled) throw error;
        const kept = await copy.get<KeptList>(key);
        if (!kept) throw new ApiError(0, 'not_downloaded');
        served(kept.at);
        const answer = answerList(kept.value, query);
        if (!answer) throw new ApiError(0, 'offline_unanswerable');
        return answer;
    }
}

const pageSize = 200;

/** A list read to its end, page by page. */
async function wholeList(read: (offset: number) => Promise<SongList>): Promise<KeptList> {
    const pages: SongList[] = [];
    let offset = 0;
    for (;;) {
        const page = await read(offset);
        pages.push(page);
        offset = page.offset + page.rows.length;
        if (!page.rows.length || offset >= page.total) return joinPages(pages);
    }
}

/**
 * Takes the library's lists whole into the copy: the library's rows, my playlists, each playlist's details and rows.
 * The songs and the chords no longer in the library leave the copy.
 */
export async function downloadLists(): Promise<void> {
    if (!copy || state.busy) return;
    const into = copy;
    set({ busy: true, failed: null, progress: null });
    try {
        const playlists = await call(api.GET('/api/playlists'));
        const mine = playlists.filter(p => !!p.myRole);
        const all = mine.length + 2;
        let done = 0;
        const step = () => set({ progress: { done: ++done, all } });
        step();
        const library = await wholeList(offset => call(api.GET('/api/library', { params: { query: { offset, limit: pageSize } } })));
        step();
        const entries: { key: string; value: unknown }[] = [{ key: keys.myPlaylists, value: playlists }, { key: keys.library, value: library }];
        const lists: KeptList[] = [library];
        for (const p of mine) {
            const details = await call(api.GET('/api/playlists/{id}', { params: { path: { id: p.id } } }));
            const rows = await wholeList(offset => call(api.GET('/api/playlists/{id}/songs', { params: { path: { id: p.id }, query: { sort: 'order', offset, limit: pageSize } } })));
            entries.push({ key: keys.playlist(p.id), value: details }, { key: keys.playlistRows(p.id), value: rows });
            lists.push(rows);
            step();
        }
        if (into !== copy) return;
        const held = membership(lists.map(x => x.rows));
        await into.remove('list:');
        await into.remove('playlist:');
        await into.put(entries);
        await removeGone(into, held.songs, held.chords);
        songs = held.songs;
        chords = held.chords;
        copyState = { ...copyState, downloadedAt: Date.now(), songs: [...songs], chords: [...chords], playlists: mine.map(p => p.id) };
        await into.setState(copyState);
        await recount();
    } catch (error) {
        set({ failed: error });
    } finally {
        set({ busy: false, progress: null });
    }
}

/** The songs and chords kept that the library no longer holds leave the copy. */
async function removeGone(into: Copy, songsHeld: Set<string>, chordsHeld: Set<string>): Promise<void> {
    const gone = (await into.sizes()).map(x => x.key).filter(key =>
        (key.startsWith('song:') && !songsHeld.has(key)) || (key.startsWith('chords:') && !chordsHeld.has(key.slice('chords:'.length))));
    await into.delete(gone);
}

/** Keeping the library on this device, or not (then the copy is deleted). */
export async function setKeeping(on: boolean): Promise<void> {
    if (!account) return;
    if (!on) {
        await removeCopy();
        return;
    }
    copy ??= await Copy.open(account);
    copyState = { ...copyState, enabled: true };
    await copy.setState(copyState);
    // the browser is asked to keep the copy when space runs short
    await navigator.storage?.persist?.().catch(() => false);
    await recount();
    await downloadLists();
}

/** The copy deleted from this device. */
export async function removeCopy(): Promise<void> {
    copy?.close();
    copy = null;
    copyState = emptyCopyState;
    songs = new Set();
    chords = new Set();
    await deleteCopies(null);
    set({ ...initial, ready: true });
}
