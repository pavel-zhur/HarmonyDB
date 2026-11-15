import type { DateTime } from '../../../../common/dotnet/DateTime';
import type { Guid } from '../../../../common/dotnet/Guid';
import { jsonClass, jsonDateTime, jsonGuid, jsonInt32, jsonInt64, jsonProperty, jsonUri, type JsonContract } from '../../../../common/dotnet/json/JsonSerializer';
import type { Uri } from '../../../../common/dotnet/Uri';
import { Collective } from './Collective';

export interface CollectiveVersionInit {
    collectiveId: Guid;
    collective: Collective;
    versionNumber: number;
    createdOn: DateTime;
    searchTag: number;
    createdByUserId: number;
    updatedOn: DateTime;
    uri: Uri;
}

export class CollectiveVersion {
    collectiveId: Guid;
    collective: Collective;
    versionNumber: number;
    createdOn: DateTime;
    searchTag: number;
    createdByUserId: number;
    updatedOn: DateTime;
    uri: Uri;

    constructor(init: CollectiveVersionInit) {
        this.collectiveId = init.collectiveId;
        this.collective = init.collective;
        this.versionNumber = init.versionNumber;
        this.createdOn = init.createdOn;
        this.searchTag = init.searchTag;
        this.createdByUserId = init.createdByUserId;
        this.updatedOn = init.updatedOn;
        this.uri = init.uri;
    }

    static readonly json: JsonContract<CollectiveVersion> = jsonClass<CollectiveVersion>('OneShelf.Collectives.Api.Model.V2.Sub.CollectiveVersion', init => new CollectiveVersion(init as unknown as CollectiveVersionInit), () => [
        jsonProperty('CollectiveId', jsonGuid, { required: true }),
        jsonProperty('Collective', Collective.json, { required: true }),
        jsonProperty('VersionNumber', jsonInt32, { required: true }),
        jsonProperty('CreatedOn', jsonDateTime, { required: true }),
        jsonProperty('SearchTag', jsonInt32, { required: true }),
        jsonProperty('CreatedByUserId', jsonInt64, { required: true }),
        jsonProperty('UpdatedOn', jsonDateTime, { required: true }),
        jsonProperty('Uri', jsonUri, { required: true }),
    ]);
}
