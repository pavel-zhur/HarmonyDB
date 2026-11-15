import { jsonBoolean, jsonClass, jsonProperty, type JsonContract } from '../../../../common/dotnet/json/JsonSerializer';

export class CreateLikeCategoryResponse {
    result: boolean;

    constructor(init: { result: boolean }) {
        this.result = init.result;
    }

    static readonly json: JsonContract<CreateLikeCategoryResponse> = jsonClass<CreateLikeCategoryResponse>('OneShelf.Frontend.Api.Model.V3.Api.CreateLikeCategoryResponse', init => new CreateLikeCategoryResponse(init as never), () => [
        jsonProperty('Result', jsonBoolean, { required: true }),
    ]);
}
