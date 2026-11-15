import type { DateTime } from '../../../../common/dotnet/DateTime';
import { int64HashCode, singleHashCode, stringHashCode } from '../../../../common/dotnet/hashCode';
import { jsonClass, jsonDateTime, jsonDictionary, jsonInt32, jsonInt64, jsonList, jsonNullable, jsonProperty, jsonSingle, jsonString, type JsonContract } from '../../../../common/dotnet/json/JsonSerializer';
import type { ISong } from './Interfaces/ISong';
import { Like } from './Like';
import { Version } from './Version';

export interface SongInit {
    id: number;
    index: number;
    title: string;
    templateRating?: number | null;
    artists: number[];
    additionalKeywords: string | null;
    createdByUserId: number;
    createdOn: DateTime;
    versions: Version[];
    likes: Like[];
    comments: Map<number, string>;
}

export class Song implements ISong {
    readonly id: number;
    readonly index: number;
    readonly title: string;
    /** float? */
    readonly templateRating: number | null;
    readonly artists: number[];
    readonly additionalKeywords: string | null;
    readonly createdByUserId: number;
    readonly createdOn: DateTime;
    readonly versions: Version[];
    readonly likes: Like[];
    readonly comments: Map<number, string>;

    constructor(init: SongInit) {
        this.id = init.id;
        this.index = init.index;
        this.title = init.title;
        this.templateRating = init.templateRating != null ? Math.fround(init.templateRating) : null;
        this.artists = init.artists;
        this.additionalKeywords = init.additionalKeywords;
        this.createdByUserId = init.createdByUserId;
        this.createdOn = init.createdOn;
        this.versions = init.versions;
        this.likes = init.likes;
        this.comments = init.comments;
    }

    deepHash(): number[] {
        return [
            this.id,
            this.index,
            this.templateRating != null ? singleHashCode(this.templateRating) : 0,
            stringHashCode(this.title),
            ...this.artists,
            this.additionalKeywords != null ? stringHashCode(this.additionalKeywords) : 0,
            int64HashCode(this.createdByUserId),
            this.createdOn.getHashCode(),
            ...this.versions.flatMap(x => x.deepHash()),
            ...this.likes.flatMap(x => x.deepHash()),
            ...[...this.comments].map(([key, value]) => int64HashCode(key) ^ stringHashCode(value)),
        ];
    }

    clone(): Song {
        return new Song({
            id: this.id,
            versions: [...this.versions],
            likes: [...this.likes],
            title: this.title,
            artists: [...this.artists],
            additionalKeywords: this.additionalKeywords,
            comments: new Map(this.comments),
            createdByUserId: this.createdByUserId,
            createdOn: this.createdOn,
            index: this.index,
            templateRating: this.templateRating,
        });
    }

    static readonly json: JsonContract<Song> = jsonClass<Song>('OneShelf.Frontend.Api.Model.V3.Databasish.Song', init => new Song(init as unknown as SongInit), () => [
        jsonProperty('Id', jsonInt32, { required: true }),
        jsonProperty('Index', jsonInt32, { required: true }),
        jsonProperty('Title', jsonString, { required: true }),
        jsonProperty('TemplateRating', jsonNullable(jsonSingle)),
        jsonProperty('Artists', jsonList(jsonInt32), { required: true }),
        jsonProperty('AdditionalKeywords', jsonString, { required: true }),
        jsonProperty('CreatedByUserId', jsonInt64, { required: true }),
        jsonProperty('CreatedOn', jsonDateTime, { required: true }),
        jsonProperty('Versions', jsonList(Version.json), { required: true }),
        jsonProperty('Likes', jsonList(Like.json), { required: true }),
        jsonProperty('Comments', jsonDictionary('int64', jsonString), { required: true }),
    ]);
}
