// The original: Services/ProgressionsCacheLoader.cs.
//
// The value tuples (ISong song, Version version, ChordsProgression progression) are objects { song, version, progression }.
// The cache's key is CalculateHash over the library's external ids' string hashes (as there, valid within one run).

import type { ILogger } from '../framework/logging';
import type { ChordsProgression } from '../lib/analysis/Models/ChordsProgression';
import type { ChordDataParser } from '../lib/analysis/Services/ChordDataParser';
import type { ProgressionsBuilder } from '../lib/analysis/Services/ProgressionsBuilder';
import type { ISong } from '../lib/api/OneShelf.Frontend.Api.Model/V3/Databasish/Interfaces/ISong';
import type { Version } from '../lib/api/OneShelf.Frontend.Api.Model/V3/Databasish/Version';
import { stringHashCode } from '../lib/common/dotnet/hashCode';
import { defaultJsonSerializerOptions, deserialize } from '../lib/common/dotnet/json/JsonSerializer';
import { calculateHash } from '../lib/common/OneShelf.Common.Songs/Hashing/MyHashExtensions';
import { RepresentationSettings } from '../lib/representations/RepresentationSettings';
import { compareOrdinal } from '../dotnet/collections';
import { delay, throwIfCancellationRequested } from '../dotnet/tasks';
import { CompressedChordsProgressionDataV1Json } from '../indexedDb/CompressedChordsProgressionDataV1Json';
import { AsyncLock } from '../packages/Nito.AsyncEx/AsyncLock';
import { CacheLoaderBase } from './CacheLoaderBase';
import type { CollectionIndexProvider } from './CollectionIndexProvider';
import type { DataProvider } from './DataProvider';

export interface SongVersionProgression {
    readonly song: ISong;
    readonly version: Version;
    readonly progression: ChordsProgression;
}

/** C# int division (operands are non-negative here). */
function div(a: number, b: number): number {
    if (b === 0) {
        const e = new Error('Attempted to divide by zero.');
        e.name = 'DivideByZeroException';
        throw e;
    }

    return Math.trunc(a / b);
}

export class ProgressionsCacheLoader extends CacheLoaderBase {
    private readonly _dataProvider: DataProvider;
    private readonly _collectionIndexProvider: CollectionIndexProvider;
    private readonly _progressionsBuilder: ProgressionsBuilder;
    private readonly _chordDataParser: ChordDataParser;
    private readonly _lock = new AsyncLock();

    private _cache: { result: SongVersionProgression[] | null; hash: number } | null = null;

    constructor(logger: ILogger, dataProvider: DataProvider, collectionIndexProvider: CollectionIndexProvider, progressionsBuilder: ProgressionsBuilder, chordDataParser: ChordDataParser) {
        super(logger);
        this._dataProvider = dataProvider;
        this._collectionIndexProvider = collectionIndexProvider;
        this._progressionsBuilder = progressionsBuilder;
        this._chordDataParser = chordDataParser;
    }

    protected override async work(token: AbortSignal): Promise<void> {
        await this.getCore(false, token);
    }

    /** Get(): only fast — null when too much is missing. */
    async get(): Promise<SongVersionProgression[] | null> {
        return await this.getCore(true, undefined);
    }

    peek(): SongVersionProgression[] | null {
        return this._cache?.result ?? null;
    }

    /** Get(bool onlyFast, CancellationToken cancellationToken) */
    private async getCore(onlyFast: boolean, cancellationToken: AbortSignal | undefined): Promise<SongVersionProgression[] | null> {
        const collectionIndex = this._collectionIndexProvider.peek().collectionIndex;
        if (collectionIndex === null) return null;

        const _ = await this._lock.lockAsync();
        try {
            const externalIds = [...collectionIndex.songsById.values()]
                .flatMap(x => x.versions)
                .filter(x => x.externalId !== null)
                .map(x => x.externalId!);

            const hash = calculateHash([...externalIds].sort(compareOrdinal).map(x => stringHashCode(x)));

            if (this._cache !== null && this._cache.hash === hash) return this._cache.result;
            const dbProgressions = await this._dataProvider.getProgressions();

            throwIfCancellationRequested(cancellationToken);

            const progressionsProgress = new Set(dbProgressions.map(x => x.key));

            const availableChords = await this._dataProvider.getChordsExternalIdsAvailableInCache();

            throwIfCancellationRequested(cancellationToken);

            // availableChords.Intersect(externalIds).Except(dbProgressions.Select(x => x.Key))
            const externalIdsSet = new Set(externalIds);
            const dbKeys = new Set(dbProgressions.map(x => x.key));
            const toGet = [...availableChords].filter(x => externalIdsSet.has(x) && !dbKeys.has(x));

            if ((toGet.length > CacheLoaderBase.MissingProgressionsThreshold || (externalIds.length - availableChords.size) > CacheLoaderBase.MissingChordsThresholdForProgressionsOnlyFastFail) && onlyFast) return null;

            const progressions = new Map<string, ChordsProgression>();
            const chordsFactor = 50; // reading chords will take 50 times longer
            const total = dbProgressions.length + toGet.length * chordsFactor;
            let progress = 0;
            for (let i = 0; i < dbProgressions.length; i++) {
                const item = dbProgressions[i]!;
                throwIfCancellationRequested(cancellationToken);

                const previous = progress;
                progress = div(div(i * 100, total), 10) * 10;
                if (previous !== progress) {
                    await delay(20, cancellationToken);
                    await this.onUpdated(progressionsProgress, progress);
                }

                progressions.set(item.key, this._progressionsBuilder.buildProgression(deserialize(item.contents, CompressedChordsProgressionDataV1Json, defaultJsonSerializerOptions)!.decompress()));
            }

            for (let i = 0; i < toGet.length; i++) {
                const externalId = toGet[i]!;
                throwIfCancellationRequested(cancellationToken);

                const previous = progress;
                progress = div((dbProgressions.length + i * chordsFactor) * 100, total);
                if (previous !== progress) {
                    await delay(20, cancellationToken);
                    await this.onUpdated(progressionsProgress, progress);
                }

                try {
                    const chords = await this._dataProvider.getChords(externalId, false);
                    const progression = chords.output.asChords(new RepresentationSettings()).map(x => this._chordDataParser.getProgressionData(x));

                    await this._dataProvider.saveChordsProgression(externalId, progression);
                    progressionsProgress.add(externalId);

                    progressions.set(externalId, this._progressionsBuilder.buildProgression(progression));
                } catch (e) {
                    this.logger.logError(e, 'Error getting the chords progression for {externalId}.', externalId);
                }
            }

            const result = [...collectionIndex.versionsById.values()]
                .filter(x => x.version.externalId !== null)
                .map(x => ({ x, progression: progressions.get(x.version.externalId!) ?? null }))
                .filter(x => x.progression !== null)
                .map(x => ({ song: x.x.song, version: x.x.version, progression: x.progression! }))
                .sort((a, b) => a.song.index - b.song.index);

            this._cache = { result, hash };
            await this.onFinished();
            return result;
        } finally {
            _.dispose();
        }
    }
}
