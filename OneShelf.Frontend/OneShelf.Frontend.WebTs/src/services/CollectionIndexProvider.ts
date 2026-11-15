// The original: Services/CollectionIndexProvider.cs.
//
// The library (Collection) is kept in localStorage "CollectionV4"; the index is rebuilt from it with the pending
// instant actions applied, and synced with the server (Apply) in the background.
// Peek() gives the C# tuple (CollectionIndex? collectionIndex, bool failed) as { collectionIndex, failed }.
//
// Kept as in the original:
// - Sync, when not authorized, returns with IsSyncing left true (SyncingChanged was raised for true, never for false);
// - Dispose subscribes to LocationChanged again instead of unsubscribing (it is never disposed: a singleton);
// - Console.WriteLine timings go to console.log, as in the original.

import type { ILogger } from '../framework/logging';
import { navigationManager, type LocationChangedEventArgs } from '../framework/navigation';
import { asyncVoid } from '../framework/unhandledExceptions';
import type { ApplyRequest } from '../lib/api/OneShelf.Frontend.Api.Model/V3/Api/ApplyRequest';
import type { Identity } from '../lib/api/OneShelf.Authorization.Api.Model/Identity';
import { Collection } from '../lib/api/OneShelf.Frontend.Api.Model/V3/Databasish/Collection';
import { DateTime } from '../lib/common/dotnet/DateTime';
import { newGuid, type Guid } from '../lib/common/dotnet/Guid';
import { AsyncDelegateEvent, DelegateEvent } from '../dotnet/events';
import { sequenceEqual } from '../dotnet/collections';
import { delay, yieldTask } from '../dotnet/tasks';
import { CollectionIndex } from '../models/CollectionIndex';
import type { LocalStorageService } from '../packages/Blazored.LocalStorage/LocalStorageService';
import { AsyncLock } from '../packages/Nito.AsyncEx/AsyncLock';
import type { Api } from './Api';
import type { IdentityProvider } from './IdentityProvider';
import type { InstantActions } from './InstantActions';

export interface CollectionIndexState {
    readonly collectionIndex: CollectionIndex | null;
    readonly failed: boolean;
}

/** (DateTime.Now - started).TotalMilliseconds */
function totalMilliseconds(from: DateTime, to: DateTime): number {
    return Number(to.ticks - from.ticks) / 10000;
}

/** DateTime.Now - x, in milliseconds. */
function millisecondsSince(from: DateTime): number {
    return totalMilliseconds(from, DateTime.now());
}

export class CollectionIndexProvider {
    private static readonly CollectionKey = 'CollectionV4';
    private static readonly EmptyFrequency = 1500;

    private readonly _logger: ILogger;
    private readonly _identityProvider: IdentityProvider;
    private readonly _localStorageService: LocalStorageService;
    private readonly _api: Api;

    private readonly _syncLock = new AsyncLock();
    private readonly _reapplicationLock = new AsyncLock();

    private _lastActuallyLoaded: DateTime = DateTime.minValue;
    private _collectionIndex: CollectionIndexState = { collectionIndex: null, failed: false };
    private readonly _instantActions: InstantActions;
    private _storedCollection: Collection | null = null;
    private _recentLazy: Guid | null = null;

    private _syncedFirst = false;

    constructor(logger: ILogger, identityProvider: IdentityProvider, localStorageService: LocalStorageService, api: Api, instantActions: InstantActions) {
        this._logger = logger;
        this._identityProvider = identityProvider;
        this._localStorageService = localStorageService;
        this._api = api;
        this._instantActions = instantActions;

        navigationManager.locationChanged(this.onNavigationManagerOnLocationChanged);

        this._instantActions.collectionRelevantUpdate.add(this.instantActionHappened);
        this._instantActions.lazyUpdate.add(this.lazyActionHappened);

        this.mightSync();
    }

    /** `event Func<CollectionIndex?, bool, Task>? CollectionChanged` */
    readonly collectionChanged = new AsyncDelegateEvent<[collectionIndex: CollectionIndex | null, failed: boolean]>();

    /** `event Action SyncingChanged` */
    readonly syncingChanged = new DelegateEvent();

