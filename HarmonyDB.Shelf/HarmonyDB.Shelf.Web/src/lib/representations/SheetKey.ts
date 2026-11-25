import { ArgumentOutOfRangeException } from '../common/dotnet/exceptions';
import { jsonBoolean, jsonClass, jsonProperty, jsonString, type JsonContract } from '../common/dotnet/json/JsonSerializer';
import { NoteAlteration } from '../common/HarmonyDB.Common/NoteAlteration';
import { Note } from './Note';
import { RepresentationSettings } from './RepresentationSettings';
import { NodeNote } from './internal';

const LetterFifths: ReadonlyMap<string, number> = new Map([
    ['F', -1], ['C', 0], ['G', 1], ['D', 2], ['A', 3], ['E', 4], ['B', 5],
]);

/** A key: its tonic as written (a letter, then its sharps or flats: "F#", "Bb", "C") and whether it is minor. A record. */
export class SheetKey {
    readonly tonic: string;
    readonly isMinor: boolean;

    constructor(tonic: string, isMinor: boolean) {
        if (!/^[A-G](#+|b+)?$/.test(tonic))
            throw new ArgumentOutOfRangeException('tonic');

        this.tonic = tonic;
        this.isMinor = isMinor;
    }

    /** The key's name: "F#m", "Bb". [JsonIgnore] */
    get name(): string {
        return this.isMinor ? `${this.tonic}m` : this.tonic;
    }

    /** The sharps (positive) or flats (negative) of its key signature: 3 for A and F#m, -2 for Bb and Gm. [JsonIgnore] */
    get signature(): number {
        return LetterFifths.get(this.tonic[0]!)! + 7 * this.accidentals - (this.isMinor ? 3 : 0);
    }

    private get accidentals(): number {
        return this.tonic.length === 1 ? 0 : this.tonic[1] === '#' ? this.tonic.length - 1 : 1 - this.tonic.length;
    }

    /** The note of the tonic, as Note.value. [JsonIgnore] */
    get value(): number {
        return Note.CharactersToNotes.get(this.tonic[0]!)!.add(this.accidentals).value;
    }

    /** Reads a key's name: "F#m", "Bb", "Am". */
    static parse(name: string): SheetKey {
        return name.endsWith('m') ? new SheetKey(name.substring(0, name.length - 1), true) : new SheetKey(name, false);
    }

    /**
     * The key as the sheet shows it: its tonic named as the chords' roots are under these settings (transposed with them,
     * with the sharps or flats asked for, as written when they are shown as written), so that the key and its chords agree.
     */
    shown(representationSettings: RepresentationSettings): SheetKey {
        const accidentals = this.accidentals;
        const node = new NodeNote(new Note(this.value, accidentals > 0 ? NoteAlteration.Sharp : accidentals < 0 ? NoteAlteration.Flat : null));
        node.accidentals = accidentals;
        const tonic = node.representation(representationSettings.with({ relativeTo: null, testOnlyX: false }));
        return new SheetKey(tonic, this.isMinor);
    }

    /** Whether the key has a key signature to draw: at most seven sharps or flats (G# has eight and has none). [JsonIgnore] */
    get hasSignature(): boolean {
        return Math.abs(this.signature) <= 7;
    }

    equals(other: SheetKey | null | undefined): boolean {
        return other != null && this.tonic === other.tonic && this.isMinor === other.isMinor;
    }

    static readonly json: JsonContract<SheetKey> = jsonClass<SheetKey>('HarmonyDB.Common.Representations.OneShelf.SheetKey', init => new SheetKey(init.tonic as string, init.isMinor as boolean), () => [
        jsonProperty('Tonic', jsonString),
        jsonProperty('IsMinor', jsonBoolean),
    ]);
}
