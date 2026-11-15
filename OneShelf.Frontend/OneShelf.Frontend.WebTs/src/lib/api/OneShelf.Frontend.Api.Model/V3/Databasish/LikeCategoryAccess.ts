import { jsonEnum } from '../../../../common/dotnet/json/JsonSerializer';

export enum LikeCategoryAccess {
    Private = 0,
    SharedView = 1,
    SharedEditMulti = 2,
    SharedEditSingle = 3,
}

export const LikeCategoryAccessJson = jsonEnum<LikeCategoryAccess>('OneShelf.Frontend.Api.Model.V3.Databasish.LikeCategoryAccess');
