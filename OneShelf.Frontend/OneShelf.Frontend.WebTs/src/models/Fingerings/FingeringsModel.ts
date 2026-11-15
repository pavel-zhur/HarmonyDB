// The original: Models/Fingerings/FingeringsModel.cs.

import { jsonBytes, jsonClass, jsonDictionary, jsonList, jsonProperty, type JsonContract } from '../../lib/common/dotnet/json/JsonSerializer';
import { FingeringsChordType } from './FingeringsChordType';

export class FingeringsModel {
    readonly types: readonly FingeringsChordType[];
    /** byte[] values (base64 in the JSON). */
    readonly pictures: ReadonlyMap<string, Uint8Array>;

    constructor(init: { types: readonly FingeringsChordType[]; pictures: ReadonlyMap<string, Uint8Array> }) {
        this.types = init.types;
        this.pictures = init.pictures;
    }

    static readonly json: JsonContract<FingeringsModel> = jsonClass<FingeringsModel>('OneShelf.Frontend.Web.Models.Fingerings.FingeringsModel', init => new FingeringsModel(init as never), () => [
        jsonProperty('Types', jsonList(FingeringsChordType.json), { required: true }),
        jsonProperty('Pictures', jsonDictionary('string', jsonBytes), { required: true }),
    ]);
}