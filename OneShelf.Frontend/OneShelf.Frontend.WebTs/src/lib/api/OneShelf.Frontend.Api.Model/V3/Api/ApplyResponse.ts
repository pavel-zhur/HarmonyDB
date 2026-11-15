import { jsonClass, jsonProperty, type JsonContract } from '../../../../common/dotnet/json/JsonSerializer';
import { Collection } from '../Databasish/Collection';

export class ApplyResponse {
    readonly collection: Collection | null;

    constructor(init: { collection: Collection | null }) {
        this.collection = init.collection;
    }

    static readonly json: JsonContract<ApplyResponse> = jsonClass<ApplyResponse>('OneShelf.Frontend.Api.Model.V3.Api.ApplyResponse', init => new ApplyResponse(init as never), () => [
        jsonProperty('Collection', Collection.json, { required: true }),
    ]);
}
