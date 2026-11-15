import { jsonClass, jsonInt32, jsonList, jsonProperty, type JsonContract } from '../../../../common/dotnet/json/JsonSerializer';
import { VisitedChords } from '../Instant/VisitedChords';

export class GetVisitedChordsResponse {
    readonly visitedChords: VisitedChords[];
    readonly pagesCount: number;

    constructor(init: { visitedChords: VisitedChords[]; pagesCount: number }) {
        this.visitedChords = init.visitedChords;
        this.pagesCount = init.pagesCount;
    }

    static readonly json: JsonContract<GetVisitedChordsResponse> = jsonClass<GetVisitedChordsResponse>('OneShelf.Frontend.Api.Model.V3.Api.GetVisitedChordsResponse', init => new GetVisitedChordsResponse(init as never), () => [
        jsonProperty('VisitedChords', jsonList(VisitedChords.json), { required: true }),
        jsonProperty('PagesCount', jsonInt32, { required: true }),
    ]);
}
