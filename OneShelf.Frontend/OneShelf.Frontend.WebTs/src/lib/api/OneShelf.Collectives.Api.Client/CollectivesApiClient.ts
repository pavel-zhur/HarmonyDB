// Of CollectivesApiClient the site calls Get (V2), Delete, Update and Insert. List, ListAll and the internal Get(Guid),
// Get(Uri) (master-code calls between services) are not reached; not ported.

import type { Guid } from '../../common/dotnet/Guid';
import type { Identity } from '../OneShelf.Authorization.Api.Model/Identity';
import * as CollectivesApiUrls from '../OneShelf.Collectives.Api.Model/CollectivesApiUrls';
import { DeleteRequest } from '../OneShelf.Collectives.Api.Model/V2/DeleteRequest';
import { DeleteResponse } from '../OneShelf.Collectives.Api.Model/V2/DeleteResponse';
import { GetRequest } from '../OneShelf.Collectives.Api.Model/V2/GetRequest';
import { GetResponse } from '../OneShelf.Collectives.Api.Model/V2/GetResponse';
import { InsertRequest } from '../OneShelf.Collectives.Api.Model/V2/InsertRequest';
import { InsertResponse } from '../OneShelf.Collectives.Api.Model/V2/InsertResponse';
import type { Collective } from '../OneShelf.Collectives.Api.Model/V2/Sub/Collective';
import { UpdateRequest } from '../OneShelf.Collectives.Api.Model/V2/UpdateRequest';
import { UpdateResponse } from '../OneShelf.Collectives.Api.Model/V2/UpdateResponse';
import { ApiClientBase, type HttpFetch } from '../OneShelf.Common.Api.Client/ApiClientBase';
import { ApiClientOptions } from '../OneShelf.Common.Api.Client/ApiClientOptions';
import type { ConfigurationGetter } from '../OneShelf.Common.Api.Client/Configuration';

/** Registered by AddCollectivesApiClient: the options come from "CollectivesApiClientOptions". */
export class CollectivesApiClient extends ApiClientBase {
    constructor(options: ApiClientOptions, httpFetch?: HttpFetch) {
        super(options, httpFetch);
    }

    /** AddCollectivesApiClient(configuration) */
    static create(configuration: ConfigurationGetter, httpFetch?: HttpFetch): CollectivesApiClient {
        return new CollectivesApiClient(ApiClientOptions.bind(configuration, 'CollectivesApiClient'), httpFetch);
    }

    async get(collectiveId: Guid, identity: Identity): Promise<GetResponse> {
        return await this.httpPost(CollectivesApiUrls.V2Get, new GetRequest({
            collectiveId,
            identity,
        }), GetRequest.json, GetResponse.json);
    }

    async delete(identity: Identity, collectiveId: Guid): Promise<DeleteResponse> {
        return await this.httpPost(CollectivesApiUrls.V2Delete, new DeleteRequest({
            identity,
            collectiveId,
        }), DeleteRequest.json, DeleteResponse.json);
    }

    async update(identity: Identity, collectiveId: Guid, collective: Collective): Promise<UpdateResponse> {
        return await this.httpPost(CollectivesApiUrls.V2Update, new UpdateRequest({
            identity,
            collectiveId,
            collective,
        }), UpdateRequest.json, UpdateResponse.json);
    }

    async insert(identity: Identity, collective: Collective, derivedFromVersionId: number | null): Promise<InsertResponse> {
        return await this.httpPost(CollectivesApiUrls.V2Insert, new InsertRequest({
            identity,
            collective,
            derivedFromVersionId,
        }), InsertRequest.json, InsertResponse.json);
    }
}
