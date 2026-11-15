import 'fake-indexeddb/auto';
import { IDBFactory } from 'fake-indexeddb';
import { beforeEach, describe, expect, it } from 'vitest';
import type { ILogger } from '../framework/logging';
import { Chords } from '../lib/api/HarmonyDB.Source.Api.Model/V1/Chords';
import { deserialize } from '../lib/common/dotnet/json/JsonSerializer';
import { IndexedItemType } from '../indexedDb/IndexedItemType';
import type { Api } from './Api';
import { ChordsCacheLoader } from './ChordsCacheLoader';
import type { CollectionIndexProvider } from './CollectionIndexProvider';
import { DataProvider } from './DataProvider';
import { MyIndexedDb } from './MyIndexedDb';

const chordsJson = '{"Output":{"Name":"pre","Classes":null,"Children":[]},"BestTonality":null,"IsStable":true,"IsPublic":false,"UnstableErrorMessage":null,"SourceUri":"https://example.com/","Type":null,"SpecificAttributes":{},"ExternalId":"x","Source":"First","Rating":null,"Title":"T","Artists":null}';

function recordingLogger(events: string[]): ILogger {
    return {
        log() {}, logTrace() {}, logDebug() {}, logWarning() {}, logCritical() {},
        logInformation: (message: string | null) => events.push(`info ${message}`),
        logError: (_e: unknown, message: string | null) => events.push(`error ${message}`),
    };
}

/** A library of songs whose versions have these external ids (a stand-in for the provider's index). */
function collectionIndexProvider(externalIds: (string | null)[]): CollectionIndexProvider {
    const songsById = new Map(externalIds.map((externalId, i) => [i, { versions: [{ externalId }] }]));
    return { peek: () => ({ collectionIndex: { songsById }, failed: false }) } as unknown as CollectionIndexProvider;
}

describe('ChordsCacheLoader', () => {
    beforeEach(() => {
        globalThis.indexedDB = new IDBFactory();
    });

    it('loads the missing chords in groups of 30, asking for the next group as soon as one comes, with the progress after each song', async () => {
        const events: string[] = [];
        const ids = Array.from({ length: 33 }, (_, i) => `ex-${String(i).padStart(2, '0')}`);
        const myIndexedDb = new MyIndexedDb(recordingLogger([]));
        await myIndexedDb.save(IndexedItemType.Chords, 'ex-00', chordsJson);
        await myIndexedDb.save(IndexedItemType.Chords, 'ex-01', chordsJson);

        const api = {
            getChords: async (externalIds: readonly string[]) => {
                events.push(`request ${externalIds.length} from ${externalIds[0]}`);
                await new Promise(resolve => setTimeout(resolve, 5));
                return new Map(externalIds.map(x => [x, deserialize(chordsJson, Chords.json)]));
            },
        } as unknown as Api;

        const dataProvider = new DataProvider(api, recordingLogger([]), myIndexedDb);
        // a version without chords and a repeated external id are not counted
        const loader = new ChordsCacheLoader(recordingLogger(events), collectionIndexProvider([...ids, null, 'ex-05']), dataProvider, api);
        loader.progressChanged.add(progress => events.push(`progress ${progress}`));
        const finished = new Promise<void>(resolve => loader.updated.add(async ids => {
            if (ids === null) resolve();
        }));

        loader.start();
        expect(loader.isRunning).toBe(true);
        await finished;

        expect(loader.isRunning).toBe(false);
        expect(events.slice(0, 4)).toEqual([
            'info Chords loading started.',
            'progress 6',
            'request 30 from ex-02',
            'request 1 from ex-32',
        ]);
        const progress = events.filter(x => x.startsWith('progress')).map(x => x.substring(9));
        expect(progress).toEqual(['6', ...Array.from({ length: 31 }, (_, i) => String(Math.trunc((3 + i) * 100 / 33))).filter((x, i, a) => a.indexOf(x) === i && x !== '6'), 'null']);
        expect(events[events.length - 1]).toBe('info Chords loading finished successfully.');
        expect((await dataProvider.getChordsExternalIdsAvailableInCache()).size).toBe(33);
    });

    it('stops: no error, the progress goes away, it can start again', async () => {
        const events: string[] = [];
        const myIndexedDb = new MyIndexedDb(recordingLogger([]));
        const api = {
            getChords: async (externalIds: readonly string[]) => new Map(externalIds.map(x => [x, deserialize(chordsJson, Chords.json)])),
        } as unknown as Api;
        const dataProvider = new DataProvider(api, recordingLogger([]), myIndexedDb);
        const loader = new ChordsCacheLoader(recordingLogger(events), collectionIndexProvider(Array.from({ length: 40 }, (_, i) => `ex-${i}`)), dataProvider, api);
        loader.progressChanged.add(progress => events.push(`progress ${progress}`));

        loader.start();
        await new Promise(resolve => setTimeout(resolve, 150));
        loader.stop();
        expect(loader.isRunning).toBe(false);
        await new Promise(resolve => setTimeout(resolve, 100));

        expect(events.filter(x => x.startsWith('error'))).toEqual([]);
        expect(events.filter(x => x.startsWith('info'))).toEqual(['info Chords loading started.']);
        expect(events[events.length - 1]).toBe('progress null');
        const saved = (await dataProvider.getChordsExternalIdsAvailableInCache()).size;
        expect(saved).toBeGreaterThan(0);
        expect(saved).toBeLessThan(40);
    });
});
