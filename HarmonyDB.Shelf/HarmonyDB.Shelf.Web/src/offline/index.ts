// The library on this device ("Chords offline"): the person's copy of their library — its lists, their playlists, the
// songs and the chords — kept in IndexedDB (offline/db.ts), one copy per account. The one surface the app uses:
//
//   readThrough(keys.chords(id), () => call(…))      // a reading: the server's answer, kept fresh in the copy when
//                                                    // the copy holds it; with no connection, the copy's
//   readList(keys.library, query, () => call(…))     // a page of a list: with no connection, answered from the rows kept
//   attachOffline(accountId)                         // with the local-first store: whose copy this browser has
//   const offline = useOffline()                     // the copy's state, for the page /offline
//   await download(); await download({ all: true }); await removeCopy(); await setKeeping(on)
//
// What the copy holds:
// - the library's rows, my playlists, each playlist's (mine and followed) details and rows (the shelf's own lists);
// - every chord of the library: its list from the server with a watermark (asked again "since" it: only a change
//   sends the list anew), and the chords themselves read 50 at a time (two readings at most at once, a pause after
//   "too many requests"). Only what the device lacks is read, so a taking stopped (no connection, a closed tab) goes on
//   where it stopped. Chords are read anew when opened online, and all of them on "read everything again";
// - the song pages, each as it is opened while the library is kept (a song not kept opens as its chords alone).
// Not kept: recordings, separate instruments, pictures. With no connection, what is not in the copy answers
// `not_downloaded`; a thing the server refuses (gone, not open any more) leaves the copy.
//
// One person, one copy: attaching another account deletes every other copy on the device; signing out and deleting
// the profile delete them all.

import { useSyncExternalStore } from 'react';
import { ApiError, api, call, type Schemas } from '../api/client';
import { Copy, deleteCopies, emptyCopyState, hasCopy, sizeOf, type CopyState } from './db';
import { answerList, deviceRows, joinPages, keys, membership, type KeptList, type ListQuery } from './lists';
import { applyContents, backoff, batchSize, chunks, missing } from './plan';

export { keys } from './lists';

type SongList = Schemas['SongList'];
type SongRow = Schemas['SongRow'];

export interface OfflineState {
    /** the copy is known (the account's copy opened, or none) */
    ready: boolean;
    keeping: boolean;
    downloadedAt: number | null;
    busy: boolean;
    /** what is being taken now: the lists, or the chords (so many of so many) */
    progress: { stage: 'lists' | 'chords'; done: number; all: number } | null;
    /** what the copy holds against what the library holds */
    songs: { kept: number; all: number };
    chords: { kept: number; all: number };
    /** chords whose source did not answer at the last taking (asked again at the next) */
    later: number;
    /** the library's chords not open to the person: not kept */
    closed: number;
    /** a taking that stopped before its end (a closed tab, a lost connection): it goes on from where it stopped */
    unfinished: boolean;
    bytes: number;
    failed: unknown;
}

const initial: OfflineState = {
    ready: false, keeping: false, downloadedAt: null, busy: false, progress: null, songs: { kept: 0, all: 0 }, chords: { kept: 0, all: 0 }, later: 0, closed: 0, unfinished: false, bytes: 0, failed: null,
};

let account: string | null = null;
let copy: Copy | null = null;
let copyState: CopyState = emptyCopyState;
let songs = new Set<string>();
let chords = new Set<string>();
/** every entry of the copy and its size: read once when the copy opens, then followed */
let sizes = new Map<string, number>();
let later = 0;
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

/** Entries into the copy (their sizes followed). */
async function keep(into: Copy, entries: { key: string; value: unknown }[]): Promise<void> {
    if (!entries.length) return;
    await into.put(entries);
    if (into !== copy) return;
    for (const { key, value } of entries) sizes.set(key, sizeOf(value));
    recount();
}

async function drop(into: Copy, gone: string[]): Promise<void> {
    if (!gone.length) return;
    await into.delete(gone);
    if (into !== copy) return;
    for (const key of gone) sizes.delete(key);
    recount();
}

const holds = (key: string) => sizes.has(key);

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

// The rows of a list answered from the copy that have nothing on the device (they open only with a connection).
const rowsNotKept = new WeakSet<SongRow>();

