import { jsonBoolean, jsonClass, jsonProperty, type JsonContract } from '../../../../common/dotnet/json/JsonSerializer';

export class UpdateLikeCategoryResponse {
    result: boolean;

    constructor(init: { result: boolean }) {
        this.result = init.result;
    }

    static readonly json: JsonContract<UpdateLikeCategoryResponse> = jsonClass<UpdateLikeCategoryResponse>('OneShelf.Frontend.Api.Model.V3.Api.UpdateLikeCategoryResponse', init => new UpdateLikeCategoryResponse(init as never), () => [
        jsonProperty('Result', jsonBoolean, { required: true }),
    ]);
}
