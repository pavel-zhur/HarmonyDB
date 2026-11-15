// The original: Models/Fingerings/FingeringsChordType.cs.
// Notes is a dictionary keyed by FingeringsNote: in JSON its keys are the members' names (System.Text.Json writes
// and reads enum dictionary keys by name).

import { JsonException } from '../../lib/common/dotnet/exceptions';
import { JsonValueKind } from '../../lib/common/dotnet/json/JsonNode';
import { jsonClass, jsonList, jsonProperty, jsonString, type JsonContract } from '../../lib/common/dotnet/json/JsonSerializer';
import { writeJsonString } from '../../lib/common/dotnet/json/JsonWriter';
import { FingeringsNote } from './FingeringsNote';

function parseNoteName(name: string, path: string): FingeringsNote {
    const exact = (FingeringsNote as unknown as Record<string, number | string>)[name];
    if (typeof exact === 'number') return exact;
    for (const [key, value] of Object.entries(FingeringsNote)) {
        if (typeof value === 'number' && key.toLowerCase() === name.toLowerCase()) return value;
    }

    if (/^-?[0-9]+$/.test(name.trim())) return Number(name.trim());
    throw new JsonException(`The JSON value could not be converted to OneShelf.Frontend.Web.Models.Fingerings.FingeringsNote. Path: ${path}.`);
}

const jsonNotes: JsonContract<ReadonlyMap<FingeringsNote, readonly string[]>> = {
    typeName: 'IReadOnlyDictionary<FingeringsNote, IReadOnlyList<string>>',
    read(node, options, path) {
        if (node.kind === JsonValueKind.Null) return null!;
        if (node.kind !== JsonValueKind.Object) throw new JsonException(`The JSON value could not be converted to ${this.typeName}. Path: ${path}.`);
        const result = new Map<FingeringsNote, readonly string[]>();
        for (const [key, child] of node.entries) {
            result.set(parseNoteName(key, `${path}.${key}`), jsonList(jsonString).read(child, options, `${path}.${key}`));
        }

        return result;
    },
    write(value, options) {
        if (value == null) return 'null';
        const parts: string[] = [];
        for (const [key, child] of value) {
            parts.push(writeJsonString(FingeringsNote[key] ?? String(key)) + ':' + jsonList(jsonString).write([...child], options));
        }

        return '{' + parts.join(',') + '}';
    },
};

export class FingeringsChordType {
    readonly description: string;
    readonly chordHtml: string;
    readonly chordText: string;
    readonly url: string;
    readonly notes: ReadonlyMap<FingeringsNote, readonly string[]>;

    constructor(init: { description: string; chordHtml: string; chordText: string; url: string; notes: ReadonlyMap<FingeringsNote, readonly string[]> }) {
        this.description = init.description;
        this.chordHtml = init.chordHtml;
        this.chordText = init.chordText;
        this.url = init.url;
        this.notes = init.notes;
    }

    static readonly json: JsonContract<FingeringsChordType> = jsonClass<FingeringsChordType>('OneShelf.Frontend.Web.Models.Fingerings.FingeringsChordType', init => new FingeringsChordType(init as never), () => [
        jsonProperty('Description', jsonString, { required: true }),
        jsonProperty('ChordHtml', jsonString, { required: true }),
        jsonProperty('ChordText', jsonString, { required: true }),
        jsonProperty('Url', jsonString, { required: true }),
        jsonProperty('Notes', jsonNotes, { required: true }),
    ]);
}