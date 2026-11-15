import { ArgumentOutOfRangeException, InvalidOperationException } from '../common/dotnet/exceptions';
import { htmlAttributeEncode, htmlEncode } from '../common/dotnet/html';
import { jsonClass, jsonProperty, type JsonContract } from '../common/dotnet/json/JsonSerializer';
import type { StringBuilder } from '../common/dotnet/StringBuilder';
import { NoteAlteration } from '../common/HarmonyDB.Common/NoteAlteration';
import { Note } from './Note';
import type { RepresentationSettings } from './RepresentationSettings';
import { NodeBase } from './NodeBase';
import './internal'; // fills nodeTypes, which the base class uses

export class NodeNote extends NodeBase {
    readonly note: Note;

    constructor(note: Note) {
        super();
        this.note = note;
    }

    representation(representationSettings: RepresentationSettings): string {
        return this.note.representation(representationSettings);
    }

    override appendHtmlTo(builder: StringBuilder, representationSettings: RepresentationSettings): void {
        builder.append(htmlEncode(this.representation(representationSettings)));
    }

    override appendChordsTo(): void {
        throw new InvalidOperationException();
    }

    override appendLyricsTo(): void {
        throw new InvalidOperationException();
    }

    override appendHtmlAttributeTo(builder: StringBuilder, representationSettings: RepresentationSettings): void {
        let alteration: string | null;
        switch (representationSettings.alteration ?? this.note.alteration) {
            case NoteAlteration.Flat: alteration = 'b'; break;
            case NoteAlteration.Sharp: alteration = '#'; break;
            case null: alteration = null; break;
            default: throw new ArgumentOutOfRangeException();
        }

        builder.append(htmlAttributeEncode(`[!${this.note.tone(representationSettings)}${alteration ?? ''}!]`));
    }

    override appendOneShelfTo(builder: StringBuilder, representationSettings: RepresentationSettings): void {
        builder.append(this.note.representation(representationSettings));
    }

    override shallowClone(): NodeBase {
        return new NodeNote(this.note);
    }

    static readonly json: JsonContract<NodeNote> = jsonClass<NodeNote>('HarmonyDB.Common.Representations.OneShelf.NodeNote', init => new NodeNote((init.note ?? null) as Note), () => [
        jsonProperty('Note', () => Note.json),
    ]);
}
