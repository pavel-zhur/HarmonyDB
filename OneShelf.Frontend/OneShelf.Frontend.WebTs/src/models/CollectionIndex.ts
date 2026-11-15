// The original: Models/CollectionIndex.cs.
//
// The C# value tuples are objects with their element names: (Version version, ISong song) → { version, song },
// (byte level, int? likeCategoryId) → { level, likeCategoryId }. ILookup is Lookup (src/dotnet/collections);
// VersionsByUri, keyed by System.Uri, is an EquatableLookup (Uri's own Equals and GetHashCode).

import { Lookup, getRequired, listRemove, listRemoveAll, listSet, orderNumbers, sequenceEqual, toDictionary } from '../dotnet/collections';
import type { ApplyRequest } from '../lib/api/OneShelf.Frontend.Api.Model/V3/Api/ApplyRequest';
import type { Artist } from '../lib/api/OneShelf.Frontend.Api.Model/V3/Databasish/Artist';
import type { Collection } from '../lib/api/OneShelf.Frontend.Api.Model/V3/Databasish/Collection';
import type { ICollection } from '../lib/api/OneShelf.Frontend.Api.Model/V3/Databasish/Interfaces/ICollection';
import type { ISong } from '../lib/api/OneShelf.Frontend.Api.Model/V3/Databasish/Interfaces/ISong';
import { Like } from '../lib/api/OneShelf.Frontend.Api.Model/V3/Databasish/Like';
import type { LikeCategory } from '../lib/api/OneShelf.Frontend.Api.Model/V3/Databasish/LikeCategory';
import { LikeCategoryAccess } from '../lib/api/OneShelf.Frontend.Api.Model/V3/Databasish/LikeCategoryAccess';
import type { Song } from '../lib/api/OneShelf.Frontend.Api.Model/V3/Databasish/Song';
import type { User } from '../lib/api/OneShelf.Frontend.Api.Model/V3/Databasish/User';
import type { Version } from '../lib/api/OneShelf.Frontend.Api.Model/V3/Databasish/Version';
import { buildCache, find, type FullTextSearchCache } from '../lib/common/OneShelf.Common.Songs/FullTextSearch/FullTextSearchLogic';
import { EquatableLookup } from '../lib/common/dotnet/EquatableLookup';
import type { Uri } from '../lib/common/dotnet/Uri';

export interface VersionAndSong {
    readonly version: Version;
    readonly song: ISong;
}

export interface VirtualLikeLevel {
    /** byte */
    readonly level: number;
    readonly likeCategoryId: number | null;
}

export class CollectionIndex {
    private readonly _userId: number;
    private readonly _cache: FullTextSearchCache<ISong>;

    collection: ICollection;
    usersById: Map<number, User>;
    artistsById: Map<number, Artist>;
    songsById: Map<number, ISong>;
    existingExternalIdsWithVirtual: ReadonlySet<string>;
    versionsByUri: EquatableLookup<Uri, VersionAndSong>;
    versionsByExternalId: Lookup<string, VersionAndSong>;
    versionsById: ReadonlyMap<number, VersionAndSong>;
    virtualLikeLevels: Map<string, VirtualLikeLevel>;
    likeCategoriesById: Map<number, LikeCategory>;

