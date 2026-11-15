import { jsonClass, jsonProperty, jsonString, type JsonContract } from '../../../../common/dotnet/json/JsonSerializer';
import { Identity } from '../../../OneShelf.Authorization.Api.Model/Identity';

export class SearchRequest {
    readonly identity: Identity;
    readonly query: string;

    constructor(init: { identity: Identity; query: string }) {
        this.identity = init.identity;
        this.query = init.query;
    }

    static readonly json: JsonContract<SearchRequest> = jsonClass<SearchRequest>('HarmonyDB.Source.Api.Model.V1.Api.SearchRequest', init => new SearchRequest(init as never), () => [
        jsonProperty('Identity', Identity.json, { required: true }),
        jsonProperty('Query', jsonString, { required: true }),
    ]);
}
