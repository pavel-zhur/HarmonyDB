import { describe, expect, it } from 'vitest';
import { ApplyRequest } from '../lib/api/OneShelf.Frontend.Api.Model/V3/Api/ApplyRequest';
import { Collection } from '../lib/api/OneShelf.Frontend.Api.Model/V3/Databasish/Collection';
import { ImportedVersion } from '../lib/api/OneShelf.Frontend.Api.Model/V3/Instant/ImportedVersion';
import { RemovedVersion } from '../lib/api/OneShelf.Frontend.Api.Model/V3/Instant/RemovedVersion';
import { UpdatedLike } from '../lib/api/OneShelf.Frontend.Api.Model/V3/Instant/UpdatedLike';
import { DateTime } from '../lib/common/dotnet/DateTime';
import { Uri } from '../lib/common/dotnet/Uri';
import { deserialize, serialize } from '../lib/common/dotnet/json/JsonSerializer';
import { CollectionIndex } from './CollectionIndex';

const me = 100;
const friend = 200;

// A library as the original keeps it in localStorage "CollectionV4" (its format; the content made up).
const collectionJson = '{"Songs":['
    + '{"Id":1,"Index":1,"Title":"first song","TemplateRating":null,"Artists":[1],"AdditionalKeywords":null,"CreatedByUserId":100,"CreatedOn":"0001-01-01T00:00:00","Versions":['
    + '{"Id":11,"Uri":"https://example.com/a#x","UserId":100,"Source":"First","ExternalId":"ex-11","CreatedOn":"2023-01-02T05:06:00","Comments":{"100":"\\u0432 \\u043A\\u043D\\u0438\\u0436\\u043A\\u0430\\u0445"},"CollectiveId":null,"CollectiveSearchTag":null,"CollectiveType":null},'
    + '{"Id":12,"Uri":"https://example.com/b","UserId":200,"Source":"Second","ExternalId":"ex-12","CreatedOn":"2023-01-03T05:06:00","Comments":{},"CollectiveId":null,"CollectiveSearchTag":null,"CollectiveType":null}],'
    + '"Likes":[{"UserId":100,"Level":3,"CreatedOn":"2023-04-10T22:24:01.4693255","Transpose":null,"VersionId":null,"LikeCategoryId":null},{"UserId":200,"Level":2,"CreatedOn":"2023-04-11T22:24:01","Transpose":1,"VersionId":12,"LikeCategoryId":5}],"Comments":{}},'
    + '{"Id":2,"Index":2,"Title":"second song","TemplateRating":2.5,"Artists":[1,2],"AdditionalKeywords":"extra words","CreatedByUserId":200,"CreatedOn":"2023-02-01T00:00:00","Versions":['
    + '{"Id":21,"Uri":"https://example.com/c","UserId":100,"Source":"Local","ExternalId":null,"CreatedOn":"2023-02-01T00:00:00","Comments":{},"CollectiveId":"0f8fad5b-d9cb-469f-a165-70867728950e","CollectiveSearchTag":7,"CollectiveType":0}],'
    + '"Likes":[],"Comments":{"200":"note"}}],'
    + '"Users":[{"Id":100,"Title":"Me"},{"Id":200,"Title":"Friend"}],'
    + '"Artists":[{"Id":1,"Name":"The Band","Synonyms":["band"]},{"Id":2,"Name":"Other","Synonyms":[]}],'
    + '"LikeCategories":[{"Id":5,"UserId":200,"Name":"shared","CssColor":"any","Order":1,"PrivateWeight":2,"CssIcon":"any","Access":3}],'
    + '"Etag":1285321070}';

const at = (s: string) => DateTime.parseJson(s)!;

