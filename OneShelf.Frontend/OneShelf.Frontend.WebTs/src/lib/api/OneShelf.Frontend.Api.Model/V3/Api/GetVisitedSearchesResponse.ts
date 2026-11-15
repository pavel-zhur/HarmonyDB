import { jsonClass, jsonInt32, jsonList, jsonProperty, type JsonContract } from '../../../../common/dotnet/json/JsonSerializer';
import { VisitedSearch } from '../Instant/VisitedSearch';

export class GetVisitedSearchesResponse {
    readonly visitedSearches: VisitedSearch[];
    readonly pagesCount: number;

    constructor(init: { visitedSearches: VisitedSearch[]; pagesCount: number }) {
        this.visitedSearches = init.visitedSearches;
        this.pagesCount = init.pagesCount;
    }

    static readonly json: JsonContract<GetVisitedSearchesResponse> = jsonClass<GetVisitedSearchesResponse>('OneShelf.Frontend.Api.Model.V3.Api.GetVisitedSearchesResponse', init => new GetVisitedSearchesResponse(init as never), () => [
        jsonProperty('VisitedSearches', jsonList(VisitedSearch.json), { required: true }),
        jsonProperty('PagesCount', jsonInt32, { required: true }),
    ]);
}
