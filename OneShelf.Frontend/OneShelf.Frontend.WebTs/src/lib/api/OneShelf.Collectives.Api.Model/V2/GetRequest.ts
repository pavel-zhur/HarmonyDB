import type { Guid } from '../../../common/dotnet/Guid';
import { jsonClass, jsonGuid, jsonProperty, type JsonContract } from '../../../common/dotnet/json/JsonSerializer';
import { Identity } from '../../OneShelf.Authorization.Api.Model/Identity';

export class GetRequest {
    identity: Identity;
    collectiveId: Guid;

    constructor(init: { identity: Identity; collectiveId: Guid }) {
        this.identity = init.identity;
        this.collectiveId = init.collectiveId;
    }

    static readonly json: JsonContract<GetRequest> = jsonClass<GetRequest>('OneShelf.Collectives.Api.Model.V2.GetRequest', init => new GetRequest(init as never), () => [
        jsonProperty('Identity', Identity.json, { required: true }),
        jsonProperty('CollectiveId', jsonGuid, { required: true }),
    ]);
}
