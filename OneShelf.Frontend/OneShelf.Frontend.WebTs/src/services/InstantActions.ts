// The original: Services/InstantActions.cs.
//
// The actions not yet sent are kept in localStorage "InstantActionsV1" as an ApplyRequest (Identity null).
// VisitedChords/VisitedSearch add a visit unless the last one has the same GetHashCode(): those are records, and every
// new one has a new ActionId and HappenedOn, so the hash is (but for a collision) always different — the visit is
// always added. Here: added unless the last one equals it as a record (all members), which is the same outcome.

import type { ILogger } from '../framework/logging';
import { ApplyRequest } from '../lib/api/OneShelf.Frontend.Api.Model/V3/Api/ApplyRequest';
import { ImportedVersion } from '../lib/api/OneShelf.Frontend.Api.Model/V3/Instant/ImportedVersion';
import { RemovedVersion } from '../lib/api/OneShelf.Frontend.Api.Model/V3/Instant/RemovedVersion';
import { UpdatedLike } from '../lib/api/OneShelf.Frontend.Api.Model/V3/Instant/UpdatedLike';
import { VisitedChords } from '../lib/api/OneShelf.Frontend.Api.Model/V3/Instant/VisitedChords';
import { VisitedSearch } from '../lib/api/OneShelf.Frontend.Api.Model/V3/Instant/VisitedSearch';
import { DateTime } from '../lib/common/dotnet/DateTime';
import { Uri } from '../lib/common/dotnet/Uri';
import type { Identity } from '../lib/api/OneShelf.Authorization.Api.Model/Identity';
import { DelegateEvent } from '../dotnet/events';
import { listRemoveAll } from '../dotnet/collections';
import type { LocalStorageService } from '../packages/Blazored.LocalStorage/LocalStorageService';
import { AsyncLock } from '../packages/Nito.AsyncEx/AsyncLock';

/** int.TryParse(query, out _) with the invariant culture: optional white space, an optional sign, digits, in range. */
function isInt32(s: string): boolean {
    const match = /^[\t\n\v\f\r ]*([+-]?)([0-9]+)[\t\n\v\f\r ]*$/.exec(s);
    if (match === null) return false;
    const value = Number(match[1]! + match[2]!);
    return value >= -2147483648 && value <= 2147483647;
}

function visitedChordsEquals(a: VisitedChords, b: VisitedChords): boolean {
    return a.actionId === b.actionId && a.happenedOn.equals(b.happenedOn)
        && Uri.equals(a.uri, b.uri) && a.songId === b.songId && a.externalId === b.externalId && a.searchQuery === b.searchQuery
        && a.transpose === b.transpose && a.artists === b.artists && a.title === b.title && a.source === b.source;
}

function visitedSearchEquals(a: VisitedSearch, b: VisitedSearch): boolean {
    return a.actionId === b.actionId && a.happenedOn.equals(b.happenedOn) && a.query === b.query;
}

export class InstantActions {
    private static readonly Key = 'InstantActionsV1';

    private readonly _localStorageService: LocalStorageService;
    private readonly _logger: ILogger;
    private readonly _lock = new AsyncLock();

    constructor(localStorageService: LocalStorageService, logger: ILogger) {
        this._localStorageService = localStorageService;
        this._logger = logger;
    }

    readonly collectionRelevantUpdate = new DelegateEvent();
    readonly lazyUpdate = new DelegateEvent();

    async visitedChords(uri: Uri | null, externalId: string | null, searchQuery: string | null, transpose: number | null, songId: number | null,
        artists: string, title: string, source: string | null): Promise<void> {
        const modified = await this.storeModified(r => {
            const newVisit = new VisitedChords({
                uri,
                externalId,
                searchQuery,
                transpose,
                happenedOn: DateTime.now(),
                songId,
                artists,
                title,
                source,
            });

            const last = r.visitedChords[r.visitedChords.length - 1];
            if (last === undefined || !visitedChordsEquals(last, newVisit)) {
                r.visitedChords.push(newVisit);
                return true;
            }

            return false;
        });

        if (modified) this.onLazyUpdate();
    }

