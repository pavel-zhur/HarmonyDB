// The original: Services/ChordsCacheLoader.cs.
//
// The library's chords not yet in IndexedDB are fetched in groups of 30, the next group's request started as soon as
// the previous group's answer has come; each song is saved, then 20 ms, then the progress (existing * 100 / total);
// 300 ms between groups.

import type { ILogger } from '../framework/logging';
import { asyncVoid } from '../framework/unhandledExceptions';
import { chunk } from '../dotnet/collections';
import { delay } from '../dotnet/tasks';
import type { Chords } from '../lib/api/HarmonyDB.Source.Api.Model/V1/Chords';
import type { Api } from './Api';
import { CacheLoaderBase } from './CacheLoaderBase';
import type { CollectionIndexProvider } from './CollectionIndexProvider';
import type { DataProvider } from './DataProvider';

export class ChordsCacheLoader extends CacheLoaderBase {
    private static readonly FirstDelay = 20 * 1000;

    private readonly _collectionIndexProvider: CollectionIndexProvider;
    private readonly _dataProvider: DataProvider;
    private readonly _api: Api;

    constructor(logger: ILogger, collectionIndexProvider: CollectionIndexProvider, dataProvider: DataProvider, api: Api) {
        super(logger);
        this._collectionIndexProvider = collectionIndexProvider;
        this._dataProvider = dataProvider;
        this._api = api;
    }

    protected override async work(token: AbortSignal): Promise<void> {
        const collectionIndex = this._collectionIndexProvider.peek().collectionIndex;
        if (collectionIndex === null) {
            return;
        }

        const existing = await this._dataProvider.getChordsExternalIdsAvailableInCache();

        const total = [...new Set(
            [...collectionIndex.songsById.values()]
                .flatMap(x => x.versions.map(x => x.externalId))
                .filter((x): x is string => x !== null))];

        const externalIds = total
            .filter(x => !existing.has(x));

        const groups = chunk(externalIds, 30);

        // existing.Count * 100 / total.Count: integer division (a library with no versions divides by zero)
        const updateProgress = () => this.onUpdated(existing, integerDivide(existing.size * 100, total.length));

        await updateProgress();

        let nextTask: Promise<Map<string, Chords>> | null = groups.length > 0 ? unobserved(this._api.getChords(groups[0]!, token)) : null;

        for (let i = 0; i < groups.length; i++) {
            if (token.aborted) break;

            if (nextTask === null) break;
            const got = await nextTask;
            nextTask = i + 1 < groups.length ? unobserved(this._api.getChords(groups[i + 1]!, token)) : null;

            for (const [externalId, chords] of got) {
                if (token.aborted) break;
                await this._dataProvider.saveChords(externalId, chords);
                await delay(20, token);
                existing.add(externalId);
                await updateProgress();
            }

            await delay(300, token);
        }
    }

    /** `async void StartDelayed()` */
    readonly startDelayed = (): void => {
        asyncVoid(async () => {
            await delay(ChordsCacheLoader.FirstDelay);
            this.start();
        });
    };
}

/**
 * A task started ahead and awaited later, if at all: when the loop stops before awaiting it, its failure goes unseen
 * (an unobserved task in .NET), not as an unhandled rejection. Awaiting it still gives its result or failure.
 */
function unobserved<T>(task: Promise<T>): Promise<T> {
    task.catch(() => {
    });
    return task;
}

/** C# int division: DivideByZeroException for a zero divisor. */
function integerDivide(dividend: number, divisor: number): number {
    if (divisor === 0) {
        const e = new Error('Attempted to divide by zero.');
        e.name = 'DivideByZeroException';
        throw e;
    }

    return Math.trunc(dividend / divisor);
}
