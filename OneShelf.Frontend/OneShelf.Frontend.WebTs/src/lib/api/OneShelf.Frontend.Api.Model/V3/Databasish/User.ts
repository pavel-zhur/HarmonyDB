import { int64HashCode, stringHashCode } from '../../../../common/dotnet/hashCode';
import { jsonClass, jsonInt64, jsonProperty, jsonString, type JsonContract } from '../../../../common/dotnet/json/JsonSerializer';

export interface UserInit {
    id: number;
    title: string;
}

export class User {
    readonly id: number;
    readonly title: string;

    constructor(init: UserInit) {
        this.id = init.id;
        this.title = init.title;
    }

    deepHash(): number[] {
        return [
            int64HashCode(this.id),
            stringHashCode(this.title),
        ];
    }

    static readonly json: JsonContract<User> = jsonClass<User>('OneShelf.Frontend.Api.Model.V3.Databasish.User', init => new User(init as unknown as UserInit), () => [
        jsonProperty('Id', jsonInt64, { required: true }),
        jsonProperty('Title', jsonString, { required: true }),
    ]);
}
