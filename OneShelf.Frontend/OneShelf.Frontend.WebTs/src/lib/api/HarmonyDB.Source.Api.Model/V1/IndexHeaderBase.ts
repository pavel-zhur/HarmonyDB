import { jsonList, jsonNullable, jsonProperty, jsonSingle, jsonString, type JsonPropertyDefinition } from '../../../common/dotnet/json/JsonSerializer';

export interface IndexHeaderBaseInit {
    externalId: string;
    source: string;
    rating: number | null;
    title: string | null;
    artists: readonly string[] | null;
}

/** A record; not deserialized by itself on the site, only as the base of PublicHeaderBase. */
export class IndexHeaderBase {
    externalId: string;
    source: string;
    readonly rating: number | null;
    readonly title: string | null;
    readonly artists: readonly string[] | null;

    constructor(init: IndexHeaderBaseInit) {
        this.externalId = init.externalId;
        this.source = init.source;
        this.rating = init.rating;
        this.title = init.title;
        this.artists = init.artists;
    }

    static readonly jsonProperties = (): JsonPropertyDefinition[] => [
        jsonProperty('ExternalId', jsonString, { required: true }),
        jsonProperty('Source', jsonString, { required: true }),
        jsonProperty('Rating', jsonNullable(jsonSingle), { required: true }),
        jsonProperty('Title', jsonString, { required: true }),
        jsonProperty('Artists', jsonList(jsonString), { required: true }),
    ];
}
