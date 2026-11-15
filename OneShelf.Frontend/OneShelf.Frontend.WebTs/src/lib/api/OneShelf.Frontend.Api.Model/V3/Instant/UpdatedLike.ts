import { jsonByte, jsonClass, jsonInt32, jsonNullable, jsonProperty, type JsonContract } from '../../../../common/dotnet/json/JsonSerializer';
import { ActionBase, type ActionBaseInit } from './ActionBase';

export interface UpdatedLikeInit extends ActionBaseInit {
    songId: number;
    versionId: number | null;
    level: number | null;
    transpose: number | null;
    likeCategoryId: number | null;
}

export class UpdatedLike extends ActionBase {
    songId: number;
    versionId: number | null;
    /** byte? */
    level: number | null;
    transpose: number | null;
    likeCategoryId: number | null;

    constructor(init: UpdatedLikeInit) {
        super(init);
        this.songId = init.songId;
        this.versionId = init.versionId;
        this.level = init.level;
        this.transpose = init.transpose;
        this.likeCategoryId = init.likeCategoryId;
    }

    static readonly json: JsonContract<UpdatedLike> = jsonClass<UpdatedLike>('OneShelf.Frontend.Api.Model.V3.Instant.UpdatedLike', init => new UpdatedLike(init as unknown as UpdatedLikeInit), () => [
        jsonProperty('SongId', jsonInt32, { required: true }),
        jsonProperty('VersionId', jsonNullable(jsonInt32), { required: true }),
        jsonProperty('Level', jsonNullable(jsonByte), { required: true }),
        jsonProperty('Transpose', jsonNullable(jsonInt32), { required: true }),
        jsonProperty('LikeCategoryId', jsonNullable(jsonInt32), { required: true }),
        ...ActionBase.jsonProperties(),
    ]);
}
