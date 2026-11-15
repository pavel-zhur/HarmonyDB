// The original: Services/IllustrationsProvider.cs.
//
// The illustrations are kept in localStorage "IllustrationsProviderV3" (IllustrationsCache). The constructor and every
// Peek() start MaybeInit: 10 seconds later, Get(). Get() refreshes in the background when the data is older than an
// hour; requests are at least 5 seconds apart.

import type { ILogger } from '../framework/logging';
import { asyncVoid } from '../framework/unhandledExceptions';
import type { AllIllustrations } from '../lib/api/OneShelf.Frontend.Api.Model/V3/Illustrations/AllIllustrations';
import { DateTime } from '../lib/common/dotnet/DateTime';
import { delay } from '../dotnet/tasks';
import { IllustrationsCache } from '../models/IllustrationsCache';
import type { LocalStorageService } from '../packages/Blazored.LocalStorage/LocalStorageService';
import { AsyncLock } from '../packages/Nito.AsyncEx/AsyncLock';
import type { Api } from './Api';

const ticksPerMillisecond = 10000n;

export class IllustrationsProvider {
    private static readonly IllustrationsKey = 'IllustrationsProviderV3';
    private static readonly FirstDelay = 10 * 1000;
    private static readonly RefreshDelay = 60 * 60 * 1000;
    private static readonly AttemptDelaySeconds = 5 * 1000;

    private readonly _logger: ILogger;
    private readonly _localStorageService: LocalStorageService;
    private readonly _api: Api;

    private readonly _loading = new AsyncLock();

    private _data: IllustrationsCache | null = null;
    private _localStorageRead = false;
    private _lastRequested: DateTime | null = null;

    constructor(logger: ILogger, localStorageService: LocalStorageService, api: Api) {
        this._logger = logger;
        this._localStorageService = localStorageService;
        this._api = api;

        this.maybeInit();
    }

    peek(): AllIllustrations | null {
        this.maybeInit();
        return this._data?.all ?? null;
    }

    async get(): Promise<AllIllustrations | null> {
        // DateTime.Now - _data?.ReceivedOn > RefreshDelay (false when there is no data)
        if (this._data !== null && DateTime.now().ticks - this._data.receivedOn.ticks > BigInt(IllustrationsProvider.RefreshDelay) * ticksPerMillisecond) {
            this.refresh();
        }

        const data = this._data;
        if (data !== null) {
            return data.all;
        }

        const _ = await this._loading.lockAsync();
        try {
            try {
                if (this._data !== null) {
                    return this._data.all;
                }

                if (!this._localStorageRead) {
                    this._data = await this._localStorageService.getItemAsync(IllustrationsProvider.IllustrationsKey, IllustrationsCache.json);
                    if (this._data !== null) {
                        return this._data?.all ?? null;
                    }

                    this._localStorageRead = true;
                }

                await this.load();
            } catch (e) {
                this._logger.logError(e, 'Error loading illustrations.');
                return null;
            }

            // (Load has set _data meanwhile.)
            return (this._data as IllustrationsCache | null)?.all ?? null;
        } finally {
            _.dispose();
        }
    }

    /** `async void Refresh()` */
    private refresh(): void {
        asyncVoid(async () => {
            const _ = await this._loading.lockAsync();
            try {
                await this.load();
            } finally {
                _.dispose();
            }
        });
    }

    private async load(): Promise<void> {
        // (DateTime.Now - _lastRequested) < AttemptDelaySeconds (false when never requested)
        if (this._lastRequested !== null && DateTime.now().ticks - this._lastRequested.ticks < BigInt(IllustrationsProvider.AttemptDelaySeconds) * ticksPerMillisecond) {
            return;
        }

        try {
            const loaded = await this._api.getIllustrations(this._data?.all.etag ?? null, true);
            this._data =
                loaded.illustrations === null
                    ? this._data?.all == null
                        ? null
                        : new IllustrationsCache({
                            all: this._data.all,
                            receivedOn: DateTime.now(),
                        })
                    : new IllustrationsCache({
                        all: loaded.illustrations,
                        receivedOn: DateTime.now(),
                    });
            await this._localStorageService.setItemAsync(IllustrationsProvider.IllustrationsKey, this._data!, IllustrationsCache.json);
        } catch (e) {
            this._logger.logError(e, 'Error refreshing illustrations.');
        } finally {
            this._lastRequested = DateTime.now();
        }
    }

    /** `async void MaybeInit()` */
    maybeInit(): void {
        asyncVoid(async () => {
            await delay(IllustrationsProvider.FirstDelay);
            try {
                await this.get();
            } catch (e) {
                this._logger.logError(e, 'Error on first init.');
            }
        });
    }

    async clear(): Promise<void> {
        const _ = await this._loading.lockAsync();
        try {
            await this._localStorageService.removeItemAsync(IllustrationsProvider.IllustrationsKey);
            this._localStorageRead = true;
            this._data = null;
        } finally {
            _.dispose();
        }
    }
}
