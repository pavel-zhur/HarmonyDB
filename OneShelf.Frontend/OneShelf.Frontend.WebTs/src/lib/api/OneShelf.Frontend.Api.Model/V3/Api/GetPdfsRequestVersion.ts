import { jsonBoolean, jsonClass, jsonInt32, jsonNullable, jsonProperty, type JsonContract } from '../../../../common/dotnet/json/JsonSerializer';
import { NoteAlterationJson, type NoteAlteration } from '../../../../common/HarmonyDB.Common/NoteAlteration';

export interface GetPdfsRequestVersionInit {
    versionId: number;
    transpose: number;
    alteration: NoteAlteration | null;
    twoColumns: boolean;
}

export class GetPdfsRequestVersion {
    readonly versionId: number;
    readonly transpose: number;
    readonly alteration: NoteAlteration | null;
    readonly twoColumns: boolean;

    constructor(init: GetPdfsRequestVersionInit) {
        this.versionId = init.versionId;
        this.transpose = init.transpose;
        this.alteration = init.alteration;
        this.twoColumns = init.twoColumns;
    }

    static readonly json: JsonContract<GetPdfsRequestVersion> = jsonClass<GetPdfsRequestVersion>('OneShelf.Frontend.Api.Model.V3.Api.GetPdfsRequestVersion', init => new GetPdfsRequestVersion(init as unknown as GetPdfsRequestVersionInit), () => [
        jsonProperty('VersionId', jsonInt32, { required: true }),
        jsonProperty('Transpose', jsonInt32, { required: true }),
        jsonProperty('Alteration', jsonNullable(NoteAlterationJson), { required: true }),
        jsonProperty('TwoColumns', jsonBoolean, { required: true }),
    ]);
}
