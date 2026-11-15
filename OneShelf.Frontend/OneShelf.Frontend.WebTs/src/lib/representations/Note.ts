import { ArgumentOutOfRangeException, InvalidOperationException, KeyNotFoundException } from '../common/dotnet/exceptions';
import { jsonByte, jsonClass, jsonNullable, jsonProperty, type JsonContract } from '../common/dotnet/json/JsonSerializer';
import { toByte } from '../common/dotnet/Numbers';
import { NoteAlteration, NoteAlterationJson } from '../common/HarmonyDB.Common/NoteAlteration';
import type { RepresentationSettings } from './RepresentationSettings';
import { SignFlat, SignSharp } from './SignsConstants';

/** A record (equality by Value and Alteration). */
export class Note {
    static readonly Modulus = 12;

    readonly value: number;
    readonly alteration: NoteAlteration | null;

    constructor(value = 0, alteration: NoteAlteration | null = null) {
        this.value = value;
        this.alteration = alteration;
    }

    /** `note with { ... }` */
    with(changes: { value?: number; alteration?: NoteAlteration | null }): Note {
        return new Note(changes.value !== undefined ? changes.value : this.value, changes.alteration !== undefined ? changes.alteration : this.alteration);
    }

    equals(other: Note | null | undefined): boolean {
        return other != null && this.value === other.value && this.alteration === other.alteration;
    }

    static readonly A = new Note(0);
    static readonly B = new Note(2);
    static readonly C = new Note(3);
    static readonly D = new Note(5);
    static readonly E = new Note(7);
    static readonly F = new Note(8);
    static readonly G = new Note(10);

    static readonly Major: readonly number[] = [
        Note.A.value,
        Note.B.value,
        toByte(Note.C.value + 1),
        Note.D.value,
        Note.E.value,
        toByte(Note.F.value + 1),
        toByte(Note.G.value + 1),
    ];

    static readonly Minor: readonly number[] = [
        Note.A.value,
        Note.B.value,
        Note.C.value,
        Note.D.value,
        Note.E.value,
        Note.F.value,
        Note.G.value,
    ];

    static readonly CharactersToNotes: ReadonlyMap<string, Note> = new Map([
        ['A', Note.A],
        ['B', Note.B],
        ['C', Note.C],
        ['С', Note.C], // russian
        ['D', Note.D],
        ['E', Note.E],
        ['F', Note.F],
        ['G', Note.G],
        ['H', Note.B],
    ]);

    private static readonly NotesToCharacters: ReadonlyMap<number, string> = new Map(
        [...Note.CharactersToNotes]
            .filter(([key]) => key !== 'H' && key !== 'С') // russian 'С'
            .map(([key, value]) => [value.value, key]));

    static readonly Max = new Note(11);
    static readonly Min = Note.A;

    static readonly CircleOfFifths: readonly { readonly major: Note; readonly minor: Note; readonly isSharps: boolean }[] = (() => {
        const circleOfFifths: { major: Note; minor: Note; isSharps: boolean }[] = [];

        let current = new Note(3).add(-7);
        for (let i = 0; i < Note.Modulus; i++) {
            const isSharps = i <= 6;

            current = current.add(7);

            const applyAlteration = (x: Note) => x.with({
                alteration: Note.Minor.includes(x.value) ? null : isSharps ? NoteAlteration.Sharp : NoteAlteration.Flat,
            });

            current = applyAlteration(current);

            circleOfFifths.push({ major: current, minor: applyAlteration(current.add(-3)), isSharps });
        }

        return circleOfFifths;
    })();

    sharp(): Note {
        return new Note(
            this.value === Note.Max.value ? Note.Min.value : toByte(this.value + 1),
            NoteAlteration.Sharp);
    }

    flat(): Note {
        return new Note(
            this.value === Note.Min.value ? Note.Max.value : toByte((this.value + Note.Modulus - 1) % Note.Modulus),
            NoteAlteration.Flat);
    }

    add(delta: number): Note {
        let newValue = this.value + delta;
        newValue %= Note.Modulus;
        if (newValue < Note.Min.value) {
            newValue += Note.Modulus;
        }

        return new Note(toByte(newValue));
    }

    representation(representationSettings: RepresentationSettings): string {
        if (representationSettings.testOnlyX) return 'X';

        if (representationSettings.relativeTo != null) {
            const { note, major } = representationSettings.relativeTo;
            let scale = major ? Note.Major : Note.Minor;
            scale = scale.map(n => new Note(n).add(note).add(-representationSettings.transpose).value);
            // .Select((x, i) => (x, i: i + 1)).SingleOrDefault(x => x.x == Value).i
            const matching = scale.map((x, i) => ({ x, i: i + 1 })).filter(x => x.x === this.value);
            if (matching.length > 1) throw new InvalidOperationException('Sequence contains more than one matching element');
            const found = matching[0]?.i ?? 0;
            if (found > 0) return found.toString();
        }

        let value = this.value;
        let preferredAlteration = this.alteration;
        if (representationSettings.transpose !== 0) {
            value = toByte((value + Note.Modulus + representationSettings.transpose) % Note.Modulus);
            preferredAlteration = null;
        }

        const result = Note.NotesToCharacters.get(value);
        if (result !== undefined) return result;

        preferredAlteration = representationSettings.alteration ?? preferredAlteration;

        preferredAlteration ??= NoteAlteration.Sharp;

        switch (preferredAlteration) {
            case NoteAlteration.Flat:
                return `${Note.character(toByte(value === Note.Max.value ? Note.Min.value : value + 1))}${SignFlat}`;

            case NoteAlteration.Sharp:
                return `${Note.character(toByte(value === Note.Min.value ? Note.Max.value : value - 1))}${SignSharp}`;

            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private static character(value: number): string {
        const result = Note.NotesToCharacters.get(value);
        if (result === undefined) throw new KeyNotFoundException(value);
        return result;
    }

    tone(representationSettings: RepresentationSettings): number {
        let value = this.value;
        if (representationSettings.transpose !== 0) {
            value = toByte((value + Note.Modulus + representationSettings.transpose) % Note.Modulus);
        }

        return value;
    }

    length(): number {
        return this.alteration != null ? 2 : 1;
    }

    estimatedLength(representationSettings: RepresentationSettings): number {
        return this.representation(representationSettings).length;
    }

    static normalize(delta: number): number {
        return toByte((((delta % Note.Modulus) + Note.Modulus + Note.Modulus) % Note.Modulus));
    }

    static readonly json: JsonContract<Note> = jsonClass<Note>('HarmonyDB.Common.Representations.OneShelf.Note', init => new Note(
        init.value !== undefined ? init.value as number : 0,
        init.alteration !== undefined ? init.alteration as NoteAlteration | null : null,
    ), () => [
        jsonProperty('Value', jsonByte),
        jsonProperty('Alteration', jsonNullable(NoteAlterationJson)),
    ]);
}
