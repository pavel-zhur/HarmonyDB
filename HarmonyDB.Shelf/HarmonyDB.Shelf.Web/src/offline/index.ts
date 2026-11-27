// The library on this device: the person's copy of their library — its lists, their playlists, the songs, the chords
// and their sources' labels — kept in IndexedDB (offline/db.ts), one copy per account, always (as the old shelf kept
// its library on the device until the person cleared it). The app lives from it: what the copy holds is answered from
// it at once, with no server; the server is asked only for what the copy lacks, and when the library changed.
//
//   readThrough(keys.chords(id), () => call(…))      // a reading: the copy's when it holds it; else the server's
//                                                    // (kept when it belongs in the copy)
//   readList(keys.library, query, () => call(…))     // a page of a list: from the rows kept, else the server's
//   attachOffline(accountId)                         // with the local-first store: whose copy this browser has
//   syncLibrary()                                    // the library taken anew if it changed (after the person's
//                                                    // own changes: data/actions.ts)
//   const offline = useOffline()                     // the copy's state, for the page /offline
//   await clearCopy()                                // "Clear": the copy deleted and taken anew
//
// What the copy holds:
// - the library's rows, my playlists, each playlist's (mine and followed) details and rows (the shelf's own lists),
//   and the labels of the library's chords (data/labels.ts) — taken anew whenever the library changed (the server's
//   watermark tells it: asked when the app opens, when the person comes back to it, after their own changes);
// - every chord of the library, by the address its entry reads it by: what the device lacks is taken 50 at a time
//   (two readings at most at once, a pause after "too many requests"), and never read again — chords whose address
//   changed (their author edited them) are new, and taken. Chords opened outside the library are kept too. Only
//   chords that opened are kept: "could not be loaded" is asked again;
// - the song pages, each as it is opened (dropped when the library changed, and read again when opened).
// Not kept: the recent list, recordings, separate instruments, pictures. With no connection, what is not in the copy
// answers `not_downloaded`.
//
// One person, one copy: attaching another account deletes every other copy on the device; signing out and deleting
// the profile delete them all.

import { useSyncExternalStore } from 'react';
import { ApiError, api, call, type Schemas } from '../api/client';
import { queryClient } from '../data/queryClient';
import { allLabels, forgetLabels, setLabels, type Label } from '../data/labels';
import { Copy, deleteCopies, emptyCopyState, sizeOf, type CopyState } from './db';
import { answerList, deviceRows, joinPages, keys, membership, type KeptList, type ListQuery } from './lists';
import { applyContents, backoff, batchSize, chunks, toTake } from './plan';

export { keys } from './lists';

type SongList = Schemas['SongList'];
type SongRow = Schemas['SongRow'];

/** The key the library's labels are kept under. */
const labelsKey = 'labels';

export interface OfflineState {
    /** the copy is known (the account's copy opened, or none: a guest) */
    ready: boolean;
    downloadedAt: number | null;
    busy: boolean;
    /** what is being taken now: the lists, or the chords (so many of so many) */
    progress: { stage: 'lists' | 'chords'; done: number; all: number } | null;
    /** what the copy holds against what the library holds */
    songs: { kept: number; all: number };
    chords: { kept: number; all: number };
    /** chords whose source did not answer at the last taking (asked again at the next) */
    later: number;
    /** the library's chords that could not be loaded: not kept */
    closed: number;
    bytes: number;
    failed: unknown;
}