    private _isSyncing = false;

    get isSyncing(): boolean {
        return this._isSyncing;
    }

    peek(): CollectionIndexState {
        return this._collectionIndex;
    }

    /** `async void MightSync()` */
    mightSync(): void {
        asyncVoid(async () => {
            await delay(CollectionIndexProvider.EmptyFrequency);
            if (this._identityProvider.identity !== null && !this._syncedFirst) {
                this.sync();
                this._syncedFirst = true;
            }
        });
    }

    /** `async void LazyActionHappened()` */
    private readonly lazyActionHappened = (): void => {
        asyncVoid(async () => {
            const remember = this._recentLazy = newGuid();
            await delay(CollectionIndexProvider.EmptyFrequency);
            if (remember !== this._recentLazy) return;

            try {
                this.sync();
            } catch (e) {
                this._logger.logError(e, 'Error on keep alive');
            }
        });
    };

    /** `async void InstantActionHappened()` */
    private readonly instantActionHappened = (): void => {
        asyncVoid(async () => {
            // take instant actions
            // reapply them to the collection
            // notify about the collection change - if any changes
            // sync

            const lock = await this._reapplicationLock.lockAsync();
            try {
                if (!this._identityProvider.ensureAuthorized()) {
                    return;
                }

                const identity = this._identityProvider.requiredIdentity;

                const started = DateTime.now();
                this._storedCollection ??= await this._localStorageService.getItemAsync(CollectionIndexProvider.CollectionKey, Collection.json);
                console.log(`InstantActionHappened (${millisecondsSince(started)} ms)`);
                if (this._storedCollection !== null) {
                    const peek = await this._instantActions.peek();
                    const collectionIndexFromStorage = new CollectionIndex(this._storedCollection, identity.id, peek);
                    this._collectionIndex = { collectionIndex: collectionIndexFromStorage, failed: false };
                    await this.onCollectionChanged(collectionIndexFromStorage, false);
                }
            } finally {
                lock.dispose();
            }

            this.sync();
        });
    };

    /** `async void Sync(bool force = false)` */
    sync(force = false): void {
        asyncVoid(() => this.syncCore(force));
    }

