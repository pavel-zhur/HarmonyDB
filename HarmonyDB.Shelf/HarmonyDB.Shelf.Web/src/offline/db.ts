// The device's copy of a person's library: one IndexedDB database per account ("shelf-offline-<account id>"), with
// one store of entries — each an answer of the API kept under its key (offline/keys.ts) with its size — and one of
// the copy's own state. Nothing here knows what the answers mean: offline/index.ts does.

const prefix = 'shelf-offline-';
const version = 1;

export interface Entry<T = unknown> {
    key: string;
    value: T;
    /** its size as kept, bytes (UTF-8 of its JSON) */
    bytes: number;
    /** when it was kept, ms */
    at: number;
}

export interface CopyState {
    /** the person keeps the library on this device */
    enabled: boolean;
    /** when the library's lists were last taken whole, ms */
    downloadedAt: number | null;
    /** what the library holds, as its lists said then: its songs' keys and its chords' ids (offline/lists.ts) */
    songs: string[];
    chords: string[];
    /** my playlists' ids, as they were then */
    playlists: string[];
}

export const emptyCopyState: CopyState = { enabled: false, downloadedAt: null, songs: [], chords: [], playlists: [] };

const encoder = new TextEncoder();
export const sizeOf = (value: unknown) => encoder.encode(JSON.stringify(value)).length;

const request = <T>(r: IDBRequest<T>) => new Promise<T>((resolve, reject) => {
    r.onsuccess = () => resolve(r.result);
    r.onerror = () => reject(r.error);
});

const done = (tx: IDBTransaction) => new Promise<void>((resolve, reject) => {
    tx.oncomplete = () => resolve();
    tx.onerror = () => reject(tx.error);
    tx.onabort = () => reject(tx.error);
});

/** A person's copy, opened. */
export class Copy {
    private constructor(private readonly db: IDBDatabase) {}

    static async open(accountId: string): Promise<Copy> {
        const open = indexedDB.open(prefix + accountId, version);
        open.onupgradeneeded = () => {
            open.result.createObjectStore('entries', { keyPath: 'key' });
            open.result.createObjectStore('state');
        };
        return new Copy(await request(open));
    }

    close(): void {
        this.db.close();
    }

    async get<T>(key: string): Promise<Entry<T> | undefined> {
        return await request(this.db.transaction('entries').objectStore('entries').get(key)) as Entry<T> | undefined;
    }

    async put(entries: { key: string; value: unknown }[]): Promise<void> {
        const tx = this.db.transaction('entries', 'readwrite');
        const store = tx.objectStore('entries');
        const at = Date.now();
        for (const { key, value } of entries) store.put({ key, value, bytes: sizeOf(value), at } satisfies Entry);
        await done(tx);
    }

    /** Removes these entries. */
    async delete(keys: readonly string[]): Promise<void> {
        const tx = this.db.transaction('entries', 'readwrite');
        const store = tx.objectStore('entries');
        for (const key of keys) store.delete(key);
        await done(tx);
    }

    /** Removes the entries whose keys start so (a kind of them), or all. */
    async remove(keyPrefix = ''): Promise<void> {
        const tx = this.db.transaction('entries', 'readwrite');
        const store = tx.objectStore('entries');
        if (keyPrefix) store.delete(IDBKeyRange.bound(keyPrefix, keyPrefix + '￿'));
        else store.clear();
        await done(tx);
    }

    /** Every entry's key and size (not their values). */
    async sizes(): Promise<{ key: string; bytes: number }[]> {
        const out: { key: string; bytes: number }[] = [];
        const tx = this.db.transaction('entries');
        const cursor = tx.objectStore('entries').openCursor();
        await new Promise<void>((resolve, reject) => {
            cursor.onerror = () => reject(cursor.error);
            cursor.onsuccess = () => {
                const c = cursor.result;
                if (!c) return resolve();
                const entry = c.value as Entry;
                out.push({ key: entry.key, bytes: entry.bytes });
                c.continue();
            };
        });
        return out;
    }

    async getState(): Promise<CopyState> {
        return { ...emptyCopyState, ...(await request(this.db.transaction('state').objectStore('state').get('state')) as CopyState | undefined) };
    }

    async setState(state: CopyState): Promise<void> {
        const tx = this.db.transaction('state', 'readwrite');
        tx.objectStore('state').put(state, 'state');
        await done(tx);
    }
}

/** Whether the account has a copy on this device (without making one). */
export async function hasCopy(accountId: string): Promise<boolean> {
    return (await indexedDB.databases()).some(x => x.name === prefix + accountId);
}

/** Deletes the copies of every account but `keep` (none: all of them). */
export async function deleteCopies(keep: string | null): Promise<void> {
    if (typeof indexedDB === 'undefined') return;
    const names = (await indexedDB.databases()).map(x => x.name).filter((x): x is string => !!x && x.startsWith(prefix));
    await Promise.all(names.filter(name => name !== (keep === null ? null : prefix + keep)).map(name => request(indexedDB.deleteDatabase(name)).catch(() => undefined)));
}
