// The original: Services/MyIndexedDb.cs (over SpawnDev.BlazorJS 2.5.9's IndexedDB wrappers).
//
// The database "OneShelfDatabase", version 11; one store per IndexedItemType, "IndexedItem-{Type}-4", key path "key",
// items { key, contents }. As in the original:
// - every call opens the database anew (indexedDB.open, keeping the last connection, closing none);
// - on upgrade, the stores of other names are dropped (logged), and every store is created (an existing one with the
//   same name makes the upgrade fail);
// - Save adds (an existing key fails the request: ConstraintError);
// - DeleteAll starts clearing the stores and returns without waiting for it;
// - a failed request gives the browser's DOMException; a failed open gives Error("Open failed"); a blocked open waits.

import type { ILogger } from '../framework/logging';
import { JsonException } from '../lib/common/dotnet/exceptions';
import type { IndexedItem } from '../indexedDb/IndexedItem';
import { IndexedItemType, indexedItemTypes } from '../indexedDb/IndexedItemType';

/** SpawnDev's IDBRequest.WaitAsync: done already → its result or error; otherwise the success or error event. */
function waitAsync<T>(request: IDBRequest<T>): Promise<T> {
    if (request.readyState === 'done') {
        return request.error != null ? Promise.reject(request.error) : Promise.resolve(request.result);
    }

    return new Promise<T>((resolve, reject) => {
        const onError = () => {
            reject(request.error ?? new Error('IDBRequest - unknown error'));
            onComplete();
        };
        const onSuccess = () => {
            resolve(request.result);
            onComplete();
        };
        const onComplete = () => {
            request.removeEventListener('error', onError);
            request.removeEventListener('success', onSuccess);
        };
        request.addEventListener('error', onError);
        request.addEventListener('success', onSuccess);
    });
}

/** The stored object read as IndexedItem (both members required). */
function toIndexedItem(value: unknown): IndexedItem | null {
    if (value == null) return null;
    const item = value as { key?: unknown; contents?: unknown };
    const missing = [item.key === undefined ? 'key' : null, item.contents === undefined ? 'contents' : null].filter(x => x !== null);
    if (missing.length > 0) {
        throw new JsonException(`JSON deserialization for type 'OneShelf.Frontend.Web.IndexedDb.IndexedItem' was missing required properties, including the following: ${missing.join(', ')}`);
    }

    return { key: item.key as string, contents: item.contents as string };
}

export class MyIndexedDb {
    private static readonly DataVersion = 4;

    private readonly logger: ILogger;
    private _idbFactory: IDBFactory | null = null;
    private _idb: IDBDatabase | null = null;

    constructor(logger: ILogger) {
        this.logger = logger;
    }

    async getItem(type: IndexedItemType, key: string): Promise<IndexedItem | null> {
        await this.initialize();
        const storeName = MyIndexedDb.getStoreName(type);
        const transaction = this._idb!.transaction(storeName, 'readonly');
        const store = transaction.objectStore(storeName);
        return toIndexedItem(await waitAsync(store.get(key)));
    }

    async getItems(type: IndexedItemType): Promise<IndexedItem[]> {
        await this.initialize();
        const storeName = MyIndexedDb.getStoreName(type);
        const transaction = this._idb!.transaction(storeName, 'readonly');
        const store = transaction.objectStore(storeName);
        return (await waitAsync(store.getAll())).map(x => toIndexedItem(x)!);
    }

    async getKeys(type: IndexedItemType): Promise<Set<string>> {
        await this.initialize();
        const storeName = MyIndexedDb.getStoreName(type);
        const transaction = this._idb!.transaction(storeName, 'readonly');
        const store = transaction.objectStore(storeName);
        return new Set((await waitAsync(store.getAllKeys())) as string[]);
    }

    async save(type: IndexedItemType, key: string, contents: string): Promise<void> {
        await this.initialize();
        const storeName = MyIndexedDb.getStoreName(type);
        const transaction = this._idb!.transaction(storeName, 'readwrite');
        const store = transaction.objectStore(storeName);
        const item: IndexedItem = {
            key,
            contents,
        };
        await waitAsync(store.add(item));
    }

    async deleteAll(...types: IndexedItemType[]): Promise<void> {
        await this.initialize();
        const names = types.map(MyIndexedDb.getStoreName);
        const transaction = this._idb!.transaction(names, 'readwrite');
        for (const type of names) {
            const store = transaction.objectStore(type);
            store.clear();
        }
    }

    private async initialize(): Promise<void> {
        const dbName = 'OneShelfDatabase';
        this._idbFactory = indexedDB;
        this._idb = await MyIndexedDb.openAsync(this._idbFactory, dbName, 11, evt => {
            const request = evt.target as IDBOpenDBRequest;
            const db = request.result;
            const stores = [...db.objectStoreNames];

            const ours = indexedItemTypes.map(MyIndexedDb.getStoreName);
            for (const name of [...new Set(stores)].filter(x => !ours.includes(x))) {
                if (stores.includes(name)) {
                    db.deleteObjectStore(name);
                    this.logger.logInformation(`Dropped object store ${name}`);
                }
            }

            for (const indexedItemType of indexedItemTypes) {
                db.createObjectStore(MyIndexedDb.getStoreName(indexedItemType), {
                    keyPath: 'key',
                });
            }
        });
    }

    /** SpawnDev's IDBFactory.OpenAsync(dbName, dbVersion, onUpgradeNeeded). */
    private static openAsync(factory: IDBFactory, dbName: string, dbVersion: number, onUpgradeNeeded: (evt: IDBVersionChangeEvent) => void): Promise<IDBDatabase> {
        const openRequest = factory.open(dbName, dbVersion);
        return new Promise<IDBDatabase>((resolve, reject) => {
            const onSuccess = () => onComplete(null, openRequest.result);
            const onError = () => onComplete('Open failed', null);
            openRequest.addEventListener('success', onSuccess);
            openRequest.addEventListener('error', onError);
            openRequest.addEventListener('upgradeneeded', onUpgradeNeeded);
            const onComplete = (error: string | null, result: IDBDatabase | null) => {
                openRequest.removeEventListener('success', onSuccess);
                openRequest.removeEventListener('error', onError);
                openRequest.removeEventListener('upgradeneeded', onUpgradeNeeded);
                if (result === null) {
                    reject(new Error(error ?? 'Open failed'));
                } else {
                    resolve(result);
                }
            };
        });
    }

    static getStoreName(indexedItemType: IndexedItemType): string {
        return `IndexedItem-${IndexedItemType[indexedItemType]}-${MyIndexedDb.DataVersion}`;
    }

    dispose(): void {
        this._idb = null;
        this._idbFactory = null;
    }
}
