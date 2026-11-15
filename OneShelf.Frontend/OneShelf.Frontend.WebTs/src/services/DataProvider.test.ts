import 'fake-indexeddb/auto';
import { IDBFactory } from 'fake-indexeddb';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import type { ILogger } from '../framework/logging';
import { ChordDataParser } from '../lib/analysis/Services/ChordDataParser';
import { Chords } from '../lib/api/HarmonyDB.Source.Api.Model/V1/Chords';
import { deserialize } from '../lib/common/dotnet/json/JsonSerializer';
import { RepresentationSettings } from '../lib/representations/RepresentationSettings';
import { IndexedItemType } from '../indexedDb/IndexedItemType';
import type { Api } from './Api';
import { DataProvider } from './DataProvider';
import { MyIndexedDb } from './MyIndexedDb';

const logger: ILogger = { log() {}, logTrace() {}, logDebug() {}, logInformation() {}, logWarning() {}, logError() {}, logCritical() {} };

// Chords as the original stores them (the shape and escaping of its IndexedDB items; the content made up).
const chordsJson = '{"Output":{"Name":"pre","Classes":null,"Children":[{"NodeText":{"Text":"Intro "}},{"NodeChord":{"Children":[{"NodeText":{"Text":"C"}}]}},{"NodeText":{"Text":" "}},{"NodeChord":{"Children":[{"NodeText":{"Text":"Gm"}}]}},{"NodeText":{"Text":" "}},{"NodeChord":{"Children":[{"NodeText":{"Text":"Eb"}}]}},{"NodeText":{"Text":" "}},{"NodeChord":{"Children":[{"NodeText":{"Text":"Bb\\u002B"}}]}},{"NodeText":{"Text":" "}},{"NodeChord":{"Children":[{"NodeText":{"Text":"Gm"}}]}},{"NodeText":{"Text":"\\nla la \\u0027la\\u0027 \\u0432\\u0441\\u0451\\n"}}]},"BestTonality":null,"IsStable":true,"IsPublic":false,"UnstableErrorMessage":null,"SourceUri":"https://example.com/song/1/","Type":null,"SpecificAttributes":{"ShortSourceName":"FS","SourceColor":"Warning"},"ExternalId":"ex-TEST00000001","Source":"First","Rating":null,"Title":"Song","Artists":["Artist"]}';

// The progression the original builds and stores for these chords (its item format, the entries as it writes them).
const progressionJson = '{"Sequence":[0,1,2,3,1],"Distinct":[{"OriginalRepresentation":"C","HarmonyRepresentation":"C","HarmonyData":{"Root":3,"Alteration":null,"ChordType":0},"ChordData":"[!3!]"},{"OriginalRepresentation":"Gm","HarmonyRepresentation":"Gm","HarmonyData":{"Root":10,"Alteration":null,"ChordType":1},"ChordData":"[!10!]m"},{"OriginalRepresentation":"Eb","HarmonyRepresentation":"Eb","HarmonyData":{"Root":6,"Alteration":0,"ChordType":0},"ChordData":"[!6b!]"},{"OriginalRepresentation":"Bb\\u002B","HarmonyRepresentation":"Bb?","HarmonyData":{"Root":1,"Alteration":0,"ChordType":2},"ChordData":"[!1b!]\\u002B"}]}';

function openRaw(): Promise<IDBDatabase> {
    return new Promise((resolve, reject) => {
        const request = indexedDB.open('OneShelfDatabase');
        request.onsuccess = () => resolve(request.result);
        request.onerror = () => reject(request.error);
    });
}

function rawAll(db: IDBDatabase, store: string): Promise<unknown[]> {
    return new Promise(resolve => {
        const request = db.transaction(store, 'readonly').objectStore(store).getAll();
        request.onsuccess = () => resolve(request.result);
    });
}

