// The JSON contract of HarmonyDB.Index.Analysis.Models.V1.CompressedChordsProgressionDataV1 (with ChordDataV1 and
// HarmonyDataV1), as DataProvider stores a progression in IndexedDB: JsonSerializer.Serialize with the default options —
// the C# property names in their declaration order, enums as numbers, nulls written; ChordDataV1.IsStandard is
// [JsonIgnore]. The analysis library has no JSON contracts of its own; this is the site's.
//
//   {"Sequence":[0,1,0],"Distinct":[{"OriginalRepresentation":"Am","HarmonyRepresentation":"Am",
//     "HarmonyData":{"Root":9,"Alteration":null,"ChordType":1},"ChordData":"[!9!]m"}, ...]}

import { ChordDataV1 } from '../lib/analysis/Models/V1/ChordDataV1';
import { CompressedChordsProgressionDataV1 } from '../lib/analysis/Models/V1/CompressedChordsProgressionDataV1';
import { HarmonyDataV1 } from '../lib/analysis/Models/V1/HarmonyDataV1';
import { jsonByte, jsonClass, jsonEnum, jsonInt32, jsonList, jsonNullable, jsonProperty, jsonString, type JsonContract } from '../lib/common/dotnet/json/JsonSerializer';

/** HarmonyDB.Index.Analysis.Models.ChordType : byte — written as its number. */
const jsonChordType = jsonEnum<number>('HarmonyDB.Index.Analysis.Models.ChordType');

/** HarmonyDB.Common.NoteAlteration — written as its number. */
const jsonNoteAlteration = jsonEnum<number>('HarmonyDB.Common.NoteAlteration');

export const HarmonyDataV1Json: JsonContract<HarmonyDataV1> = jsonClass<HarmonyDataV1>('HarmonyDB.Index.Analysis.Models.V1.HarmonyDataV1', init => new HarmonyDataV1(init as never), () => [
    jsonProperty('Root', jsonByte, { required: true }),
    jsonProperty('Alteration', jsonNullable(jsonNoteAlteration)),
    jsonProperty('ChordType', jsonChordType, { required: true }),
]);

export const ChordDataV1Json: JsonContract<ChordDataV1> = jsonClass<ChordDataV1>('HarmonyDB.Index.Analysis.Models.V1.ChordDataV1', init => new ChordDataV1(init as never), () => [
    jsonProperty('OriginalRepresentation', jsonString, { required: true }),
    jsonProperty('HarmonyRepresentation', jsonString, { required: true }),
    jsonProperty('HarmonyData', HarmonyDataV1Json),
    jsonProperty('ChordData', jsonString, { required: true }),
]);

export const CompressedChordsProgressionDataV1Json: JsonContract<CompressedChordsProgressionDataV1> = jsonClass<CompressedChordsProgressionDataV1>('HarmonyDB.Index.Analysis.Models.V1.CompressedChordsProgressionDataV1', init => new CompressedChordsProgressionDataV1(init as never), () => [
    jsonProperty('Sequence', jsonList(jsonInt32), { required: true }),
    jsonProperty('Distinct', jsonList(ChordDataV1Json), { required: true }),
]);
