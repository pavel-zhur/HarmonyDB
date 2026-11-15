// The original: Services/ProgressionsSearchContext.cs.
// Results is ((foundProgressionsWithCoverage, harmonyGroupsWithIsFirst) result, string title)? → { result, title } | null.

import type { ILogger } from '../framework/logging';
import { asyncVoid } from '../framework/unhandledExceptions';
import type { ChordsProgression } from '../lib/analysis/Models/ChordsProgression';
import type { HarmonyGroup } from '../lib/analysis/Models/HarmonyGroup';
import type { HarmonyMovementsSequence } from '../lib/analysis/Models/HarmonyMovementsSequence';
import type { ProgressionsSearch } from '../lib/analysis/Services/ProgressionsSearch';
import { DelegateEvent } from '../dotnet/events';
import { InvalidOperationException } from '../lib/common/dotnet/exceptions';
import type { ProgressionsSearchSource } from '../models/ProgressionsSearchSource';
import type { ProgressionsCacheLoader } from './ProgressionsCacheLoader';

/** `Enumerable.First()` */
function first<T>(source: readonly T[]): T {
    if (source.length === 0) throw new InvalidOperationException('Sequence contains no elements');
    return source[0]!;
}

export interface ProgressionsSearchResults {
    readonly result: {
        /** float values */
        readonly foundProgressionsWithCoverage: Map<ChordsProgression, number>;
        readonly harmonyGroupsWithIsFirst: Map<HarmonyGroup, boolean>;
    };
    readonly title: string;
}

export class ProgressionsSearchContext {
    private readonly _logger: ILogger;
    private readonly _progressionsCacheLoader: ProgressionsCacheLoader;
    private readonly _progressionsSearch: ProgressionsSearch;

    constructor(logger: ILogger, progressionsCacheLoader: ProgressionsCacheLoader, progressionsSearch: ProgressionsSearch) {
        this._logger = logger;
        this._progressionsCacheLoader = progressionsCacheLoader;
        this._progressionsSearch = progressionsSearch;
    }

    readonly searchResultsChanged = new DelegateEvent<[source: ProgressionsSearchSource]>();

    results: ProgressionsSearchResults | null = null;

    clear(source: ProgressionsSearchSource): void {
        this.results = null;
        this.onSearchChanged(source);
    }

    /** `async void Start(...)` */
    start(source: ProgressionsSearchSource, sequence: HarmonyMovementsSequence): void {
        asyncVoid(async () => {
            this.results = null;

            try {
                const progressions = await this._progressionsCacheLoader.get();
                if (progressions === null) {
                    throw new Error('Could not have happened.');
                }

                const x = this._progressionsSearch.search(progressions.map(x => x.progression), sequence);
                this.results = {
                    result: x,
                    title: [first(sequence.movements).from, ...sequence.movements.map(x => x.to)].map(x => x.harmonyRepresentation).join(' - '),
                };
                this.onSearchChanged(source);
            } catch (e) {
                this._logger.logError(e, 'Search initiation failed.');
                this.results = null;
            }
        });
    }

    private onSearchChanged(source: ProgressionsSearchSource): void {
        this.searchResultsChanged.invoke(source);
    }
}