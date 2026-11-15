import type { Guid } from '../../../common/dotnet/Guid';
import { jsonClass, jsonGuid, jsonProperty, type JsonContract } from '../../../common/dotnet/json/JsonSerializer';
import { Identity } from '../../OneShelf.Authorization.Api.Model/Identity';

export class DeleteRequest {
    identity: Identity;
    collectiveId: Guid;

    constructor(init: { identity: Identity; collectiveId?: Guid }) {
        this.identity = init.identity;
        this.collectiveId = init.collectiveId ?? '00000000-0000-0000-0000-000000000000';
    }

    static readonly json: JsonContract<DeleteRequest> = jsonClass<DeleteRequest>('OneShelf.Collectives.Api.Model.V2.DeleteRequest', init => new DeleteRequest(init as never), () => [
        jsonProperty('Identity', Identity.json, { required: true }),
        jsonProperty('CollectiveId', jsonGuid),
    ]);
}
