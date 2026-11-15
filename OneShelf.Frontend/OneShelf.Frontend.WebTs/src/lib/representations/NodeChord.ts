import { ArgumentOutOfRangeException, InvalidCastException, InvalidOperationException } from '../common/dotnet/exceptions';
import { htmlAttributeEncode } from '../common/dotnet/html';
import { jsonClass, jsonList, jsonProperty, type JsonContract } from '../common/dotnet/json/JsonSerializer';
import { StringBuilder } from '../common/dotnet/StringBuilder';
import { charAt, containsIgnoreCase, isDigit, replace, replaceIgnoreCase, startsWithIgnoreCase, substring } from '../common/dotnet/text';
import { withIndices, withIsFirst } from '../common/OneShelf.Common/Extensions';
import { Note } from './Note';
import type { RepresentationSettings } from './RepresentationSettings';
import { hasFlag, SimplificationMode } from './SimplificationMode';
import type { TraversalContext } from './TraversalContext';
import type { NodeBase, Ref } from './NodeBase';
import { NodeCollectionBase } from './NodeCollectionBase';
import { NodeChild, NodeNote, NodeText } from './internal';

// The .NET patterns, with .NET's `$` (end, or before a final \n) written as (?=\n?$).
const remove9AndMorePattern1 = /^(?<take>m|maj|mmaj)?[b\-+#]?(9|11|13)(b-\+#)?(?=\n?$)/g;
const remove9AndMorePattern2 = /^(9|11|13)(?<retain>sus|add|dim|aug|lyd)/g;
const remove9AndMorePattern3 = /add(9|11|13)[\-+]?(?=\n?$)|\((9|11|13)-\)|\((b|add|add#|addb|add-|add\+|-|#)?(9|11|13)\)|^M(9|11|13)(?=\n?$)|(add|addb|add-|add#|add\+|b|#|-|\+)?(9|11|13)[+\-b]?(?=\n?$)/g;
const remove7Pattern = /^7\+(?=\n?$)|^7M|\(maj7\)|maj7|major7|^\+7|7\+(?=\n?$)|7#(?=\n?$)|\+7(?=\n?$)|^7(?=\n?$)|\((maj|#)?7[+#]?\)/gi;

function asNodeText(node: NodeBase): NodeText {
    if (!(node instanceof NodeText)) throw new InvalidCastException();
    return node;
}

export class NodeChord extends NodeCollectionBase {
    /** `new NodeChord()` or `new NodeChord { Children = ... }` */
    constructor(init: { children?: NodeChild[] } = {}) {
        super(init.children);
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

        builder.append('\'>');

        traversalContext.isInsideChord = true;
        super.appendHtmlTo(builder, representationSettings, traversalContext);
        traversalContext.isInsideChord = false;

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

            let start = Note.CharactersToNotes.get(part[0]!);
            if (start === undefined) {
                newChildren.push(new NodeText(part).asChild());
                continue;
            }

            let modified = false;
            if (part.length > 1 && part[1] === '#') {
                start = start.sharp();
                modified = true;
            }

            if (part.length > 1 && part[1] === 'b') {
                start = start.flat();
                modified = true;
            }

            newChildren.push(new NodeNote(start).asChild());

            if (part.length > (modified ? 2 : 1)) {
                newChildren.push(new NodeText(substring(part, modified ? 2 : 1)).asChild());
            }
        }

        this.children.length = 0;
        this.children.push(...newChildren);

        if (postfix.length > 0) {
            parent.children.splice(myIndex + 1, 0, new NodeText(postfix).asChild());
        }
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
        return new NodeChord();
    }

    static readonly json: JsonContract<NodeChord> = jsonClass<NodeChord>('HarmonyDB.Common.Representations.OneShelf.NodeChord', init => new NodeChord({ children: init.children as NodeChild[] | undefined }), () => [
        jsonProperty('Children', () => jsonList(NodeChild.json)),
    ]);
}