const initial: OfflineState = {
    ready: false, downloadedAt: null, busy: false, progress: null, songs: { kept: 0, all: 0 }, chords: { kept: 0, all: 0 }, later: 0, closed: 0, bytes: 0, failed: null,
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
    // told only with no connection: online, the copy is simply the app's
    if (navigator.onLine || (servedAt !== null && servedAt <= at)) return;
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

/** A row of a list answered from the copy with nothing of it on the device, while there is no connection. */
export function notOnDevice(row: SongRow): boolean {
    return rowsNotKept.has(row);
}

/** Whose copy this browser has: every other account's is deleted; none (signed out) deletes all. */
export function attachOffline(accountId: string | null): Promise<void> {
    if (accountId === account && state.ready) return attaching;
    // readings answered meanwhile wait for the copy of the person now (a song read as the page opens is answered from,
    // or kept in, the right copy)
    attaching = attachCopy(accountId);
    void attaching.finally(firstAttached);
    return attaching;
}

let firstAttached: () => void = () => undefined;
/** Until the session is known (it is, also offline: data/session.ts), readings wait for whose copy this is. */
let attaching: Promise<void> = new Promise(resolve => { firstAttached = resolve; });

function clear(): void {
    copy?.close();
    copy = null;
    copyState = emptyCopyState;
    songs = new Set();
    chords = new Set();
    sizes = new Map();
    later = 0;
    forgetLabels();
}

async function attachCopy(accountId: string | null): Promise<void> {
    account = accountId;
    clear();
    set(initial);
    if (typeof indexedDB === 'undefined') return;
    await deleteCopies(accountId);
    if (accountId === null) {
        set({ ready: true });
        return;
    }

    // a person's copy is always kept: made when there is none
    const opened = await Copy.open(accountId);
    const kept = await opened.getState();
    const entries = await opened.sizes();
    const labels = await opened.get<[string, Label][]>(labelsKey);
    if (account !== accountId) {
        opened.close();
        return;
    }

    copy = opened;
    copyState = kept;
    songs = new Set(copyState.songs);
    chords = new Set(copyState.chords);
    sizes = new Map(entries.map(x => [x.key, x.bytes]));
    if (labels) setLabels(labels.value);
    recount();
    set({ ready: true });
    void download();
}

/** The copy's counts and size, from what is followed in memory. */
function recount(): void {
    let bytes = 0, songsKept = 0, chordsKept = 0;
    for (const [key, size] of sizes) {
        bytes += size;
        if (key.startsWith('song:')) songsKept++;
        else if (key.startsWith('chords:') && chords.has(key.slice('chords:'.length))) chordsKept++;
    }
    set({
        downloadedAt: copyState.downloadedAt,
        songs: { kept: songsKept, all: songs.size },
        chords: { kept: chordsKept, all: chords.size - copyState.closed.length },
        later,
        closed: copyState.closed.length,
        bytes,
    });
}

/** Whether an answer under this key belongs in the copy. */
function belongs(key: string, value: unknown): boolean {
    // chords: whichever opened (a "could not be loaded" is asked again)
    if (key.startsWith('chords:')) return (value as { state?: string }).state === 'open';
    if (key.startsWith('song:')) return songs.has(key);
    if (key.startsWith('playlist:')) return copyState.playlists.includes(key.slice('playlist:'.length));
    return key === keys.myPlaylists;
}

/**
 * A reading through the copy: the copy's answer when it holds one (no server is asked); else the server's, kept when it
 * belongs in the copy; with no connection and nothing kept, `not_downloaded`.
 */
export async function readThrough<T>(key: string, run: () => Promise<T>): Promise<T> {
    await attaching;
    const into = copy;
    if (into && holds(key)) {
        const kept = await into.get<T>(key);
        if (kept) {
            served(kept.at);
            return kept.value;
        }
    }

    try {
        const value = await run();
        if (into && into === copy && belongs(key, value)) await keep(into, [{ key, value }]).catch(() => undefined);
        return value;
    } catch (error) {
        if (into && isRefused(error)) await drop(into, [key]).catch(() => undefined);
        if (isOffline(error) && into) throw new ApiError(0, 'not_downloaded');
        throw error;
    }
}

/**
 * A page of a list: from the rows kept when the copy holds the list (no server is asked); else the server's. A list
 * the rows kept cannot answer (a filter by another person) is the server's.
 */
export async function readList(key: string, query: ListQuery, run: () => Promise<SongList>): Promise<SongList> {
    await attaching;
    const into = copy;
    const kept = into && holds(key) ? await into.get<KeptList>(key) : undefined;
    const answer = kept ? answerList(kept.value, query) : null;
    if (kept && answer) {
        served(kept.at);
        if (navigator.onLine) return answer;
        const device = deviceRows(answer.rows, holds, id => holds(keys.chords(id)));
        device.notKept.forEach(row => rowsNotKept.add(row));
        return { ...answer, rows: device.rows };
    }

    try {
        return await run();
    } catch (error) {
        if (isOffline(error)) throw new ApiError(0, into ? 'offline_unanswerable' : 'list_not_kept');
        throw error;
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
 * Takes the library's lists whole into the copy: the library's rows, my playlists, each playlist's details and rows;
 * then the labels of the library's chords. The song pages leave the copy: they are read again when opened.
 */
async function takeLists(into: Copy, libraryChords: readonly string[]): Promise<void> {
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

    // the labels of the library's chords, as the old shelf's library came with its sources' names
    const ids = [...new Set([...libraryChords, ...lists.flatMap(x => x.rows.flatMap(r => r.chordsIds))])];
    const labels: [string, Label][] = [];
    for (const part of chunks(ids, 1000)) {
        const answer = await call(api.POST('/api/chords/labels', { body: { ids: part } }));
        for (const label of answer.labels) labels.push([label.id, label.source]);
    }

    if (into !== copy) return;
    const held = membership(lists.map(x => x.rows));
    await drop(into, [...sizes.keys()].filter(key => key.startsWith('list:') || key.startsWith('playlist:') || key.startsWith('song:')));
    setLabels(labels);
    await keep(into, [...entries, { key: labelsKey, value: allLabels() }]);
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
 * Takes the library's chords the device lacks into the copy — or holds by another address than the library's now —
 * a page at a time, two at most at once. Each page is kept as it comes: a taking stopped goes on where it stopped.
 */
async function takeChords(into: Copy, addresses: Record<string, string>): Promise<void> {
    const library = [...chords];
    const closed = new Set(copyState.closed);
    const todo = toTake(library, id => holds(keys.chords(id)), copyState.addresses, addresses, closed);
    const current = (id: string) => holds(keys.chords(id)) && (copyState.addresses[id] ?? '') === (addresses[id] ?? '');
    const pages = chunks(todo, batchSize);
    const progress = () => set({ progress: { stage: 'chords', done: library.filter(current).length, all: library.length - closed.size } });
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
            const read = Object.fromEntries(result.keep.map(x => x.key.slice('chords:'.length)).map(id => [id, addresses[id] ?? '']));
            copyState = { ...copyState, closed: [...closed], chords: [...chords], addresses: { ...copyState.addresses, ...read } };
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

let busy: Promise<void> | null = null;
let again = false;
/** waiting for the lists of the taking now (or the next one) to be in the copy */
let listsWaiting: (() => void)[] = [];

function listsTaken(): void {
    const waiting = listsWaiting;
    listsWaiting = [];
    waiting.forEach(resolve => resolve());
}

/**
 * Takes the library onto the device, or brings it up to date: when it changed since the copy's watermark (or was never
 * taken), its lists and labels; then the chords the device lacks. One at a time: asked while one runs, it runs once
 * more after it.
 */
export function download(): Promise<void> {
    if (busy) {
        again = true;
        return busy;
    }

    busy = (async () => {
        do {
            again = false;
            await takeOnce();
        } while (again && copy);
        busy = null;
    })();
    return busy;
}

/**
 * The library taken anew if it changed: the person's own changes (data/actions.ts) are seen at once. Resolves when its
 * lists are in the copy (the chords go on being taken after it).
 */
export function syncLibrary(): Promise<void> {
    if (!copy) return Promise.resolve();
    const taken = new Promise<void>(resolve => listsWaiting.push(resolve));
    void download();
    return taken;
}

async function takeOnce(): Promise<void> {
    if (!copy) return;
    const into = copy;
    set({ busy: true, failed: null, progress: null });
    try {
        const list = await call(api.GET('/api/library/offline/chords', { params: { query: { since: copyState.watermark ?? undefined } } }));
        if (into !== copy) return;
        const changed = list.changed || copyState.watermark === null;
        const addresses = changed ? Object.fromEntries(list.chords.map(x => [x.id, x.address ?? ''])) : { ...copyState.addresses };
        if (changed) {
            chords = new Set(list.chords.map(x => x.id));
            await takeLists(into, [...chords]);
            if (into !== copy) return;
            // what was not loaded is asked again when the library changed
            copyState = { ...copyState, chords: [...chords], closed: [] };
            await into.setState(copyState);
            // the screens read the lists and pages anew from the copy
            await queryClient.invalidateQueries({ predicate: q => ['songs', 'song', 'playlists', 'playlist', 'library'].includes(String(q.queryKey[0])) });
        }

        listsTaken();
        await takeChords(into, addresses);
        if (into !== copy) return;
        copyState = { ...copyState, watermark: list.watermark, downloadedAt: Date.now() };
        await into.setState(copyState);
        recount();
    } catch (error) {
        // the device without room: said so plainly; what was taken stays
        set({ failed: isFull(error) ? new ApiError(0, 'device_full') : error });
        // the connection lost: the taking goes on by itself when it is back, from where it stopped
        resumeWhenOnline = isOffline(error);
    } finally {
        listsTaken();
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
    // the person comes back to the app: the library taken anew if it changed meanwhile
    addEventListener('visibilitychange', () => {
        if (document.visibilityState === 'visible' && navigator.onLine) void syncLibrary();
    });
}

/** "Clear": the copy deleted from this device, and the library taken anew. */
export async function clearCopy(): Promise<void> {
    const accountId = account;
    clear();
    await deleteCopies(null);
    set({ ...initial });
    account = null;
    await attachOffline(accountId);
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

/** The chords as the device keeps them, or null: nothing is asked (a variant's marks, shown when the device holds them). */
export async function keptChords(id: string): Promise<Schemas['ChordsContentResponse'] | null> {
    await attaching;
    if (!copy || !holds(keys.chords(id))) return null;
    return (await copy.get<Schemas['ChordsContentResponse']>(keys.chords(id)))?.value ?? null;
}
