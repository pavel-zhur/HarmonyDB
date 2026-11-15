import { jsonBoolean, jsonClass, jsonProperty, type JsonContract } from '../../../../common/dotnet/json/JsonSerializer';

export class DeleteLikeCategoryResponse {
    result: boolean;

    constructor(init: { result: boolean }) {
        this.result = init.result;
    }

    static readonly json: JsonContract<DeleteLikeCategoryResponse> = jsonClass<DeleteLikeCategoryResponse>('OneShelf.Frontend.Api.Model.V3.Api.DeleteLikeCategoryResponse', init => new DeleteLikeCategoryResponse(init as never), () => [
        jsonProperty('Result', jsonBoolean, { required: true }),
    ]);
}