describe('MyIndexedDb and DataProvider', () => {
    beforeEach(() => {
        globalThis.indexedDB = new IDBFactory();
    });

    it('creates the original\'s database: OneShelfDatabase v11, a store per type keyed by "key"', async () => {
        await new MyIndexedDb(logger).getKeys(IndexedItemType.Chords);
        const db = await openRaw();
        expect(db.version).toBe(11);
        expect([...db.objectStoreNames].sort()).toEqual(['IndexedItem-Chords-4', 'IndexedItem-Progression-4', 'IndexedItem-Search-4']);
        for (const name of db.objectStoreNames) {
            const store = db.transaction(name, 'readonly').objectStore(name);
            expect(store.keyPath).toBe('key');
            expect(store.autoIncrement).toBe(false);
            expect([...store.indexNames]).toEqual([]);
        }
        db.close();
    });

    it('drops the stores of an older data version on upgrade', async () => {
        await new Promise<void>(resolve => {
            const request = indexedDB.open('OneShelfDatabase', 10);
            request.onupgradeneeded = () => request.result.createObjectStore('IndexedItem-Chords-3', { keyPath: 'key' });
            request.onsuccess = () => {
                request.result.close();
                resolve();
            };
        });

        const logInformation = vi.fn();
        await new MyIndexedDb({ ...logger, logInformation }).getKeys(IndexedItemType.Chords);
        const db = await openRaw();
        expect([...db.objectStoreNames]).not.toContain('IndexedItem-Chords-3');
        expect(logInformation).toHaveBeenCalledWith('Dropped object store IndexedItem-Chords-3');
        db.close();
    });

    it('saves items as { key, contents }; adding an existing key fails', async () => {
        const myIndexedDb = new MyIndexedDb(logger);
        await myIndexedDb.save(IndexedItemType.Search, 'q', '[]');
        expect(await myIndexedDb.getItem(IndexedItemType.Search, 'q')).toEqual({ key: 'q', contents: '[]' });
        expect(await myIndexedDb.getItem(IndexedItemType.Search, 'other')).toBeNull();
        await expect(myIndexedDb.save(IndexedItemType.Search, 'q', '[]')).rejects.toMatchObject({ name: 'ConstraintError' });

        const db = await openRaw();
        expect((await rawAll(db, 'IndexedItem-Search-4')).map(x => Object.keys(x as object))).toEqual([['key', 'contents']]);
        db.close();
    });

    it('reads chords the original stored, fetches and stores missing ones in its format', async () => {
        const myIndexedDb = new MyIndexedDb(logger);
        await myIndexedDb.save(IndexedItemType.Chords, 'ex-TEST00000001', chordsJson);

        const getChords = vi.fn(async (externalIds: readonly string[]) => new Map([[externalIds[0]!, deserialize(chordsJson, Chords.json)]]));
        const dataProvider = new DataProvider({ getChords } as unknown as Api, logger, myIndexedDb);

        const fromStorage = await dataProvider.getChords('ex-TEST00000001');
        expect(fromStorage.title).toBe('Song');
        expect(getChords).not.toHaveBeenCalled();

        await dataProvider.getChords('ex-TEST00000002');
        expect(getChords).toHaveBeenCalledWith(['ex-TEST00000002']);
        expect((await myIndexedDb.getItem(IndexedItemType.Chords, 'ex-TEST00000002'))!.contents).toBe(chordsJson);
        expect([...await dataProvider.getChordsExternalIdsAvailableInCache()]).toEqual(['ex-TEST00000001', 'ex-TEST00000002']);
    });

    it('stores a progression built from chords as the original does', async () => {
        const myIndexedDb = new MyIndexedDb(logger);
        const dataProvider = new DataProvider({} as Api, logger, myIndexedDb);
        const parser = new ChordDataParser(logger);
        const chords = deserialize(chordsJson, Chords.json);
        const progression = chords.output.asChords(new RepresentationSettings()).map(x => parser.getProgressionData(x));

        await dataProvider.saveChordsProgression('ex-TEST00000001', progression);

        const items = await dataProvider.getProgressions();
        expect(items).toEqual([{ key: 'ex-TEST00000001', contents: progressionJson }]);
    });
});
