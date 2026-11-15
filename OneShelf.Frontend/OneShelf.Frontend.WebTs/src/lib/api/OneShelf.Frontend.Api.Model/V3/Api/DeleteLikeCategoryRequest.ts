import { jsonClass, jsonInt32, jsonProperty, type JsonContract } from '../../../../common/dotnet/json/JsonSerializer';
import { Identity } from '../../../OneShelf.Authorization.Api.Model/Identity';

export class DeleteLikeCategoryRequest {
    identity: Identity;
    likeCategoryId: number;

    constructor(init: { identity: Identity; likeCategoryId: number }) {
        this.identity = init.identity;
        this.likeCategoryId = init.likeCategoryId;
    }

    static readonly json: JsonContract<DeleteLikeCategoryRequest> = jsonClass<DeleteLikeCategoryRequest>('OneShelf.Frontend.Api.Model.V3.Api.DeleteLikeCategoryRequest', init => new DeleteLikeCategoryRequest(init as never), () => [
        jsonProperty('Identity', Identity.json, { required: true }),
        jsonProperty('LikeCategoryId', jsonInt32, { required: true }),
    ]);
}