describe('CollectionIndex', () => {
    it('reads the stored library and writes it back to the same text', () => {
        expect(serialize(deserialize(collectionJson, Collection.json), Collection.json)).toBe(collectionJson);
    });

    it('indexes the library', () => {
        const collection = deserialize(collectionJson, Collection.json);
        const index = new CollectionIndex(collection, me, new ApplyRequest({ identity: null! }));

        expect([...index.songsById.keys()]).toEqual([1, 2]);
        expect([...index.existingExternalIdsWithVirtual]).toEqual(['ex-11', 'ex-12']);
        expect(index.versionsByExternalId.get('ex-12').map(x => x.song.id)).toEqual([1]);
        expect(index.versionsByExternalId.get('missing')).toEqual([]);
        // System.Uri equality ignores the fragment, the user info and the case of the scheme and host
        expect(index.versionsByUri.get(new Uri('https://example.com/a')).map(x => x.version.id)).toEqual([11]);
        expect(index.versionsByUri.get(new Uri('HTTPS://user@EXAMPLE.com:443/a#y')).map(x => x.version.id)).toEqual([11]);
        expect(index.versionsByUri.get(new Uri('https://example.com/A'))).toEqual([]);
        expect(index.versionsById.get(21)!.song.id).toBe(2);
        expect(index.find('band').map(x => x.id)).toEqual([1, 2]);
        expect(index.find('2').map(x => x.id)).toEqual([2]);
        expect(index.collection.etag).toBe(1285321070);
    });

    it('applies the pending actions: likes (by time), removed versions, imported versions', () => {
        const collection = deserialize(collectionJson, Collection.json);
        const actions = new ApplyRequest({
            identity: null!,
            updatedLikes: [
                new UpdatedLike({ songId: 2, versionId: null, level: 3, transpose: null, likeCategoryId: null, happenedOn: at('2026-01-02T00:00:00') }),
                new UpdatedLike({ songId: 1, versionId: null, level: null, transpose: null, likeCategoryId: null, happenedOn: at('2026-01-03T00:00:00') }),
                new UpdatedLike({ songId: 2, versionId: null, level: 1, transpose: 4, likeCategoryId: null, happenedOn: at('2026-01-01T00:00:00') }),
                // a like in a SharedEditSingle category of another user is replaced
                new UpdatedLike({ songId: 1, versionId: 12, level: 1, transpose: null, likeCategoryId: 5, happenedOn: at('2026-01-04T00:00:00') }),
                new UpdatedLike({ songId: 999, versionId: null, level: 1, transpose: null, likeCategoryId: null, happenedOn: at('2026-01-04T00:00:00') }),
            ],
            removedVersions: [
                new RemovedVersion({ versionId: 21, happenedOn: at('2026-01-05T00:00:00') }),
                new RemovedVersion({ versionId: 12, happenedOn: at('2026-01-05T00:00:00') }), // another user's: kept
            ],
            importedVersions: [
                new ImportedVersion({ externalId: 'ex-new', level: 2, transpose: 0, likeCategoryId: null, happenedOn: at('2026-01-06T00:00:00') }),
            ],
        });

        const index = new CollectionIndex(collection, me, actions);

        const song2 = index.songsById.get(2)!;
        expect(song2.likes.map(x => ({ userId: x.userId, level: x.level, transpose: x.transpose }))).toEqual([{ userId: me, level: 3, transpose: null }]);
        expect(song2.likes[0]!.createdOn.equals(at('2026-01-01T00:00:00'))).toBe(true);
        expect(song2.versions).toEqual([]);
        const song1 = index.songsById.get(1)!;
        expect(song1.likes.map(x => ({ userId: x.userId, level: x.level, likeCategoryId: x.likeCategoryId }))).toEqual([{ userId: me, level: 1, likeCategoryId: 5 }]);
        expect(song1.likes[0]!.createdOn.equals(at('2023-04-11T22:24:01'))).toBe(true);
        expect(index.collection.etag).toBeNull();
        expect(index.versionsById.has(21)).toBe(false);
        expect([...index.existingExternalIdsWithVirtual]).toEqual(['ex-11', 'ex-12', 'ex-new']);
        expect(index.virtualLikeLevels.get('ex-new')).toEqual({ level: 2, likeCategoryId: null });

        // the stored collection itself is not changed
        expect(collection.songs[1]!.versions.length).toBe(1);
        expect(collection.etag).toBe(1285321070);
    });

    it('keeps the original\'s failure when two removed versions belong to one song', () => {
        const collection = deserialize(collectionJson, Collection.json);
        const twoOfMine = deserialize(collectionJson.replace('"UserId":200,"Source":"Second"', '"UserId":100,"Source":"Second"'), Collection.json);
        expect(collection.songs[0]!.versions[1]!.userId).toBe(friend);
        const actions = new ApplyRequest({
            identity: null!,
            removedVersions: [
                new RemovedVersion({ versionId: 11, happenedOn: at('2026-01-05T00:00:00') }),
                new RemovedVersion({ versionId: 12, happenedOn: at('2026-01-05T00:00:00') }),
            ],
        });

        // the second removal looks the original song up in the list it has already been replaced in
        expect(() => new CollectionIndex(twoOfMine, me, actions)).toThrow(/Index was out of range/);
    });

    it('compares indexes as the original does', () => {
        const collection = deserialize(collectionJson, Collection.json);
        const a = new CollectionIndex(collection, me, new ApplyRequest({ identity: null! }));
        const b = new CollectionIndex(deserialize(collectionJson, Collection.json), me, new ApplyRequest({ identity: null! }));
        const c = new CollectionIndex(deserialize(collectionJson.replace('first song', 'renamed song'), Collection.json), me, new ApplyRequest({ identity: null! }));
        expect(a.deepEquals(b)).toBe(true);
        expect(a.deepEquals(c)).toBe(false);
    });
});
