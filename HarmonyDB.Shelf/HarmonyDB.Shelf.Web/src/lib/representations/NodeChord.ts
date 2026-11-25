import { ArgumentOutOfRangeException, InvalidCastException, InvalidOperationException } from '../common/dotnet/exceptions';
import { htmlAttributeEncode, htmlEncode } from '../common/dotnet/html';
import { jsonClass, jsonDouble, jsonList, jsonNullable, jsonProperty, jsonString, type JsonContract } from '../common/dotnet/json/JsonSerializer';
import { NoteAlteration } from '../common/HarmonyDB.Common/NoteAlteration';
import { StringBuilder } from '../common/dotnet/StringBuilder';
import { charAt, containsIgnoreCase, isDigit, replace, replaceIgnoreCase, startsWithIgnoreCase, substring } from '../common/dotnet/text';
import { withIndices, withIsFirst } from '../common/OneShelf.Common/Extensions';
import { Note } from './Note';
import type { RepresentationSettings } from './RepresentationSettings';
import { hasFlag, SimplificationMode } from './SimplificationMode';
import { TimeAttribute } from './TimeAttribute';
import type { TraversalContext } from './TraversalContext';
import { NodeBase, type Ref } from './NodeBase';
import { SheetPlace, type SheetTimes } from './SheetTimes';
import { NodeCollectionBase } from './NodeCollectionBase';
import { NodeChild, NodeNote, NodeText } from './internal';

