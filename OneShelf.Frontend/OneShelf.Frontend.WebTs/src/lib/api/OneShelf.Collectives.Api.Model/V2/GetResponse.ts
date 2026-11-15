import { jsonClass, jsonProperty, type JsonContract } from '../../../common/dotnet/json/JsonSerializer';
import { CollectiveVersion } from './Sub/CollectiveVersion';

export class GetResponse {
    version: CollectiveVersion;

    constructor(init: { version: CollectiveVersion }) {
        this.version = init.version;
    }

    static readonly json: JsonContract<GetResponse> = jsonClass<GetResponse>('OneShelf.Collectives.Api.Model.V2.GetResponse', init => new GetResponse(init as never), () => [
        jsonProperty('Version', CollectiveVersion.json, { required: true }),
    ]);
}
