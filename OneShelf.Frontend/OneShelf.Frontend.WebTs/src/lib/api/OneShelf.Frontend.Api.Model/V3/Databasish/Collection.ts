import { jsonClass, jsonInt32, jsonList, jsonNullable, jsonProperty, type JsonContract } from '../../../../common/dotnet/json/JsonSerializer';
import { Artist } from './Artist';
import type { ICollection } from './Interfaces/ICollection';
import { LikeCategory } from './LikeCategory';
import { Song } from './Song';
import { User } from './User';

export interface CollectionInit {
    songs: Song[];
    users: User[];
    artists: Artist[];
    likeCategories: LikeCategory[];
    etag?: number | null;
}

export class Collection implements ICollection {
    readonly songs: Song[];
    readonly users: User[];
    readonly artists: Artist[];
    readonly likeCategories: LikeCategory[];
    etag: number | null;

    constructor(init: CollectionInit) {
        this.songs = init.songs;
        this.users = init.users;
        this.artists = init.artists;
        this.likeCategories = init.likeCategories;
        this.etag = init.etag ?? null;
    }

    deepHash(): number[] {
        return [
            ...this.songs.flatMap(x => x.deepHash()),
            ...this.users.flatMap(x => x.deepHash()),
            ...this.artists.flatMap(x => x.deepHash()),
            ...this.likeCategories.flatMap(x => x.deepHash()),
            this.songs.length,
            this.users.length,
            this.artists.length,
            this.likeCategories.length,
        ];
    }

    clone(): Collection {
        return new Collection({
            artists: [...this.artists],
            songs: [...this.songs],
            users: [...this.users],
            etag: this.etag,
            likeCategories: [...this.likeCategories],
        });
    }

    static readonly json: JsonContract<Collection> = jsonClass<Collection>('OneShelf.Frontend.Api.Model.V3.Databasish.Collection', init => new Collection(init as unknown as CollectionInit), () => [
        jsonProperty('Songs', jsonList(Song.json), { required: true }),
        jsonProperty('Users', jsonList(User.json), { required: true }),
        jsonProperty('Artists', jsonList(Artist.json), { required: true }),
        jsonProperty('LikeCategories', jsonList(LikeCategory.json), { required: true }),
        jsonProperty('Etag', jsonNullable(jsonInt32)),
    ]);
}