// The .NET patterns, with .NET's `$` (end, or before a final \n) written as (?=\n?$).
const remove9AndMorePattern1 = /^(?<take>m|maj|mmaj)?[b\-+#]?(9|11|13)(b-\+#)?(?=\n?$)/g;
const remove9AndMorePattern2 = /^(9|11|13)(?<retain>sus|add|dim|aug|lyd)/g;
const remove9AndMorePattern3 = /add(9|11|13)[\-+]?(?=\n?$)|\((9|11|13)-\)|\((b|add|add#|addb|add-|add\+|-|#)?(9|11|13)\)|^M(9|11|13)(?=\n?$)|(add|addb|add-|add#|add\+|b|#|-|\+)?(9|11|13)[+\-b]?(?=\n?$)/g;
const alteredDegree = /^(5|9|11|13)/;
const remove7Pattern = /^7\+(?=\n?$)|^7M|\(maj7\)|maj7|major7|^\+7|7\+(?=\n?$)|7#(?=\n?$)|\+7(?=\n?$)|^7(?=\n?$)|\((maj|#)?7[+#]?\)/gi;

function asNodeText(node: NodeBase): NodeText {
    if (!(node instanceof NodeText)) throw new InvalidCastException();
    return node;
}

export interface NodeChordInit {
    children?: NodeChild[];
    degree?: string | null;
    bassDegree?: string | null;
    time?: number | null;
    place?: SheetPlace | null;
}

export class NodeChord extends NodeCollectionBase {
    /**
     * The chord's degree in the key where it stands, when the key is known: a Roman numeral (I to VII) after its sharps or flats, as the root's letter
     * stands against the key's scale: "V", "bVII", "#IV". It does not change when the sheet is transposed.
     */
    readonly degree: string | null;

    /** The degree of the chord's bass, written the same way, when the chord has a bass other than its root and the key is known. */
    readonly bassDegree: string | null;

    /** When the chord begins, in seconds from the start, when known. */
    readonly time: number | null;

    /** Where the chord begins in the sheet's bars (NodeHtml.times), in place of `time`. */
    readonly place: SheetPlace | null;

    /** `new NodeChord()` or `new NodeChord { Children = ..., Degree = ..., BassDegree = ..., Time = ... }` */
    constructor(init: NodeChordInit = {}) {
        super(init.children);
        this.degree = init.degree ?? null;
        this.bassDegree = init.bassDegree ?? null;
        this.time = init.time ?? null;
        this.place = init.place ?? null;
    }

    /** internal: the degree as it is shown, when it is: "VI", "I/V"; the bass's degree goes away with the bass (SimplificationMode.RemoveBass). */
    degreeRepresentation(representationSettings: RepresentationSettings): string | null {
        return !representationSettings.showDegrees || this.degree === null
            ? null
            : this.bassDegree !== null && !hasFlag(representationSettings.simplification, SimplificationMode.RemoveBass)
                ? `${this.degree}/${this.bassDegree}`
                : this.degree;
    }

    override appendHtmlTo(builder: StringBuilder, representationSettings: RepresentationSettings,
        traversalContext: TraversalContext): void {
        const me = traversalContext.siblingChords?.indexOf(this) ?? null;
        if (me === -1) throw new Error('Could not have happened.');

        const index = traversalContext.chordIndex++;

        if (me !== null && me > 0 && representationSettings.simplification > SimplificationMode.None
            && traversalContext.siblingChords![me - 1]!.representation(representationSettings) === this.representation(representationSettings)) {
            builder.append(' '.repeat(this.representation(representationSettings).length));
            return;
        }

        builder.append(`<b class='chord' chord-index='${index}' chord-custom='${htmlAttributeEncode(representationSettings.customAttributes.get(index) ?? null) ?? ''}' chord-data='`);

        this.appendHtmlAttributeTo(builder, representationSettings);

        builder.append('\'');

        const moment = this.moment(traversalContext.times);
        if (moment !== null) {
            builder.append(' ');
            builder.append(TimeAttribute.format(moment));
        }

        builder.append('>');

        traversalContext.isInsideChord = true;
        super.appendHtmlTo(builder, representationSettings, traversalContext);
        traversalContext.isInsideChord = false;

        const degree = this.degreeRepresentation(representationSettings);
        if (degree !== null) {
            builder.append('<sup class=\'chord-degree\'>');
            builder.append(htmlEncode(degree)!);
            builder.append('</sup>');
        }

        builder.append('</b>');
    }

    override appendChordsTo(destination: string[], representationSettings: RepresentationSettings): void {
        const builder = new StringBuilder();
        this.appendHtmlAttributeTo(builder, representationSettings);
        const chord = builder.toString();
        if (chord.includes('\r') || chord.includes('\n'))
            throw new Error('The chord may not contain the newline characters.');
        destination.push(chord);
    }

    override appendLyricsTo(): void {
    }

    override appendOneShelfTo(builder: StringBuilder, representationSettings: RepresentationSettings,
        isBold: boolean, owesWhitespace: Ref<boolean>, alreadyChordLine: boolean): void {
        if (alreadyChordLine) {
            super.appendOneShelfTo(builder, representationSettings, isBold, owesWhitespace, alreadyChordLine);
            return;
        }

        if (builder.length > 0 && builder.charAt(builder.length - 1) === ' ') {
            builder.setCharAt(builder.length - 1, '<');
        } else {
            builder.append('<');
        }

        super.appendOneShelfTo(builder, representationSettings, isBold, owesWhitespace, alreadyChordLine);
        builder.append('>');
        owesWhitespace.value = true;
    }

    representation(representationSettings: RepresentationSettings): string {
        return this.children.map(x => {
            const node = x.asNode();
            if (node instanceof NodeNote) return node.representation(representationSettings);
            if (node instanceof NodeText) return node.text;
            throw new ArgumentOutOfRangeException('x');
        }).join('');
    }

    override fix1_ExtractChordNotes(representationSettings: RepresentationSettings,
        parent2: { parent: NodeCollectionBase; myIndex: number } | null = null): void {
        let chord = this.children.map(x => asNodeText(x.asNode()).text).join('');

        if (chord.includes('\n')) throw new Error('A chord may not contain a new line.');

        if (chord === '') throw new Error('A chord may not be empty.');

        if (parent2 === null) throw new InvalidOperationException('Nullable object must have a value.');
        const { parent, myIndex } = parent2;

        // 6773577253408358652_-8081625970822610332 fix
        // todo: may be other impossible chords. run analysis.
        if (chord.startsWith('-') && [...chord].every(x => x === '-')) {
            parent.children[myIndex] = new NodeText(chord).asChild();
            return;
        }

        if (chord.startsWith('-')) {
            throw new Error(`A chord may not start with a -. Text: ${chord}`);
        }

        if (chord.startsWith('|') || chord.startsWith('/')) {
            if (chord.length > 1 && (chord[1] === '|' || chord[1] === '/'))
                throw new Error('Multiple vertical lines or slahses at the beginning of the chord are not supported.');

            if (chord.length > 1) {
                parent.children.splice(myIndex, 0, new NodeText(substring(chord, 0, 1)).asChild());

                const first = withIndices(this.children.map(x => asNodeText(x.asNode()))).find(x => x.x.text.length > 0);
                if (first === undefined) throw new InvalidOperationException('Sequence contains no matching element');
                this.children[first.i] = new NodeText(substring(chord, 1)).asChild();
            } else {
                parent.children[myIndex] = new NodeText(chord).asChild();
            }

            return; // gone or prepended and will be processed again
        }

        let postfix = '';
        if (chord.includes('--')) {
            const index = chord.indexOf('-');
            postfix = substring(chord, index);
            chord = substring(chord, 0, index);
        } else if (chord.endsWith('-') && !isDigit(charAt(chord, chord.length - 2))) {
            postfix = substring(chord, chord.length - 1);
            chord = substring(chord, 0, chord.length - 1);
        }

        let parts = chord.split('/');
        if (hasFlag(representationSettings.simplification, SimplificationMode.RemoveBass)) {
            postfix += ' '.repeat(parts.length - 1 + parts.slice(1).reduce((sum, x) => sum + x.length, 0));
            parts = parts.slice(0, 1);
        }

        const newChildren: NodeChild[] = [];

        for (const { x: partOriginal, isFirst } of withIsFirst(parts)) {
            if (!isFirst) {
                newChildren.push(new NodeText('/').asChild());
            }

            let part = partOriginal;
            if (isFirst) {
                const oldLength = part.length;
                part = NodeChord.simplify(part, representationSettings.simplification);
                postfix += ' '.repeat(oldLength - part.length);
            }

            if (part === '') continue;

            const natural = Note.CharactersToNotes.get(part[0]!);
            if (natural === undefined) {
                newChildren.push(new NodeText(part).asChild());
                continue;
            }

            // The letter, then its sharps or its flats: C#, Bb, E#; written double, both (F##m, Bbb7), unless the last sign alters
            // the degree after it (Bbb5 is Bb with b5, F##9 is F# with #9). The double is read from the text as written, the
            // simplification does not touch it; one sign is taken from the simplified text, as it always was
            // ("A#7#5" without the seventh is "A##5": A# and #5).
            const writtenLength = NodeChord.writtenNoteLength(partOriginal);
            const length = writtenLength > 2 && part.startsWith(substring(partOriginal, 0, writtenLength))
                ? writtenLength
                : part.length > 1 && (part[1] === '#' || part[1] === 'b') ? 2 : 1;

            const accidentals = part.length > 1 && part[1] === '#' ? length - 1 : 1 - length;
            const note = new NodeNote(natural.add(accidentals).with({
                alteration: accidentals > 0 ? NoteAlteration.Sharp : accidentals < 0 ? NoteAlteration.Flat : null,
            }));
            note.accidentals = accidentals;
            newChildren.push(note.asChild());

            if (part.length > length) {
                newChildren.push(new NodeText(substring(part, length)).asChild());
            }
        }

        this.children.length = 0;
        this.children.push(...newChildren);

        if (postfix.length > 0) {
            parent.children.splice(myIndex + 1, 0, new NodeText(postfix).asChild());
        }
    }

    /**
     * The length of the note at the beginning of the text: its letter and the sharps or the flats after it,
     * but for the last of two or more signs when it alters the degree after it (5, 9, 11, 13): "Bbb5" is Bb and b5.
     */
    private static writtenNoteLength(text: string): number {
        let length = 1;
        if (text.length > 1 && (text[1] === '#' || text[1] === 'b')) {
            while (length < text.length && text[length] === text[1]) length++;
        }

        if (length > 2 && alteredDegree.test(substring(text, length)))
            length--;

        return length;
    }

    private static simplify(part: string, simplificationMode: SimplificationMode): string {
        if (hasFlag(simplificationMode, SimplificationMode.Remove9AndMore)) {
            part = part.replace(remove9AndMorePattern1, '$<take>7');

            part = part.replace(remove9AndMorePattern2, '7$<retain>');

            for (let i = 0; i < 3; i++) {
                part = part.replace(remove9AndMorePattern3, '');
            }

            part = replace(part, '(b9,b13)', '');
        }

        if (hasFlag(simplificationMode, SimplificationMode.Remove6)) {
            part = [
                'add6', '(add6)', 'addb6', '(addb6)', 'addb6', '(add6b)', '(6b)', '(6b)', 'b6', '6b', '(6)', '-6', '(-6)',
            ].reduce(
                (current, search) => current.endsWith(search)
                    ? substring(current, 0, current.length - search.length)
                    : current,
                part);

            part = replace(replace(part, 'mmin6', 'm'), 'min6', 'm');

            part = replace(part, '6', '');
        }

        if (hasFlag(simplificationMode, SimplificationMode.RemoveSus)) {
            part = [
                'sus4', 'sus2', 'sus', '(sus4)', '(sus2)', '(sus)',
            ].reduce(
                (current, search) => containsIgnoreCase(current, search)
                    ? replaceIgnoreCase(current, search, current.includes('5') || startsWithIgnoreCase(current, 'm') && !current.startsWith('ma') ? '' : '5')
                    : current,
                part);
        }

        if (hasFlag(simplificationMode, SimplificationMode.Remove9AndMore)) {
            let any = false;
            for (let i = 0; i < 3; i++) {
                for (const search of ['9', '11', '13']) {
                    let postfix = `add${search}#`;
                    if (part.endsWith(postfix)) {
                        part = replace(part, postfix, '');
                        any = true;
                    }

                    postfix = `(add${search}#)`;
                    if (part.endsWith(postfix)) {
                        part = replace(part, postfix, '');
                        any = true;
                    }

                    postfix = `add${search}`;
                    if (part.includes(postfix)) {
                        part = replace(part, postfix, '');
                        any = true;
                    }

                    postfix = `(add${search})`;
                    if (part.includes(postfix)) {
                        part = replace(part, postfix, '');
                        any = true;
                    }

                    postfix = `(maj${search})`;
                    if (part.includes(postfix)) {
                        part = replace(part, postfix, 'maj7');
                        any = true;
                    }

                    postfix = `maj${search}`;
                    if (part.includes(postfix)) {
                        part = replace(part, postfix, 'maj7');
                        any = true;
                    }
                }

                if (!any) break;
            }

            part = replace(part, '9', part.includes('7') ? '' : '7');
            part = replace(part, '11', part.includes('7') ? '' : '7');
            part = replace(part, '13', part.includes('7') ? '' : '7');
        }

        if (hasFlag(simplificationMode, SimplificationMode.Remove7)) {
            part = replace(part.replace(remove7Pattern, ''), '7', '');
        }

        return part;
    }

    override shallowClone(): NodeBase {
        return new NodeChord({ degree: this.degree, bassDegree: this.bassDegree, time: this.time, place: this.place });
    }

    override moment(times: SheetTimes | null): number | null {
        return NodeBase.momentOf(this.time, this.place, times);
    }

    static readonly json: JsonContract<NodeChord> = jsonClass<NodeChord>('HarmonyDB.Common.Representations.OneShelf.NodeChord', init => new NodeChord({
        children: init.children as NodeChild[] | undefined,
        degree: init.degree as string | null | undefined,
        bassDegree: init.bassDegree as string | null | undefined,
        time: init.time as number | null | undefined,
        place: init.place as SheetPlace | null | undefined,
    }), () => [
        jsonProperty('Degree', jsonString, { ignoreWhenWritingNull: true }),
        jsonProperty('BassDegree', jsonString, { ignoreWhenWritingNull: true }),
        jsonProperty('Time', jsonNullable(jsonDouble), { ignoreWhenWritingNull: true }),
        jsonProperty('Place', () => SheetPlace.json, { ignoreWhenWritingNull: true }),
        jsonProperty('Children', () => jsonList(NodeChild.json)),
    ]);
}
