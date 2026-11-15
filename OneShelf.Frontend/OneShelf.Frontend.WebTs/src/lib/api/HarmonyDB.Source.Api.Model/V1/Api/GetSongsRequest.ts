import { jsonClass, jsonList, jsonProperty, jsonString, type JsonContract } from '../../../../common/dotnet/json/JsonSerializer';
import { Identity } from '../../../OneShelf.Authorization.Api.Model/Identity';

export class GetSongsRequest {
    readonly identity: Identity;
    readonly externalIds: readonly string[];

    constructor(init: { identity: Identity; externalIds: readonly string[] }) {
        this.identity = init.identity;
        this.externalIds = init.externalIds;
    }

    static readonly json: JsonContract<GetSongsRequest> = jsonClass<GetSongsRequest>('HarmonyDB.Source.Api.Model.V1.Api.GetSongsRequest', init => new GetSongsRequest(init as never), () => [
        jsonProperty('Identity', Identity.json, { required: true }),
        jsonProperty('ExternalIds', jsonList(jsonString), { required: true }),
    ]);
}
