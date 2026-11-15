// The original: Services/DataProvider.cs.
//
// Items in IndexedDB are JSON (System.Text.Json, default options): Chords, List<SearchHeader>,
// CompressedChordsProgressionDataV1. What was read or saved is kept deserialized in memory.
//
// Kept as in the original: in Cache, the `forceReload` check (GetChords' reloadUnstable) is in a branch that cannot be
// reached (it is the `else` of "found in IndexedDB" and needs an item), so an unstable item read from IndexedDB is
// never reloaded.
//
// GetChords of an item already in memory gives, in .NET, a completed task, and its caller's `await` goes on at once,
// in the same synchronous run (ChordsView's OnParametersSetAsync then renders the chords, not "loading" first). Here
// getChords gives the item itself in that case, and a promise otherwise; a caller for whom that moment shows awaits
// only a promise.

import type { ILogger } from '../framework/logging';
import type { ChordDataV1 } from '../lib/analysis/Models/V1/ChordDataV1';
import { CompressedChordsProgressionDataV1 } from '../lib/analysis/Models/V1/CompressedChordsProgressionDataV1';
import { Chords } from '../lib/api/HarmonyDB.Source.Api.Model/V1/Chords';
import { SearchHeader } from '../lib/api/HarmonyDB.Source.Api.Model/V1/SearchHeader';
import { InvalidOperationException } from '../lib/common/dotnet/exceptions';
import { defaultJsonSerializerOptions, deserialize, jsonList, serialize, type JsonContract } from '../lib/common/dotnet/json/JsonSerializer';
import { yieldTask } from '../dotnet/tasks';
import { CompressedChordsProgressionDataV1Json } from '../indexedDb/CompressedChordsProgressionDataV1Json';
import type { IndexedItem } from '../indexedDb/IndexedItem';
import { IndexedItemType } from '../indexedDb/IndexedItemType';
import { AsyncLock } from '../packages/Nito.AsyncEx/AsyncLock';
import type { Api } from './Api';
import type { MyIndexedDb } from './MyIndexedDb';

const searchHeadersJson = jsonList(SearchHeader.json);

/** `.Single().Value` of a dictionary. */
function singleValue<TKey, TValue>(dictionary: ReadonlyMap<TKey, TValue>): TValue {
    if (dictionary.size === 0) throw new InvalidOperationException('Sequence contains no elements');
    if (dictionary.size > 1) throw new InvalidOperationException('Sequence contains more than one element');
    return dictionary.values().next().value as TValue;
}

export class DataProvider {
    private readonly _api: Api;
    private readonly _logger: ILogger;
    private readonly _savingLock = new AsyncLock();
    private readonly _myIndexedDb: MyIndexedDb;

    /** ConcurrentDictionary<(IndexedItemType type, string key), object> */
    private readonly _deserialized = new Map<string, unknown>();

    constructor(api: Api, logger: ILogger, myIndexedDb: MyIndexedDb) {
        this._api = api;
        this._logger = logger;
        this._myIndexedDb = myIndexedDb;
    }

    private static deserializedKey(type: IndexedItemType, key: string): string {
        return `${type}|${key}`;
    }

    /** The chords; the item itself when it is in memory (a completed task in .NET). */
    getChords(externalId: string, reloadUnstable = true): Chords | Promise<Chords> {
        return this.cache(
            IndexedItemType.Chords,
            externalId,
            Chords.json,
            async () => singleValue(await this._api.getChords([externalId])),
            c => !c.isStable && reloadUnstable);
    }

    async clearProgressions(): Promise<void> {
        await yieldTask();
        await this._myIndexedDb.deleteAll(IndexedItemType.Progression);
    }

    async getProgressions(): Promise<IndexedItem[]> {
        return await this._myIndexedDb.getItems(IndexedItemType.Progression);
    }

    async saveChordsProgression(externalId: string, progression: ChordDataV1[]): Promise<void> {
        const _ = await this._savingLock.lockAsync();
        try {
            await this._myIndexedDb.save(IndexedItemType.Progression, externalId, serialize(CompressedChordsProgressionDataV1.compress(progression), CompressedChordsProgressionDataV1Json, defaultJsonSerializerOptions));
        } finally {
            _.dispose();
        }
    }

    async saveChords(externalId: string, chords: Chords): Promise<void> {
        this._deserialized.set(DataProvider.deserializedKey(IndexedItemType.Chords, externalId), chords);

        try {
            const _ = await this._savingLock.lockAsync();
            try {
                await this._myIndexedDb.save(IndexedItemType.Chords, externalId, serialize(chords, Chords.json, defaultJsonSerializerOptions));
            } finally {
                _.dispose();
            }
        } catch (e) {
            this._logger.logError(e, 'These chords might have already been added.');
        }
    }

    async search(query: string): Promise<SearchHeader[]> {
        return await this.cache(IndexedItemType.Search, query, searchHeadersJson, () => this._api.search(query));
    }

    searchPeek(query: string): SearchHeader[] | null {
        const key = DataProvider.deserializedKey(IndexedItemType.Search, query);
        return this._deserialized.has(key) ? this._deserialized.get(key) as SearchHeader[] : null;
    }

    /** Cache: the item itself when it is in memory (the completed task), the loading otherwise. */
    private cache<TItem>(type: IndexedItemType, key: string, contract: JsonContract<TItem>, getter: () => Promise<TItem>, forceReload: ((item: TItem) => boolean) | null = null): TItem | Promise<TItem> {
        const deserializedKey = DataProvider.deserializedKey(type, key);
        if (this._deserialized.has(deserializedKey)) {
            return this._deserialized.get(deserializedKey) as TItem;
        }

        return this.load(type, key, contract, getter, forceReload, deserializedKey);
    }

    private async load<TItem>(type: IndexedItemType, key: string, contract: JsonContract<TItem>, getter: () => Promise<TItem>, forceReload: ((item: TItem) => boolean) | null, deserializedKey: string): Promise<TItem> {
        let item: TItem | null = null;

        if (item === null) {
            await yieldTask();
            const indexedItem = await this._myIndexedDb.getItem(type, key);
            if (indexedItem !== null) {
                item = deserialize(indexedItem.contents, contract, defaultJsonSerializerOptions);
            } else if (item !== null && forceReload !== null && forceReload(item)) {
                item = null;
            }
        }

        if (item === null) {
            item = await getter();

            await yieldTask();
            const _ = await this._savingLock.lockAsync();
            try {
                await this._myIndexedDb.save(type, key, serialize(item, contract, defaultJsonSerializerOptions));
            } finally {
                _.dispose();
            }
        }

        this._deserialized.set(deserializedKey, item);

        return item;
    }

    async clear(): Promise<void> {
        this._deserialized.clear();

        await this._myIndexedDb.deleteAll(IndexedItemType.Chords, IndexedItemType.Progression, IndexedItemType.Search);
    }

    async getChordsExternalIdsAvailableInCache(): Promise<Set<string>> {
        await yieldTask();
        return await this._myIndexedDb.getKeys(IndexedItemType.Chords);
    }

    async getProgressionsExternalIdsAvailableInCache(): Promise<Set<string>> {
        await yieldTask();
        return await this._myIndexedDb.getKeys(IndexedItemType.Progression);
    }
}