    constructor(immutableCollection: ICollection, userId: number, applyRequest: ApplyRequest) {
        this._userId = userId;

        const collection: Collection = immutableCollection.clone();

        this.artistsById = toDictionary(collection.artists, x => x.id, x => x);
        this.usersById = toDictionary(collection.users, x => x.id, x => x);
        this.songsById = toDictionary(collection.songs, x => x.id, x => x as ISong);
        this.likeCategoriesById = toDictionary(collection.likeCategories, x => x.id, x => x);

        const existingExternalIds = new Set<string>();
        for (const x of collection.songs) {
            for (const u of x.versions) {
                if (u.externalId != null) existingExternalIds.add(u.externalId);
            }
        }

        this.existingExternalIdsWithVirtual = existingExternalIds;

        this.virtualLikeLevels = new Map();

        const updatedLikes = [...applyRequest.updatedLikes].sort((a, b) => a.happenedOn.compareTo(b.happenedOn));
        for (const updatedLike of updatedLikes) {
            const immutableSong = this.songsById.get(updatedLike.songId);
            if (immutableSong === undefined) continue;

            const song = immutableSong.clone();

            const existing = song.likes.find(x =>
                x.versionId === updatedLike.versionId
                && (x.userId === userId || x.likeCategoryId != null && getRequired(this.likeCategoriesById, x.likeCategoryId).access === LikeCategoryAccess.SharedEditSingle)
                && x.likeCategoryId === updatedLike.likeCategoryId) ?? null;
            if (updatedLike.level != null) {
                const like = new Like({
                    createdOn: existing?.createdOn ?? updatedLike.happenedOn,
                    versionId: updatedLike.versionId,
                    userId: userId,
                    level: updatedLike.level,
                    transpose: updatedLike.transpose,
                    likeCategoryId: updatedLike.likeCategoryId,
                });

                if (existing !== null) {
                    listSet(song.likes, song.likes.indexOf(existing), like);
                } else {
                    song.likes.push(like);
                }
            } else if (existing !== null) {
                listRemove(song.likes, existing);
            }

            this.songsById.set(song.id, song);
            listSet(collection.songs, collection.songs.indexOf(immutableSong as Song), song);
            collection.etag = null;
        }

        const versionsById = toDictionary(
            collection.songs.flatMap(x => x.versions.map(v => ({ x, v }))),
            x => x.v.id,
            x => ({ version: x.v, song: x.x as ISong }));

        for (const removedVersion of applyRequest.removedVersions) {
            const immutableSong = versionsById.get(removedVersion.versionId);
            if (immutableSong === undefined) continue;

            const version = immutableSong.version;
            if (version.userId !== userId) continue;

            const song = immutableSong.song.clone();
            listRemove(song.versions, version);
            listRemoveAll(song.likes, x => x.versionId === removedVersion.versionId);

            this.songsById.set(song.id, song);
            listSet(collection.songs, collection.songs.indexOf(immutableSong.song as Song), song);
            collection.etag = null;

            versionsById.delete(version.id);
        }

        for (const importedVersion of applyRequest.importedVersions) {
            existingExternalIds.add(importedVersion.externalId);
            this.virtualLikeLevels.set(importedVersion.externalId, { level: importedVersion.level, likeCategoryId: importedVersion.likeCategoryId });
        }

        this.versionsByUri = EquatableLookup.from(
            collection.songs.flatMap(x => x.versions.map(v => ({ x, v }))),
            x => x.v.uri,
            x => ({ version: x.v, song: x.x as ISong }));

        this.versionsByExternalId = Lookup.from(
            collection.songs.flatMap(x => x.versions.map(v => ({ x, v }))).filter(x => x.v.externalId != null),
            x => x.v.externalId!,
            x => ({ version: x.v, song: x.x as ISong }));

        this.versionsById = versionsById;

        this.collection = collection;
        this._cache = buildCache<ISong>(collection.songs, x => x.artists.map(x => getRequired(this.artistsById, x)));
    }

    find(query: string): ISong[] {
        return find(this._cache, query, this._userId).found;
    }

    deepEquals(another: CollectionIndex): boolean {
        return sequenceEqual(orderNumbers(another.songsById.keys()), orderNumbers(this.songsById.keys()))
            && sequenceEqual(orderNumbers(another.artistsById.keys()), orderNumbers(this.artistsById.keys()))
            && sequenceEqual(orderNumbers(another.usersById.keys()), orderNumbers(this.usersById.keys()))
            && sequenceEqual(another.existingExternalIdsWithVirtual, this.existingExternalIdsWithVirtual)
            && this.collection === another.collection || sequenceEqual(this.collection.deepHash(), another.collection.deepHash());
    }
}