    private async syncCore(force: boolean): Promise<void> {
        await yieldTask();
        let syncStarted = DateTime.now();
        try {
            const syncLock = await this._syncLock.lockAsync();
            try {
                this._isSyncing = true;
                this.onSyncingChanged();
                let actions: ApplyRequest;
                let identity: Identity;
                const reapplicationLock = await this._reapplicationLock.lockAsync();
                try {
                    if (!this._identityProvider.ensureAuthorized()) {
                        if (this._collectionIndex.collectionIndex === null && this._collectionIndex.failed) return;

                        this._collectionIndex = { collectionIndex: null, failed: true };
                        await this.onCollectionChanged(null, true);
                        return;
                    }

                    identity = this._identityProvider.requiredIdentity;

                    // if failed, switch to loading and notify (if present, do nothing)
                    if (this._collectionIndex.collectionIndex === null) {
                        const started = DateTime.now();
                        this._storedCollection ??= await this._localStorageService.getItemAsync(CollectionIndexProvider.CollectionKey, Collection.json);
                        console.log(`Sync.1 (${millisecondsSince(started)} ms)`);
                        const peek = await this._instantActions.peek();
                        if (this._storedCollection !== null) {
                            const collectionIndexFromStorage = new CollectionIndex(this._storedCollection, identity.id, peek);
                            this._collectionIndex = { collectionIndex: collectionIndexFromStorage, failed: false };
                            await this.onCollectionChanged(collectionIndexFromStorage, false);
                        } else { // currently is failed. cannot be loading since we're inside the lock
                            this._collectionIndex = { collectionIndex: null, failed: false }; // make non-failed
                            await this.onCollectionChanged(null, false);
                        }
                    }

                    actions = await this._instantActions.peek();
                } finally {
                    reapplicationLock.dispose();
                }

                if (actions.getIds().length === 0
                    && millisecondsSince(this._lastActuallyLoaded) < CollectionIndexProvider.EmptyFrequency
                    && this._collectionIndex.collectionIndex !== null
                    && !force) {
                    this._isSyncing = false;
                    this.onSyncingChanged();
                    return;
                }

                let collection: Collection;
                try {
                    actions.identity = identity;
                    const applying = DateTime.now();
                    collection = await this._api.apply(actions, this._storedCollection);
                    syncStarted = new DateTime(syncStarted.ticks + (DateTime.now().ticks - applying.ticks), syncStarted.kind);
                    this._lastActuallyLoaded = DateTime.now();
                    this._isSyncing = false;
                    this.onSyncingChanged();
                } catch (e) {
                    this._isSyncing = false;
                    this.onSyncingChanged();
                    const errorLock = await this._reapplicationLock.lockAsync();
                    try {
                        this._logger.logError(e, 'Error applying the pending actions. Hopefully, an internet connection error.');

                        if (this._collectionIndex.collectionIndex === null) {
                            this._collectionIndex = { collectionIndex: null, failed: true };
                            await this.onCollectionChanged(null, true);
                        }

                        return;
                    } finally {
                        errorLock.dispose();
                    }
                }

                const savingLock = await this._reapplicationLock.lockAsync();
                try {
                    await this._instantActions.extractSent(actions);

                    if (this._storedCollection === null
                        || collection !== this._storedCollection && (collection.etag !== this._storedCollection.etag || !sequenceEqual(collection.deepHash(), this._storedCollection.deepHash()))) {
                        const started = DateTime.now();
                        await this._localStorageService.setItemAsync(CollectionIndexProvider.CollectionKey, collection, Collection.json);
                        this._storedCollection = collection;
                        console.log(`Sync.2.2 (${millisecondsSince(started)} ms)`);
                    }

                    const collectionIndex = new CollectionIndex(collection, identity.id, await this._instantActions.peek());

                    if (this._collectionIndex.collectionIndex?.deepEquals(collectionIndex) !== true) {
                        this._logger.logInformation(
                            'Ehh, collection index changed after it\'s back from the server. If a concurrent action happened, it\'s fine, otherwise there\'s a mismatch between the local application logic and the server application logic. Couldn\'t avoid the re-render.');
                        this._collectionIndex = { collectionIndex, failed: false };
                        await this.onCollectionChanged(collectionIndex, false);
                    }
                } finally {
                    savingLock.dispose();
                }
            } finally {
                syncLock.dispose();
            }
        } catch (e) {
            this._logger.logError(e, 'Exception refreshing collection index (asynchronous background task).');
            this._collectionIndex = { collectionIndex: null, failed: true };
            try {
                await this.onCollectionChanged(null, true);
            } catch (e2) {
                this._logger.logError(e2, 'Error notifying about the collection change.');
            }
        } finally {
            console.log(`Sync (${millisecondsSince(syncStarted)} ms)`);
        }
    }

    async clear(withReload = true): Promise<void> {
        const lock = await this._reapplicationLock.lockAsync();
        try {
            await this._localStorageService.removeItemAsync(CollectionIndexProvider.CollectionKey);
            this._storedCollection = null;
            this._collectionIndex = { collectionIndex: null, failed: !withReload };
            await this.onCollectionChanged(null, !withReload);
        } finally {
            lock.dispose();
        }

        if (withReload) {
            this.sync();
        }
    }

    private async onCollectionChanged(collectionIndex: CollectionIndex | null, failed: boolean): Promise<void> {
        const started = DateTime.now();
        await this.collectionChanged.invokeAll(collectionIndex, failed);
        console.log(`OnCollectionChanged (${millisecondsSince(started)} ms)`);
    }

    dispose(): void {
        this._instantActions.collectionRelevantUpdate.remove(this.instantActionHappened);
        this._instantActions.lazyUpdate.remove(this.lazyActionHappened);

        navigationManager.locationChanged(this.onNavigationManagerOnLocationChanged);
    }

    private readonly onNavigationManagerOnLocationChanged = (_e: LocationChangedEventArgs): void => {
        if (this._identityProvider.identity !== null) {
            this.lazyActionHappened();
        }
    };

    protected onSyncingChanged(): void {
        this.syncingChanged.invoke();
    }
}
