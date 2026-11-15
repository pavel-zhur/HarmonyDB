import type { DateTime } from '../../../../common/dotnet/DateTime';
import { int64HashCode } from '../../../../common/dotnet/hashCode';
import { jsonByte, jsonClass, jsonDateTime, jsonInt32, jsonInt64, jsonNullable, jsonProperty, type JsonContract } from '../../../../common/dotnet/json/JsonSerializer';
import type { ILike } from '../../../../common/OneShelf.Common.Songs/ILike';

export interface LikeInit {
    userId: number;
    level: number;
    createdOn: DateTime;
    transpose: number | null;
    versionId: number | null;
    likeCategoryId: number | null;
}

export class Like implements ILike {
    readonly userId: number;
    /** byte */
    readonly level: number;
    readonly createdOn: DateTime;
    readonly transpose: number | null;
    readonly versionId: number | null;
    readonly likeCategoryId: number | null;

    constructor(init: LikeInit) {
        this.userId = init.userId;
        this.level = init.level;
        this.createdOn = init.createdOn;
        this.transpose = init.transpose;
        this.versionId = init.versionId;
        this.likeCategoryId = init.likeCategoryId;
    }

    deepHash(): number[] {
        return [
            int64HashCode(this.userId),
            this.level,
            this.createdOn.getHashCode(),
            this.transpose ?? -122,
            this.versionId ?? 0,
            this.likeCategoryId ?? 0,
        ];
    }

    static readonly json: JsonContract<Like> = jsonClass<Like>('OneShelf.Frontend.Api.Model.V3.Databasish.Like', init => new Like(init as unknown as LikeInit), () => [
        jsonProperty('UserId', jsonInt64, { required: true }),
        jsonProperty('Level', jsonByte, { required: true }),
        jsonProperty('CreatedOn', jsonDateTime, { required: true }),
        jsonProperty('Transpose', jsonNullable(jsonInt32), { required: true }),
        jsonProperty('VersionId', jsonNullable(jsonInt32), { required: true }),
        jsonProperty('LikeCategoryId', jsonNullable(jsonInt32), { required: true }),
    ]);
}