/** A row of a list answered from the copy with nothing of it on the device. */
export function notOnDevice(row: SongRow): boolean {
    return rowsNotKept.has(row);
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

function clear(): void {
    copy?.close();
    copy = null;
    copyState = emptyCopyState;
    songs = new Set();
    chords = new Set();
    sizes = new Map();
    later = 0;
}

async function attachCopy(accountId: string | null): Promise<void> {
    account = accountId;
    clear();
    set(initial);
    if (typeof indexedDB === 'undefined') return;
    await deleteCopies(accountId);
    if (accountId === null || !(await hasCopy(accountId))) {
        set({ ready: true });
        return;
    }
    const opened = await Copy.open(accountId);
    const state = await opened.getState();
    const entries = await opened.sizes();
    if (account !== accountId) {
        opened.close();
        return;
    }
    copy = opened;
    copyState = state;
    songs = new Set(copyState.songs);
    chords = new Set(copyState.chords);
    sizes = new Map(entries.map(x => [x.key, x.bytes]));
    recount();
    set({ ready: true });
}

/** The copy's counts and size, from what is followed in memory. */
function recount(): void {
    let bytes = 0, songsKept = 0, chordsKept = 0;
    for (const [key, size] of sizes) {
        bytes += size;
        if (key.startsWith('song:')) songsKept++;
        else if (key.startsWith('chords:')) chordsKept++;
    }
    set({
        keeping: copyState.enabled,
        downloadedAt: copyState.downloadedAt,
        songs: { kept: songsKept, all: songs.size },
        chords: { kept: chordsKept, all: chords.size - copyState.closed.length },
        later,
        closed: copyState.closed.length,
        unfinished: copyState.enabled && sizes.size > 0 && (copyState.downloadedAt === null || chordsKept + later < chords.size - copyState.closed.length),
        bytes,
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
        void attaching.then(() => (copy && belongs(key) ? keep(copy, [{ key, value }]) : undefined)).catch(() => undefined);
        return value;
    } catch (error) {
        await attaching;
        if (copy && isRefused(error)) await drop(copy, [key]).catch(() => undefined);
        if (!isOffline(error) || !copy || !copyState.enabled) throw error;
        const kept = await copy.get<T>(key);
        if (kept) {
            served(kept.at);
            return kept.value;
        }
        throw new ApiError(0, 'not_downloaded');
    }
}

/**
 * A page of a list: the server's; with no connection, from the rows kept (or `offline_unanswerable`) — a song whose
 * page is not kept opens as its chords kept, a row with nothing kept is marked (notOnDevice).
 */
export async function readList(key: string, query: ListQuery, run: () => Promise<SongList>): Promise<SongList> {
    try {
        return await run();
    } catch (error) {
        await attaching;
        if (copy && isRefused(error)) await drop(copy, [key]).catch(() => undefined);
        if (!isOffline(error) || !copy || !copyState.enabled) throw error;
        const kept = await copy.get<KeptList>(key);
        if (!kept) throw new ApiError(0, 'not_downloaded');
        served(kept.at);
        const answer = answerList(kept.value, query);
        if (!answer) throw new ApiError(0, 'offline_unanswerable');
        const device = deviceRows(answer.rows, holds, id => holds(keys.chords(id)));
        device.notKept.forEach(row => rowsNotKept.add(row));
        return { ...answer, rows: device.rows };
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
 * The song pages no longer in the library leave the copy.
 */
async function takeLists(into: Copy): Promise<void> {
    const playlists = await call(api.GET('/api/playlists'));
    // the playlists I am in and those I follow (each with its rows: it opens offline as online)
    const mine = playlists.filter(p => !!p.myRole || p.following);
    const all = mine.length + 1;
    let done = 0;
    set({ progress: { stage: 'lists', done, all } });
    const library = await wholeList(offset => call(api.GET('/api/library', { params: { query: { offset, limit: pageSize } } })));
    set({ progress: { stage: 'lists', done: ++done, all } });
    const entries: { key: string; value: unknown }[] = [{ key: keys.myPlaylists, value: playlists }, { key: keys.library, value: library }];
    const lists: KeptList[] = [library];
    for (const p of mine) {
        const details = await call(api.GET('/api/playlists/{id}', { params: { path: { id: p.id } } }));
        const rows = await wholeList(offset => call(api.GET('/api/playlists/{id}/songs', { params: { path: { id: p.id }, query: { sort: 'order', offset, limit: pageSize } } })));
        entries.push({ key: keys.playlist(p.id), value: details }, { key: keys.playlistRows(p.id), value: rows });
        lists.push(rows);
        set({ progress: { stage: 'lists', done: ++done, all } });
    }
    if (into !== copy) return;
    const held = membership(lists.map(x => x.rows));
    await drop(into, [...sizes.keys()].filter(key => (key.startsWith('list:') || key.startsWith('playlist:')) && !entries.some(e => e.key === key)));
    await keep(into, entries);
    await drop(into, [...sizes.keys()].filter(key => key.startsWith('song:') && !held.songs.has(key)));
    songs = held.songs;
    copyState = { ...copyState, songs: [...songs], playlists: mine.map(p => p.id) };
    await into.setState(copyState);
}

const wait = (ms: number) => new Promise(resolve => setTimeout(resolve, ms));

/** A page's answer into the copy: what is kept, what leaves, the copy's state. */
async function keepPage(into: Copy, result: ReturnType<typeof applyContents>): Promise<void> {
    await keep(into, result.keep);
    await drop(into, result.drop);
    await into.setState(copyState);
}

/** The device has no room for more (the browser's storage is full). */
const isFull = (error: unknown) => error instanceof DOMException && error.name === 'QuotaExceededError';

/** One page of chords from the server; after "too many requests", asked again after a growing pause. */
async function readPage(ids: string[]): Promise<Schemas['OfflineContents']> {
    for (let attempt = 0; ; attempt++) {
        try {
            return await call(api.POST('/api/library/offline/contents', { body: { chords: ids } }));
        } catch (error) {
            if (!(error instanceof ApiError && error.status === 429)) throw error;
            await wait(backoff(attempt));
        }
    }
}

/**
 * Takes the library's chords into the copy: the list of them (anew only when the library changed since the watermark;
 * `all` reads every chord again), then what the device lacks, a page at a time, two at most at once. Each page is kept
 * as it comes: a taking stopped goes on where it stopped.
 */
async function takeChords(into: Copy, all: boolean): Promise<void> {
    const list = await call(api.GET('/api/library/offline/chords', { params: { query: { since: all ? undefined : copyState.watermark ?? undefined } } }));
    if (into !== copy) return;
    const changed = list.changed || copyState.watermark === null;
    if (changed) {
        chords = new Set(list.chords);
        await drop(into, [...sizes.keys()].filter(key => key.startsWith('chords:') && !chords.has(key.slice('chords:'.length))));
    }
    // what was not open is asked again when the library changed, or when everything is read again
    copyState = { ...copyState, watermark: list.watermark, chords: [...chords], closed: changed || all ? [] : copyState.closed };
    await into.setState(copyState);

    const library = [...chords];
    const todo = all ? library : missing(library, id => holds(keys.chords(id)), new Set(copyState.closed));
    const pages = chunks(todo, batchSize);
    const closed = new Set(copyState.closed);
    const progress = () => set({ progress: { stage: 'chords', done: library.filter(id => holds(keys.chords(id))).length, all: library.length - closed.size } });
    progress();
    later = 0;
    let next = 0;
    // a failure stops the other reading too: nothing more is asked or kept after it
    let stopped = false;
    const worker = async () => {
        while (!stopped && next < pages.length && into === copy) {
            const page = pages[next++]!;
            let answer: Schemas['OfflineContents'];
            try {
                answer = await readPage(page);
            } catch (error) {
                stopped = true;
                throw error;
            }
            if (into !== copy || stopped) return;
            const result = applyContents(answer.items);
            later += result.later.length;
            result.closed.forEach(id => closed.add(id));
            result.gone.forEach(id => chords.delete(id));
            copyState = { ...copyState, closed: [...closed], chords: [...chords] };
            try {
                await keepPage(into, result);
            } catch (error) {
                stopped = true;
                throw error;
            }
            progress();
        }
    };
    await Promise.all([worker(), worker()]);
}

/**
 * Takes the whole library onto the device, or brings it up to date: its lists, then its chords (only what the device
 * lacks; with `all`, every chord read again).
 */
export async function download({ all = false }: { all?: boolean } = {}): Promise<void> {
    if (!copy || state.busy) return;
    const into = copy;
    set({ busy: true, failed: null, progress: null });
    try {
        await takeLists(into);
        await takeChords(into, all);
        if (into !== copy) return;
        copyState = { ...copyState, downloadedAt: Date.now() };
        await into.setState(copyState);
        recount();
    } catch (error) {
        // the device without room: said so plainly; what was taken stays
        set({ failed: isFull(error) ? new ApiError(0, 'device_full') : error });
        // the connection lost: the taking goes on by itself when it is back, from where it stopped
        resumeWhenOnline = isOffline(error);
    } finally {
        set({ busy: false, progress: null });
    }
}

let resumeWhenOnline = false;
if (typeof addEventListener === 'function') {
    addEventListener('online', () => {
        if (!resumeWhenOnline) return;
        resumeWhenOnline = false;
        void download();
    });
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
    recount();
    await download();
}

/** The copy deleted from this device. */
export async function removeCopy(): Promise<void> {
    clear();
    await deleteCopies(null);
    set({ ...initial, ready: true });
}

/** Whether the browser has a connection (as the browser knows it). */
export function useOnline(): boolean {
    return useSyncExternalStore(listener => {
        addEventListener('online', listener);
        addEventListener('offline', listener);
        return () => {
            removeEventListener('online', listener);
            removeEventListener('offline', listener);
        };
    }, () => navigator.onLine);
}

/**
 * A song of a playlist kept as its chords only (its page was never opened): the chords of it the copy holds, by the
 * rows kept of its playlist or of the library; null when none.
 */
export async function songChordsOnDevice(playlistId: string, songId: string): Promise<string | null> {
    await attaching;
    if (!copy) return null;
    for (const key of [keys.playlistRows(playlistId), keys.library]) {
        const kept = await copy.get<KeptList>(key);
        const row = kept?.value.rows.find(x => x.songId === songId && x.playlistId === playlistId);
        const id = row && [row.chords?.id, ...row.chordsIds].find((x): x is string => !!x && holds(keys.chords(x)));
        if (id) return id;
    }
    return null;
}
