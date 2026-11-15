import { jsonClass, type JsonContract } from '../../../common/dotnet/json/JsonSerializer';

export class DeleteResponse {
    static readonly json: JsonContract<DeleteResponse> = jsonClass<DeleteResponse>('OneShelf.Collectives.Api.Model.V2.DeleteResponse', () => new DeleteResponse(), () => []);
}
