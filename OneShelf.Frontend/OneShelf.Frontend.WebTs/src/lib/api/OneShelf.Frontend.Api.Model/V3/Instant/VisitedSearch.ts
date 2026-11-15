import { jsonClass, jsonProperty, jsonString, type JsonContract } from '../../../../common/dotnet/json/JsonSerializer';
import { ActionBase, type ActionBaseInit } from './ActionBase';

export interface VisitedSearchInit extends ActionBaseInit {
    query: string;
}

export class VisitedSearch extends ActionBase {
    query: string;

    constructor(init: VisitedSearchInit) {
        super(init);
        this.query = init.query;
    }

    static readonly json: JsonContract<VisitedSearch> = jsonClass<VisitedSearch>('OneShelf.Frontend.Api.Model.V3.Instant.VisitedSearch', init => new VisitedSearch(init as unknown as VisitedSearchInit), () => [
        jsonProperty('Query', jsonString, { required: true }),
        ...ActionBase.jsonProperties(),
    ]);
}
