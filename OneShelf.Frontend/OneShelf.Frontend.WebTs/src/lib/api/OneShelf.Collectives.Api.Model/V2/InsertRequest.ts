import { jsonClass, jsonInt32, jsonNullable, jsonProperty, type JsonContract } from '../../../common/dotnet/json/JsonSerializer';
import { Identity } from '../../OneShelf.Authorization.Api.Model/Identity';
import { Collective } from './Sub/Collective';

export class InsertRequest {
    identity: Identity;
    collective: Collective;
    derivedFromVersionId: number | null;

    constructor(init: { identity: Identity; collective: Collective; derivedFromVersionId: number | null }) {
        this.identity = init.identity;
        this.collective = init.collective;
        this.derivedFromVersionId = init.derivedFromVersionId;
    }

    static readonly json: JsonContract<InsertRequest> = jsonClass<InsertRequest>('OneShelf.Collectives.Api.Model.V2.InsertRequest', init => new InsertRequest(init as never), () => [
        jsonProperty('Identity', Identity.json, { required: true }),
        jsonProperty('Collective', Collective.json, { required: true }),
        jsonProperty('DerivedFromVersionId', jsonNullable(jsonInt32), { required: true }),
    ]);
}
