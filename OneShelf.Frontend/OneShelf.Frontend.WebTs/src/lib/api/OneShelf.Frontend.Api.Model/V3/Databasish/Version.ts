import type { DateTime } from '../../../../common/dotnet/DateTime';
import type { Guid } from '../../../../common/dotnet/Guid';
import { int64HashCode, stringHashCode } from '../../../../common/dotnet/hashCode';
import type { Uri } from '../../../../common/dotnet/Uri';
import { jsonClass, jsonDateTime, jsonDictionary, jsonGuid, jsonInt32, jsonInt64, jsonNullable, jsonProperty, jsonString, jsonUri, type JsonContract } from '../../../../common/dotnet/json/JsonSerializer';
import { VersionCollectiveType, VersionCollectiveTypeJson } from './VersionCollectiveType';

export interface VersionInit {
    id: number;
    uri: Uri;
    userId: number;
    source: string | null;
    externalId: string | null;
    createdOn: DateTime;
    comments: Map<number, string>;
    collectiveId?: Guid | null;
    collectiveSearchTag?: number | null;
    collectiveType?: VersionCollectiveType | null;
}

export class Version {
    readonly id: number;
    readonly uri: Uri;
    readonly userId: number;
    readonly source: string | null;
    readonly externalId: string | null;
    readonly createdOn: DateTime;
    readonly comments: Map<number, string>;
    readonly collectiveId: Guid | null;
    readonly collectiveSearchTag: number | null;
    readonly collectiveType: VersionCollectiveType | null;

    constructor(init: VersionInit) {
        this.id = init.id;
        this.uri = init.uri;
        this.userId = init.userId;
        this.source = init.source;
        this.externalId = init.externalId;
        this.createdOn = init.createdOn;
        this.comments = init.comments;
        this.collectiveId = init.collectiveId ?? null;
        this.collectiveSearchTag = init.collectiveSearchTag ?? null;
        this.collectiveType = init.collectiveType ?? null;
    }

    deepHash(): number[] {
        return [
            this.uri.getHashCode(),
            this.id,
            int64HashCode(this.userId),
            this.source != null ? stringHashCode(this.source) : 0,
            this.externalId != null ? stringHashCode(this.externalId) : 0,
            this.createdOn.getHashCode(),
            ...[...this.comments].map(([key, value]) => int64HashCode(key) ^ stringHashCode(value)),
            this.collectiveId != null ? stringHashCode(this.collectiveId) : 0,
            this.collectiveSearchTag ?? 0,
        ];
    }

    static readonly json: JsonContract<Version> = jsonClass<Version>('OneShelf.Frontend.Api.Model.V3.Databasish.Version', init => new Version(init as unknown as VersionInit), () => [
        jsonProperty('Id', jsonInt32, { required: true }),
        jsonProperty('Uri', jsonUri, { required: true }),
        jsonProperty('UserId', jsonInt64, { required: true }),
        jsonProperty('Source', jsonString, { required: true }),
        jsonProperty('ExternalId', jsonString, { required: true }),
        jsonProperty('CreatedOn', jsonDateTime, { required: true }),
        jsonProperty('Comments', jsonDictionary('int64', jsonString), { required: true }),
        jsonProperty('CollectiveId', jsonNullable(jsonGuid)),
        jsonProperty('CollectiveSearchTag', jsonNullable(jsonInt32)),
        jsonProperty('CollectiveType', jsonNullable(VersionCollectiveTypeJson)),
    ]);
}
