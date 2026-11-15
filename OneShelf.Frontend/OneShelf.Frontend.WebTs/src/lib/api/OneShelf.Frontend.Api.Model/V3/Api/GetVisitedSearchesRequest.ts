import { jsonClass, jsonInt32, jsonProperty, type JsonContract } from '../../../../common/dotnet/json/JsonSerializer';
import { Identity } from '../../../OneShelf.Authorization.Api.Model/Identity';

export class GetVisitedSearchesRequest {
    identity: Identity | null;
    pageIndex: number;

    constructor(init: { identity?: Identity | null; pageIndex?: number }) {
        this.identity = init.identity ?? null;
        this.pageIndex = init.pageIndex ?? 0;
    }

    static readonly json: JsonContract<GetVisitedSearchesRequest> = jsonClass<GetVisitedSearchesRequest>('OneShelf.Frontend.Api.Model.V3.Api.GetVisitedSearchesRequest', init => new GetVisitedSearchesRequest(init as never), () => [
        jsonProperty('Identity', Identity.json),
        jsonProperty('PageIndex', jsonInt32),
    ]);
}
