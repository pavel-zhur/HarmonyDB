import { jsonClass, jsonProperty, type JsonContract } from '../../../common/dotnet/json/JsonSerializer';
import { CollectiveVersion } from './Sub/CollectiveVersion';

export class UpdateResponse {
    version: CollectiveVersion;

    constructor(init: { version: CollectiveVersion }) {
        this.version = init.version;
    }

    static readonly json: JsonContract<UpdateResponse> = jsonClass<UpdateResponse>('OneShelf.Collectives.Api.Model.V2.UpdateResponse', init => new UpdateResponse(init as never), () => [
        jsonProperty('Version', CollectiveVersion.json, { required: true }),
    ]);
}
