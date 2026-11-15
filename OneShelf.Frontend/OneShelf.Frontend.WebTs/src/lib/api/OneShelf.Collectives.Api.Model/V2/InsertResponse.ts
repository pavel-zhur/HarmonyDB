import { jsonClass, jsonProperty, type JsonContract } from '../../../common/dotnet/json/JsonSerializer';
import { CollectiveVersion } from './Sub/CollectiveVersion';

export class InsertResponse {
    version: CollectiveVersion;

    constructor(init: { version: CollectiveVersion }) {
        this.version = init.version;
    }

    static readonly json: JsonContract<InsertResponse> = jsonClass<InsertResponse>('OneShelf.Collectives.Api.Model.V2.InsertResponse', init => new InsertResponse(init as never), () => [
        jsonProperty('Version', CollectiveVersion.json, { required: true }),
    ]);
}
