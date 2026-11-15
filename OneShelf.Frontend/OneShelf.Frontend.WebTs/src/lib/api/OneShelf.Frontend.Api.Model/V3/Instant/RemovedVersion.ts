import { jsonClass, jsonInt32, jsonProperty, type JsonContract } from '../../../../common/dotnet/json/JsonSerializer';
import { ActionBase, type ActionBaseInit } from './ActionBase';

export interface RemovedVersionInit extends ActionBaseInit {
    versionId: number;
}

export class RemovedVersion extends ActionBase {
    versionId: number;

    constructor(init: RemovedVersionInit) {
        super(init);
        this.versionId = init.versionId;
    }

    static readonly json: JsonContract<RemovedVersion> = jsonClass<RemovedVersion>('OneShelf.Frontend.Api.Model.V3.Instant.RemovedVersion', init => new RemovedVersion(init as unknown as RemovedVersionInit), () => [
        jsonProperty('VersionId', jsonInt32, { required: true }),
        ...ActionBase.jsonProperties(),
    ]);
}
