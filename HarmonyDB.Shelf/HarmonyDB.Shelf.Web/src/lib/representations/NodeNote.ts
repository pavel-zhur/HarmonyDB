import { ArgumentOutOfRangeException, InvalidOperationException } from '../common/dotnet/exceptions';
import { htmlAttributeEncode, htmlEncode } from '../common/dotnet/html';
import { jsonClass, jsonInt32, jsonNullable, jsonProperty, type JsonContract } from '../common/dotnet/json/JsonSerializer';
import type { StringBuilder } from '../common/dotnet/StringBuilder';
import { NoteAlteration } from '../common/HarmonyDB.Common/NoteAlteration';
import { Note } from './Note';
import type { RepresentationSettings } from './RepresentationSettings';
import { NodeBase } from './NodeBase';
import './internal'; // fills nodeTypes, which the base class uses

export class NodeNote extends NodeBase {
    readonly note: Note;

    /**
     * How many sharps (positive) or flats (negative) the note was written with, when it was read from a chord:
     * E# is E with one sharp, Bbb is B with two flats. See RepresentationSettings.showNotesAsWritten. (`init`)
     */
    accidentals: number | null = null;

    constructor(note: Note) {
        super();
        this.note = note;
    }

    representation(representationSettings: RepresentationSettings): string {
        // Not transposed and with no sharps or flats asked for, a note written with two or more signs is shown as written, as it always was;
        // with one sign, when the setting says so.
        const accidentals = this.accidentals;
        if (accidentals !== null
            && (representationSettings.showNotesAsWritten || Math.abs(accidentals) > 1)
            && representationSettings.transpose === 0 && representationSettings.alteration === null
            && representationSettings.relativeTo === null && !representationSettings.testOnlyX)
            return Note.written(this.note.value, accidentals);

        return this.note.representation(representationSettings);
    }

    /** The length of the note as written in the source text. */
    length(): number {
        return 1 + Math.abs(this.accidentals ?? (this.note.alteration !== null ? 1 : 0));
    }

    estimatedLength(representationSettings: RepresentationSettings): number {
        return this.representation(representationSettings).length;
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
        builder.append(this.representation(representationSettings));
    }

    override shallowClone(): NodeBase {
        const clone = new NodeNote(this.note);
        clone.accidentals = this.accidentals;
        return clone;
    }

    static readonly json: JsonContract<NodeNote> = jsonClass<NodeNote>('HarmonyDB.Common.Representations.OneShelf.NodeNote', init => {
        const node = new NodeNote((init.note ?? null) as Note);
        node.accidentals = (init.accidentals ?? null) as number | null;
        return node;
    }, () => [
        jsonProperty('Note', () => Note.json),
        jsonProperty('Accidentals', jsonNullable(jsonInt32), { ignoreWhenWritingNull: true }),
    ]);
}
