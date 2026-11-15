import type { Guid } from '../../../common/dotnet/Guid';
import { jsonClass, jsonGuid, jsonProperty, type JsonContract } from '../../../common/dotnet/json/JsonSerializer';
import { Identity } from '../../OneShelf.Authorization.Api.Model/Identity';
import { Collective } from './Sub/Collective';

export class UpdateRequest {
    identity: Identity;
    collectiveId: Guid;
    collective: Collective;

    constructor(init: { identity: Identity; collectiveId?: Guid; collective: Collective }) {
        this.identity = init.identity;
        this.collectiveId = init.collectiveId ?? '00000000-0000-0000-0000-000000000000';
        this.collective = init.collective;
    }

    static readonly json: JsonContract<UpdateRequest> = jsonClass<UpdateRequest>('OneShelf.Collectives.Api.Model.V2.UpdateRequest', init => new UpdateRequest(init as never), () => [
        jsonProperty('Identity', Identity.json, { required: true }),
        jsonProperty('CollectiveId', jsonGuid),
        jsonProperty('Collective', Collective.json, { required: true }),
    ]);
}
