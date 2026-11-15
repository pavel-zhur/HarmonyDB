import { jsonClass, jsonInt32, jsonNullable, jsonProperty, jsonString, jsonUri, type JsonContract } from '../../../../common/dotnet/json/JsonSerializer';
import type { Uri } from '../../../../common/dotnet/Uri';
import { ActionBase, type ActionBaseInit } from './ActionBase';

export interface VisitedChordsInit extends ActionBaseInit {
    uri: Uri | null;
    songId: number | null;
    externalId: string | null;
    searchQuery: string | null;
    transpose: number | null;
    artists: string;
    title: string;
    source: string | null;
}

export class VisitedChords extends ActionBase {
    uri: Uri | null;
    songId: number | null;
    externalId: string | null;
    searchQuery: string | null;
    transpose: number | null;
    artists: string;
    title: string;
    source: string | null;

    constructor(init: VisitedChordsInit) {
        super(init);
        this.uri = init.uri;
        this.songId = init.songId;
        this.externalId = init.externalId;
        this.searchQuery = init.searchQuery;
        this.transpose = init.transpose;
        this.artists = init.artists;
        this.title = init.title;
        this.source = init.source;
    }

    static readonly json: JsonContract<VisitedChords> = jsonClass<VisitedChords>('OneShelf.Frontend.Api.Model.V3.Instant.VisitedChords', init => new VisitedChords(init as unknown as VisitedChordsInit), () => [
        jsonProperty('Uri', jsonUri, { required: true }),
        jsonProperty('SongId', jsonNullable(jsonInt32), { required: true }),
        jsonProperty('ExternalId', jsonString, { required: true }),
        jsonProperty('SearchQuery', jsonString, { required: true }),
        jsonProperty('Transpose', jsonNullable(jsonInt32), { required: true }),
        jsonProperty('Artists', jsonString, { required: true }),
        jsonProperty('Title', jsonString, { required: true }),
        jsonProperty('Source', jsonString, { required: true }),
        ...ActionBase.jsonProperties(),
    ]);
}
