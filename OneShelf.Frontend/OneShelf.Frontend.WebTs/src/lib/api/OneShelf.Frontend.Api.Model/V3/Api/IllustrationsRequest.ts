import { jsonBoolean, jsonClass, jsonInt32, jsonNullable, jsonProperty, type JsonContract } from '../../../../common/dotnet/json/JsonSerializer';
import { Identity } from '../../../OneShelf.Authorization.Api.Model/Identity';

export class IllustrationsRequest {
    identity: Identity;
    etag: number | null;
    allVersions: boolean;

    constructor(init: { identity: Identity; etag: number | null; allVersions: boolean }) {
        this.identity = init.identity;
        this.etag = init.etag;
        this.allVersions = init.allVersions;
    }

    static readonly json: JsonContract<IllustrationsRequest> = jsonClass<IllustrationsRequest>('OneShelf.Frontend.Api.Model.V3.Api.IllustrationsRequest', init => new IllustrationsRequest(init as never), () => [
        jsonProperty('Identity', Identity.json, { required: true }),
        jsonProperty('Etag', jsonNullable(jsonInt32), { required: true }),
        jsonProperty('AllVersions', jsonBoolean, { required: true }),
    ]);
}
