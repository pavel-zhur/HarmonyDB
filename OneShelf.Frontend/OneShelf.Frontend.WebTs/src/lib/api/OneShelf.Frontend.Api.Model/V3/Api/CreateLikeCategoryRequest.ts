import { jsonClass, jsonProperty, type JsonContract } from '../../../../common/dotnet/json/JsonSerializer';
import { Identity } from '../../../OneShelf.Authorization.Api.Model/Identity';
import { LikeCategory } from '../Databasish/LikeCategory';

export class CreateLikeCategoryRequest {
    identity: Identity;
    likeCategory: LikeCategory;

    constructor(init: { identity: Identity; likeCategory: LikeCategory }) {
        this.identity = init.identity;
        this.likeCategory = init.likeCategory;
    }

    static readonly json: JsonContract<CreateLikeCategoryRequest> = jsonClass<CreateLikeCategoryRequest>('OneShelf.Frontend.Api.Model.V3.Api.CreateLikeCategoryRequest', init => new CreateLikeCategoryRequest(init as never), () => [
        jsonProperty('Identity', Identity.json, { required: true }),
        jsonProperty('LikeCategory', LikeCategory.json, { required: true }),
    ]);
}