    async visitedSearch(query: string): Promise<void> {
        if (isInt32(query)) return;

        const modified = await this.storeModified(r => {
            const newVisit = new VisitedSearch({
                query,
                happenedOn: DateTime.now(),
            });

            const last = r.visitedSearches[r.visitedSearches.length - 1];
            if (last === undefined || !visitedSearchEquals(last, newVisit)) {
                r.visitedSearches.push(newVisit);
                return true;
            }

            return false;
        });

        if (modified) this.onLazyUpdate();
    }

    /** `level`: byte? */
    async updateLike(songId: number, versionId: number | null, level: number | null, transpose: number | null, likeCategoryId: number | null): Promise<void> {
        await this.store(r => {
            r.updatedLikes.push(new UpdatedLike({
                songId,
                versionId,
                happenedOn: DateTime.now(),
                level,
                transpose,
                likeCategoryId,
            }));
        });

        this.onCollectionRelevantUpdate();
    }

    async versionRemove(versionId: number): Promise<void> {
        await this.store(r => {
            r.removedVersions.push(new RemovedVersion({
                happenedOn: DateTime.now(),
                versionId,
            }));
        });

        this.onCollectionRelevantUpdate();
    }

    /** `level`: byte */
    async versionImport(externalId: string, level: number, transpose: number, likeCategoryId: number | null = null): Promise<void> {
        await this.store(r => {
            r.importedVersions.push(new ImportedVersion({
                happenedOn: DateTime.now(),
                level,
                transpose,
                externalId,
                likeCategoryId,
            }));
        });

        this.onCollectionRelevantUpdate();
    }

    async extractSent(actions: ApplyRequest): Promise<void> {
        await this.store(storage => {
            const ids = new Set(actions.getIds());

            listRemoveAll(storage.importedVersions, x => ids.has(x.actionId));
            listRemoveAll(storage.removedVersions, x => ids.has(x.actionId));
            listRemoveAll(storage.updatedLikes, x => ids.has(x.actionId));
            listRemoveAll(storage.visitedSearches, x => ids.has(x.actionId));
            listRemoveAll(storage.visitedChords, x => ids.has(x.actionId));
        });
    }

    async peek(): Promise<ApplyRequest> {
        return (await this._localStorageService.getItemAsync(InstantActions.Key, ApplyRequest.json)) ?? new ApplyRequest({
            identity: null! as Identity,
        });
    }

    /** Store(Action<ApplyRequest>) */
    private async store(modifier: (r: ApplyRequest) => void): Promise<void> {
        await this.storeModified(r => {
            modifier(r);
            return true;
        });
    }

    /** Store(Func<ApplyRequest, bool>): true also when it failed (logged). */
    private async storeModified(modifier: (r: ApplyRequest) => boolean): Promise<boolean> {
        try {
            const asyncLock = await this._lock.lockAsync();
            try {
                const request = await this._localStorageService.getItemAsync(InstantActions.Key, ApplyRequest.json) ?? new ApplyRequest({
                    identity: null! as Identity,
                });

                const modified = modifier(request);
                if (!modified) return false;

                await this._localStorageService.setItemAsync(InstantActions.Key, request, ApplyRequest.json);
            } finally {
                asyncLock.dispose();
            }
        } catch (e) {
            this._logger.logError(e, 'Error on store.');
        }

        return true;
    }

    async clear(): Promise<void> {
        const asyncLock = await this._lock.lockAsync();
        try {
            await this._localStorageService.removeItemAsync(InstantActions.Key);
        } finally {
            asyncLock.dispose();
        }
    }

    protected onCollectionRelevantUpdate(): void {
        this.collectionRelevantUpdate.invoke();
    }

    protected onLazyUpdate(): void {
        this.lazyUpdate.invoke();
    }
}
