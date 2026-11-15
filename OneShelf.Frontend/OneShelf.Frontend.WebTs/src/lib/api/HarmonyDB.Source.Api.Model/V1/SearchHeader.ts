import { jsonBoolean, jsonClass, jsonProperty, type JsonContract, type JsonPropertyDefinition } from '../../../common/dotnet/json/JsonSerializer';
import { PublicHeaderBase, type PublicHeaderBaseInit } from './PublicHeaderBase';

export interface SearchHeaderInit extends PublicHeaderBaseInit {
    isSupported: boolean;
}

/** A record. */
export class SearchHeader extends PublicHeaderBase {
    readonly isSupported: boolean;

    constructor(init: SearchHeaderInit) {
        super(init);
        this.isSupported = init.isSupported;
    }

    static override readonly jsonProperties = (): JsonPropertyDefinition[] => [
        jsonProperty('IsSupported', jsonBoolean, { required: true }),
        ...PublicHeaderBase.jsonProperties(),
    ];

    static readonly json: JsonContract<SearchHeader> = jsonClass<SearchHeader>('HarmonyDB.Source.Api.Model.V1.SearchHeader', init => new SearchHeader(init as unknown as SearchHeaderInit), SearchHeader.jsonProperties);
}
