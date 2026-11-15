import { jsonBoolean, jsonClass, jsonInt32, jsonList, jsonProperty, jsonString, type JsonContract } from '../../../../common/dotnet/json/JsonSerializer';
import { Identity } from '../../../OneShelf.Authorization.Api.Model/Identity';
import { GetPdfsRequestVersion } from './GetPdfsRequestVersion';

export interface GetPdfsRequestInit {
    identity: Identity;
    versions: GetPdfsRequestVersion[];
    chunkSize?: number;
    includeData: boolean;
    caption?: string | null;
    includeInspiration?: boolean;
    reindex?: boolean;
}

export class GetPdfsRequest {
    identity: Identity;
    versions: GetPdfsRequestVersion[];
    chunkSize: number;
    includeData: boolean;
    caption: string | null;
    includeInspiration: boolean;
    reindex: boolean;

    constructor(init: GetPdfsRequestInit) {
        this.identity = init.identity;
        this.versions = init.versions;
        this.chunkSize = init.chunkSize !== undefined ? init.chunkSize : 20;
        this.includeData = init.includeData;
        this.caption = init.caption ?? null;
        this.includeInspiration = init.includeInspiration ?? false;
        this.reindex = init.reindex ?? false;
    }

    static readonly json: JsonContract<GetPdfsRequest> = jsonClass<GetPdfsRequest>('OneShelf.Frontend.Api.Model.V3.Api.GetPdfsRequest', init => new GetPdfsRequest(init as unknown as GetPdfsRequestInit), () => [
        jsonProperty('Identity', Identity.json, { required: true }),
        jsonProperty('Versions', jsonList(GetPdfsRequestVersion.json), { required: true }),
        jsonProperty('ChunkSize', jsonInt32),
        jsonProperty('IncludeData', jsonBoolean, { required: true }),
        jsonProperty('Caption', jsonString),
        jsonProperty('IncludeInspiration', jsonBoolean),
        jsonProperty('Reindex', jsonBoolean),
    ]);
}
