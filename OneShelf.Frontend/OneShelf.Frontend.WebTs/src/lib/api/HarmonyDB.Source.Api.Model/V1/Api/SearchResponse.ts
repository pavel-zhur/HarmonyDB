import { jsonClass, jsonList, jsonProperty, type JsonContract } from '../../../../common/dotnet/json/JsonSerializer';
import { SearchHeader } from '../SearchHeader';

export class SearchResponse {
    readonly headers: SearchHeader[];

    constructor(init: { headers: SearchHeader[] }) {
        this.headers = init.headers;
    }

    static readonly json: JsonContract<SearchResponse> = jsonClass<SearchResponse>('HarmonyDB.Source.Api.Model.V1.Api.SearchResponse', init => new SearchResponse(init as never), () => [
        jsonProperty('Headers', jsonList(SearchHeader.json), { required: true }),
    ]);
}
