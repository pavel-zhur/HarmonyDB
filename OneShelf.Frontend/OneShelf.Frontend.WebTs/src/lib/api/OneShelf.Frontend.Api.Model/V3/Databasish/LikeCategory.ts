import { int64HashCode, singleHashCode, stringHashCode } from '../../../../common/dotnet/hashCode';
import { jsonClass, jsonInt32, jsonInt64, jsonProperty, jsonSingle, jsonString, type JsonContract } from '../../../../common/dotnet/json/JsonSerializer';
import { LikeCategoryAccess, LikeCategoryAccessJson } from './LikeCategoryAccess';

export interface LikeCategoryInit {
    id: number;
    userId: number;
    name: string;
    cssColor: string;
    order: number;
    privateWeight: number;
    cssIcon: string;
    access: LikeCategoryAccess;
}

export class LikeCategory {
    readonly id: number;
    readonly userId: number;
    readonly name: string;
    readonly cssColor: string;
    readonly order: number;
    /** float */
    readonly privateWeight: number;
    readonly cssIcon: string;
    readonly access: LikeCategoryAccess;

    constructor(init: LikeCategoryInit) {
        this.id = init.id;
        this.userId = init.userId;
        this.name = init.name;
        this.cssColor = init.cssColor;
        this.order = init.order;
        this.privateWeight = Math.fround(init.privateWeight);
        this.cssIcon = init.cssIcon;
        this.access = init.access;
    }

    deepHash(): number[] {
        return [
            this.id,
            int64HashCode(this.userId),
            stringHashCode(this.cssColor),
            this.order,
            singleHashCode(this.privateWeight),
            stringHashCode(this.cssIcon),
            this.access,
            stringHashCode(this.name),
        ];
    }

    static readonly json: JsonContract<LikeCategory> = jsonClass<LikeCategory>('OneShelf.Frontend.Api.Model.V3.Databasish.LikeCategory', init => new LikeCategory(init as unknown as LikeCategoryInit), () => [
        jsonProperty('Id', jsonInt32, { required: true }),
        jsonProperty('UserId', jsonInt64, { required: true }),
        jsonProperty('Name', jsonString, { required: true }),
        jsonProperty('CssColor', jsonString, { required: true }),
        jsonProperty('Order', jsonInt32, { required: true }),
        jsonProperty('PrivateWeight', jsonSingle, { required: true }),
        jsonProperty('CssIcon', jsonString, { required: true }),
        jsonProperty('Access', LikeCategoryAccessJson, { required: true }),
    ]);
}
