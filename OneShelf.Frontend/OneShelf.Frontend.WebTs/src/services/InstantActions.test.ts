import { beforeEach, describe, expect, it, vi } from 'vitest';
import type { ILogger } from '../framework/logging';
import { ApplyRequest } from '../lib/api/OneShelf.Frontend.Api.Model/V3/Api/ApplyRequest';
import { Uri } from '../lib/common/dotnet/Uri';
import { LocalStorageService } from '../packages/Blazored.LocalStorage/LocalStorageService';
import { installMemoryLocalStorage, type MemoryStorage } from '../testing/browserStandIns';
import { InstantActions } from './InstantActions';

const logger: ILogger = { log() {}, logTrace() {}, logDebug() {}, logInformation() {}, logWarning() {}, logError() {}, logCritical() {} };

// What the original keeps after a sync took everything (observed in its localStorage).
const emptyOriginal = '{"Identity":null,"VisitedChords":[],"VisitedSearches":[],"ImportedVersions":[],"RemovedVersions":[],"UpdatedLikes":[],"Etag":null}';

describe('InstantActions', () => {
    let storage: MemoryStorage;
    let instantActions: InstantActions;

    beforeEach(() => {
        storage = installMemoryLocalStorage();
        instantActions = new InstantActions(new LocalStorageService(), logger);
    });

    it('stores the pending actions under InstantActionsV1 as the original does', async () => {
        await instantActions.extractSent(new ApplyRequest({ identity: null! }));
        expect(storage.getItem('InstantActionsV1')).toBe(emptyOriginal);
    });

    it('reads the original\'s actions and writes them back unchanged', async () => {
        const original = '{"Identity":null,"VisitedChords":[{"Uri":"https://example.com/a?b=1","SongId":null,"ExternalId":"ex-1","SearchQuery":"\\u0430\\u0431\\u0432","Transpose":2,"Artists":"A \\u0026 B","Title":"T","Source":"First","ActionId":"0f8fad5b-d9cb-469f-a165-70867728950e","HappenedOn":"2026-11-15T08:55:09.172+05:30"}],'
            + '"VisitedSearches":[],"ImportedVersions":[{"ExternalId":"ex-2","Level":3,"Transpose":0,"LikeCategoryId":null,"ActionId":"7c9e6679-7425-40de-944b-e07fc1f90ae7","HappenedOn":"2026-11-15T08:55:10.001+05:30"}],'
            + '"RemovedVersions":[{"VersionId":17,"ActionId":"16fd2706-8baf-433b-82eb-8c7fada847da","HappenedOn":"2026-11-15T08:55:11.5+05:30"}],'
            + '"UpdatedLikes":[{"SongId":5,"VersionId":null,"Level":null,"Transpose":null,"LikeCategoryId":4,"ActionId":"886313e1-3b8a-5372-9b90-0c9aee199e5d","HappenedOn":"2026-11-15T08:55:12+05:30"}],"Etag":null}';
        storage.setItem('InstantActionsV1', original);

        const peek = await instantActions.peek();
        expect(peek.getIds()).toEqual([
            '886313e1-3b8a-5372-9b90-0c9aee199e5d',
            '7c9e6679-7425-40de-944b-e07fc1f90ae7',
            '16fd2706-8baf-433b-82eb-8c7fada847da',
            '0f8fad5b-d9cb-469f-a165-70867728950e',
        ]);

        await instantActions.extractSent(new ApplyRequest({ identity: null! }));
        // The dates are written back as .NET reads them (an offset gives a Local DateTime, written with the local
        // offset), so this compares in UTC+00:00 terms only when the machine is in that zone; the rest is the same text.
        const written = storage.getItem('InstantActionsV1')!;
        expect(written.replace(/"HappenedOn":"[^"]*"/g, '"HappenedOn":""')).toBe(original.replace(/"HappenedOn":"[^"]*"/g, '"HappenedOn":""'));
    });

    it('adds an updated like with the members in the original\'s order and raises CollectionRelevantUpdate', async () => {
        const collectionRelevant = vi.fn();
        const lazy = vi.fn();
        instantActions.collectionRelevantUpdate.add(collectionRelevant);
        instantActions.lazyUpdate.add(lazy);

        await instantActions.updateLike(5, 7, 3, -2, null);

        const written = storage.getItem('InstantActionsV1')!;
        // The original writes a DateTime's offset with a plain "+" (Utf8JsonWriter writes DateTime values unescaped),
        // e.g. "2026-11-15T08:55:09.172+05:30"; this fails while src/lib's jsonDateTime escapes it as "+".
        expect(written).toMatch(/^\{"Identity":null,"VisitedChords":\[\],"VisitedSearches":\[\],"ImportedVersions":\[\],"RemovedVersions":\[\],"UpdatedLikes":\[\{"SongId":5,"VersionId":7,"Level":3,"Transpose":-2,"LikeCategoryId":null,"ActionId":"[0-9a-f-]{36}","HappenedOn":"\d{4}-\d\d-\d\dT\d\d:\d\d:\d\d(\.\d+)?[+-]\d\d:\d\d"\}\],"Etag":null\}$/);
        expect(collectionRelevant).toHaveBeenCalledTimes(1);
        expect(lazy).not.toHaveBeenCalled();
    });

    it('visits: a number is not a search; a visit raises LazyUpdate; sent ones are taken out', async () => {
        const lazy = vi.fn();
        instantActions.lazyUpdate.add(lazy);

        await instantActions.visitedSearch(' 42 ');
        expect(storage.getItem('InstantActionsV1')).toBeNull();
        expect(lazy).not.toHaveBeenCalled();

        await instantActions.visitedSearch('abc');
        // As ChordsView sends it: new Uri(version.Uri.ToString()), stored as its OriginalString.
        await instantActions.visitedChords(new Uri(new Uri('HTTPS://Example.COM:443/x/./%D0%96%20y#f').toString()), 'ex-1', 'abc', null, 12, 'Artist', 'Title', 'Source');
        expect(lazy).toHaveBeenCalledTimes(2);
        expect(storage.getItem('InstantActionsV1')).toContain('"VisitedChords":[{"Uri":"https://example.com/x/\\u0416 y#f",');

        const sent = await instantActions.peek();
        await instantActions.visitedSearch('def');
        await instantActions.extractSent(sent);

        const left = await instantActions.peek();
        expect(left.visitedSearches.map(x => x.query)).toEqual(['def']);
        expect(left.visitedChords).toEqual([]);
    });
});
