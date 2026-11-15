import { jsonDictionary, jsonObjectAny, jsonProperty, jsonString, jsonUri, type JsonPropertyDefinition } from '../../../common/dotnet/json/JsonSerializer';
import type { Uri } from '../../../common/dotnet/Uri';
import { IndexHeaderBase, type IndexHeaderBaseInit } from './IndexHeaderBase';

export interface PublicHeaderBaseInit extends IndexHeaderBaseInit {
    sourceUri: Uri;
    type: string | null;
    specificAttributes: ReadonlyMap<string, unknown>;
}

/** A record. SpecificAttributes values are JsonElement after deserialization (C# `object?`). */
export class PublicHeaderBase extends IndexHeaderBase {
    sourceUri: Uri;
    readonly type: string | null;
    readonly specificAttributes: ReadonlyMap<string, unknown>;

    constructor(init: PublicHeaderBaseInit) {
        super(init);
        this.sourceUri = init.sourceUri;
        this.type = init.type;
        this.specificAttributes = init.specificAttributes;
    }

    static override readonly jsonProperties = (): JsonPropertyDefinition[] => [
        jsonProperty('SourceUri', jsonUri, { required: true }),
        jsonProperty('Type', jsonString, { required: true }),
        jsonProperty('SpecificAttributes', jsonDictionary('string', jsonObjectAny), { required: true }),
        ...IndexHeaderBase.jsonProperties(),
    ];
}
