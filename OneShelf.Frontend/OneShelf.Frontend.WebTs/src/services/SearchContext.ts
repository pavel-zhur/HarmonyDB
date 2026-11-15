// The original: Services/SearchContext.cs — the internet search, at most one request every half second.
// The C# tuples: (List<(string artist, string title, SearchHeader header)>? results, bool failed)? →
// { results: { artist, title, header }[] | null, failed } | null.

import type { ILogger } from '../framework/logging';
import { asyncVoid } from '../framework/unhandledExceptions';
import type { SearchHeader } from '../lib/api/HarmonyDB.Source.Api.Model/V1/SearchHeader';
import { DateTime } from '../lib/common/dotnet/DateTime';
import { isNullOrWhiteSpace, toLowerInvariant, trim } from '../lib/common/dotnet/text';
import { toSearchSyntax } from '../lib/common/HarmonyDB.Common.FullTextSearch/FullTextSearchExtensions';
import { AsyncDelegateEvent } from '../dotnet/events';
import { delay, yieldTask } from '../dotnet/tasks';
import type { DataProvider } from './DataProvider';
import type { InstantActions } from './InstantActions';

export interface SearchResult {
    readonly artist: string;
    readonly title: string;
    readonly header: SearchHeader;
}

export interface SearchArrival {
    readonly results: SearchResult[] | null;
    readonly failed: boolean;
}

export class SearchContext {
    private readonly _logger: ILogger;
    private readonly _dataProvider: DataProvider;
    private readonly _instantActions: InstantActions;

    private _lastRun: DateTime | null = null;
    private _lastRequested: string | null = null;

    constructor(logger: ILogger, dataProvider: DataProvider, instantActions: InstantActions) {
        this._logger = logger;
        this._dataProvider = dataProvider;
        this._instantActions = instantActions;
    }

    /** `event Func<(...)?, Task>? Arrived` */
    readonly arrived = new AsyncDelegateEvent<[result: SearchArrival | null]>();

    private _peek: SearchArrival | null = null;

    get peek(): SearchArrival | null {
        return this._peek;
    }

    peekLastRequested(): string | null {
        return this._lastRequested;
    }

    update(query: string | null): void {
        const searchQuery = SearchContext.getSearchQuery(query);
        if (this._lastRequested === searchQuery) return;

        if (searchQuery !== null) {
            const result = this._dataProvider.searchPeek(searchQuery);
            if (result !== null) {
                this._lastRequested = searchQuery;
                this.onArrived({ results: SearchContext.headersToResult(result), failed: false }).catch(() => {
                });
                return;
            }
        } else {
            this._lastRequested = searchQuery;
            this.onArrived(null).catch(() => {
            });
            return;
        }

        this.schedule(query);
    }

    restartFailed(): void {
        this._logger.logInformation('RestartFailed');

        const lastRequested = this._lastRequested;
        if (lastRequested === null) return;

        this._lastRequested = null;
        this._logger.logInformation(`last requested: '${this._lastRequested ?? 'null'}'`);

        this.update(lastRequested);
    }

    /** `async void Schedule(string? query)` */
    private schedule(query: string | null): void {
        asyncVoid(async () => {
            await yieldTask();

            const searchQuery = SearchContext.getSearchQuery(query);
            if (this._lastRequested === searchQuery) return;

            await this.onArrived(null);

            this._lastRequested = searchQuery;
            this._logger.logInformation(`last requested: '${this._lastRequested ?? 'null'}'`);
            if (searchQuery === null) {
                return;
            }

            await this.maybeDelayAndRunNow();

            if (this._lastRequested !== searchQuery) return;

            let headers: SearchHeader[];
            try {
                this._logger.logInformation(`search: ${searchQuery}`);
                await this._instantActions.visitedSearch(searchQuery);
                headers = await this._dataProvider.search(searchQuery);
            } catch (ex) {
                await this.onArrived({ results: null, failed: true });
                this._logger.logError(ex, 'Error on search.');
                return;
            }

            if (this._lastRequested !== searchQuery) return;

            const results = SearchContext.headersToResult(headers);

            await this.onArrived({ results, failed: false });
        });
    }

    private async maybeDelayAndRunNow(): Promise<void> {
        let toWait: number;
        if (this._lastRun === null) {
            this._lastRun = DateTime.now();
            return;
        }

        const now = DateTime.now();
        toWait = 500 - Number(now.ticks - this._lastRun.ticks) / 10000;
        this._lastRun = now;
        if (toWait <= 0) {
            return;
        }

        await delay(toWait);
    }

    private static getSearchQuery(query: string | null): string | null {
        return query === null ? null : trim(query);
    }

    private static headersToResult(headers: SearchHeader[]): SearchResult[] {
        return headers.map(s => {
            const joined = s.artists === null ? null : s.artists.join(', ');
            const artist = joined === null ? null : isNullOrWhiteSpace(joined) ? null : toSearchSyntax(toLowerInvariant(trim(joined)));
            const title = isNullOrWhiteSpace(s.title) ? 'n/a' : toSearchSyntax(toLowerInvariant(trim(s.title!)));
            return {
                artist: artist ?? 'n/a',
                title,
                header: s,
            };
        });
    }

    private async onArrived(result: SearchArrival | null): Promise<void> {
        this._peek = result;
        this._logger.logInformation(`search results arrived, failed = ${result === null ? '' : result.failed ? 'True' : 'False'}`);
        await this.arrived.invokeAll(result);
    }
}
