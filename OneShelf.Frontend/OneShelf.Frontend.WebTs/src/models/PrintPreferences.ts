// The original: Models/PrintPreferences.cs — what Preferences keeps in localStorage ("ppv1").
// HashSet<int> is a Set (in the order of the JSON / of adding), Dictionary a Map.

import { NoteAlteration } from '../lib/common/HarmonyDB.Common/NoteAlteration';
import { jsonClass, jsonDictionary, jsonEnum, jsonInt32, jsonList, jsonNullable, jsonProperty, jsonString, type JsonContract } from '../lib/common/dotnet/json/JsonSerializer';

/** HashSet<int>: a JSON array; a repeated value is kept once. */
const jsonInt32Set: JsonContract<Set<number>> = {
    typeName: 'HashSet<System.Int32>',
    read(node, options, path) {
        const list = jsonList(jsonInt32).read(node, options, path);
        return list === null ? null! : new Set(list);
    },
    write(value, options) {
        return jsonList(jsonInt32).write(value == null ? null! : [...value], options);
    },
};

export class PrintPreferences {
    alteration: NoteAlteration | null = null;
    transpositions: Map<number, number> = new Map();
    twoColumns: Set<number> = new Set();
    exclusions: Set<number> = new Set();
    comments: Map<number, string> = new Map();
    order: number[] | null = null;

    constructor(init: Partial<PrintPreferences> = {}) {
        Object.assign(this, init);
    }

    static readonly json: JsonContract<PrintPreferences> = jsonClass<PrintPreferences>('OneShelf.Frontend.Web.Models.PrintPreferences', init => new PrintPreferences(init as Partial<PrintPreferences>), () => [
        jsonProperty('Alteration', jsonNullable(jsonEnum<NoteAlteration>('HarmonyDB.Common.NoteAlteration'))),
        jsonProperty('Transpositions', jsonDictionary('int32', jsonInt32)),
        jsonProperty('TwoColumns', jsonInt32Set),
        jsonProperty('Exclusions', jsonInt32Set),
        jsonProperty('Comments', jsonDictionary('int32', jsonString)),
        jsonProperty('Order', jsonList(jsonInt32)),
    ]);
}