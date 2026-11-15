import { jsonByte, jsonClass, jsonInt32, jsonNullable, jsonProperty, jsonString, type JsonContract } from '../../../../common/dotnet/json/JsonSerializer';
import { ActionBase, type ActionBaseInit } from './ActionBase';

export interface ImportedVersionInit extends ActionBaseInit {
    externalId: string;
    level: number;
    transpose: number;
    likeCategoryId?: number | null;
}

export class ImportedVersion extends ActionBase {
    externalId: string;
    /** byte */
    level: number;
    transpose: number;
    likeCategoryId: number | null;

    constructor(init: ImportedVersionInit) {
        super(init);
        this.externalId = init.externalId;
        this.level = init.level;
        this.transpose = init.transpose;
        this.likeCategoryId = init.likeCategoryId ?? null;
    }

    static readonly json: JsonContract<ImportedVersion> = jsonClass<ImportedVersion>('OneShelf.Frontend.Api.Model.V3.Instant.ImportedVersion', init => new ImportedVersion(init as unknown as ImportedVersionInit), () => [
        jsonProperty('ExternalId', jsonString, { required: true }),
        jsonProperty('Level', jsonByte, { required: true }),
        jsonProperty('Transpose', jsonInt32, { required: true }),
        jsonProperty('LikeCategoryId', jsonNullable(jsonInt32)),
        ...ActionBase.jsonProperties(),
    ]);
}
