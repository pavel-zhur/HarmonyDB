import { ArgumentOutOfRangeException } from '../common/dotnet/exceptions';
import { jsonClass, jsonList, jsonProperty, type JsonContract } from '../common/dotnet/json/JsonSerializer';
import type { StringBuilder } from '../common/dotnet/StringBuilder';
import { isNullOrWhiteSpace, isWhiteSpace, substring, trim } from '../common/dotnet/text';
import type { RepresentationSettings } from './RepresentationSettings';
import type { TraversalContext } from './TraversalContext';
import type { NodeBase, Ref } from './NodeBase';
import { NodeCollectionBase } from './NodeCollectionBase';
import { TimeAttribute } from './TimeAttribute';
import { NodeChild, NodeChord, NodeHtml, NodeMark, NodeNote, NodeText } from './internal';

export class NodeLine extends NodeCollectionBase {
    private _isDouble = false;
    private _isZero = false;

    /** `new NodeLine()` or `new NodeLine { Children = ... }` */
    constructor(init: { children?: NodeChild[] } = {}) {
        super(init.children);
    }

    override appendHtmlTo(builder: StringBuilder, representationSettings: RepresentationSettings,
        traversalContext: TraversalContext): void {
        if (this._isZero) return;

        if (this._isDouble) {
            builder.append('<div class=\'chords-double-line\'>');
        }

        // The line's time is the time of the first thing in it that has one.
        const time = this.traverseAll().map(x => x.element instanceof NodeText ? x.element.time
            : x.element instanceof NodeChord ? x.element.time
                : x.element instanceof NodeMark ? x.element.time
                    : null).find(x => x !== null) ?? null;

        builder.append(`<span class='chords-line ${this._isDouble ? 'chords-double-line' : ''}'${time !== null ? ` ${TimeAttribute.format(time)}` : ''}>`);

        traversalContext.siblingChords = this.traverseAll().map(x => x.element).filter((x): x is NodeChord => x instanceof NodeChord);
        super.appendHtmlTo(builder, representationSettings, traversalContext);
        traversalContext.siblingChords = null;

        builder.append('</span>');

        if (this._isDouble) {
            builder.append('</div>');
        } else {
            builder.append('\n');
        }
    }

    override appendLyricsTo(builder: StringBuilder): void {
        if (this._isZero) return;
        super.appendLyricsTo(builder);
        builder.appendLine();
    }

    override appendOneShelfTo(builder: StringBuilder, representationSettings: RepresentationSettings,
        isBold: boolean, owesWhitespace: Ref<boolean>, alreadyChordLine: boolean): void {
        super.appendOneShelfTo(builder, representationSettings, isBold, owesWhitespace, alreadyChordLine);
        builder.appendLine();
        owesWhitespace.value = false;
    }

    override shallowClone(): NodeBase {
        return new NodeLine();
    }

    override cloneFixingNewLines(): { clone: NodeCollectionBase; completed: boolean } {
        throw new Error('This operation will not be called on this type.');
    }

    tryInjectTo(next: NodeLine, representationSettings: RepresentationSettings): void {
        const currentLineHasTextOutsideChords = this.traverseAll().some(x =>
            x.element instanceof NodeText && !isNullOrWhiteSpace(x.element.text)
            && !x.parentsReverse.some(p => p instanceof NodeChord)
            && [...x.element.text].some(c => !isWhiteSpace(c) && !'()*/\\!-|\'_><'.includes(c)));

        if (currentLineHasTextOutsideChords) return;

        // A mark (a key, a tempo, a time signature) stands on a line of its own.
        if (this.traverseAll().concat(next.traverseAll()).some(x => x.element instanceof NodeMark)) return;

        const currentLineHasChords = this.traverseAll().some(x => x.element instanceof NodeChord);

        if (!currentLineHasChords) return;

        const nextLineHasChords = next.traverseAll().some(x => x.element instanceof NodeChord);

        if (nextLineHasChords) return;

        const nextLineHasNonEmptyText = next.traverseAll()
            .some(x => x.element instanceof NodeText && !isNullOrWhiteSpace(x.element.text));

        if (!nextLineHasNonEmptyText) return;

        const differentBlockIds = new Set(this.traverseAll()
            .filter(x => x.element !== this)
            .map(x => x.element.blockId)
            .concat(next.traverseAll()
                .filter(x => x.element !== next)
                .map(x => x.element.blockId)))
            .size > 1;

        if (differentBlockIds) return;

        let position = 0;
        let flies: { position: number; node: NodeBase; length: number }[] = [];
        const fix = representationSettings.isVariableWidth ? .66 : 1;
        for (const child of this.traverseAll()) {
            if (child.element instanceof NodeText) {
                const nodeText = child.element;
                if (child.parentsReverse.some(p => p instanceof NodeChord)) continue;

                if (!isNullOrWhiteSpace(nodeText.text)) {
                    const trimmed = trim(nodeText.text);
                    const trimmedIndex = nodeText.text.indexOf(trimmed);
                    if (trimmedIndex === -1) throw new Error('Could not have happened.');

                    position += Math.trunc(trimmedIndex * fix);
                    flies.push({ position, node: new NodeText(trimmed, nodeText.time), length: trimmed.length });
                    position += Math.trunc((nodeText.text.length - trimmedIndex) * fix);
                } else {
                    position += Math.trunc(nodeText.text.length * fix);
                }
            } else if (child.element instanceof NodeChord) {
                const nodeChord = child.element;
                const length = nodeChord.traverseAll().reduce((sum, x) =>
                    sum + (x.element instanceof NodeText ? x.element.text.length
                        : x.element instanceof NodeNote ? x.element.length()
                            : 0), 0);
                const estimatedLength = nodeChord.traverseAll().reduce((sum, x) =>
                    sum + (x.element instanceof NodeText ? x.element.text.length
                        : x.element instanceof NodeNote ? Math.trunc(x.element.estimatedLength(representationSettings) / fix) + (representationSettings.isVariableWidth ? 2 : 0)
                            : 0), 0) + (nodeChord.degreeRepresentation(representationSettings)?.length ?? 0);
                flies.push({ position, node: nodeChord, length: estimatedLength });
                position += length;
            }
        }

        if (flies.length === 0) return;

        flies = [...flies].sort((a, b) => a.position - b.position);

        position = 0;
        for (let i = 0; i < flies.length; i++) {
            let fly = flies[i]!;
            if (fly.position < position) {
                flies[i] = { position, node: fly.node, length: fly.length };
                fly = flies[i]!;
            }

            position = fly.position + fly.length + 1;
        }

        const line1Length = Math.max(...flies.map(x => x.position + x.length));
        const line2Length = next.traverseAll().reduce((sum, x) => sum + (x.element instanceof NodeText ? x.element.text.length : 0), 0);

        if (line1Length > line2Length) {
            next.children.push(new NodeText(' '.repeat(line1Length - line2Length + 1)).asChild());
        }

        position = 0;
        for (const child of next.traverseAll()) {
            if (!(child.element instanceof NodeText)) continue;
            const text = child.element;

            if (text.text.length === 0) continue;

            const start = position;
            const end = position + text.text.length;
            position = end;

            const matching = flies
                .filter(x => x.position >= start && x.position < end)
                .map(x => ({ node: x.node, position: x.position }));

            if (matching.length === 0) continue;

            const parent = child.parentsReverse[0]!;
            let currentText = text;
            let currentPosition = start;

            for (const { node, position: position2 } of matching) {
                const position2Relative = position2 - currentPosition;
                const currentTextChild = currentText.asChild();
                const currentTextIndex = parent.children.findIndex(x => x.equals(currentTextChild));
                if (currentTextIndex === -1) throw new ArgumentOutOfRangeException('index');
                const part1 = substring(currentText.text, 0, position2Relative);
                const part2 = substring(currentText.text, position2Relative);
                // The text's time stays with its beginning.
                parent.children[currentTextIndex] = new NodeText(part1, part1.length > 0 ? currentText.time : null).asChild();
                currentText = new NodeText(part2, part1.length > 0 ? null : currentText.time);
                currentPosition += part1.length;
                parent.children.splice(currentTextIndex + 1, 0,
                    new NodeHtml({
                        name: 'span',
                        classes: 'flying',
                        children: [
                            new NodeHtml({
                                name: 'span',
                                classes: 'flying2',
                                children: [
                                    node.asChild(),
                                ],
                            }).asChild(),
                        ],
                    }).asChild(),
                    currentText.asChild());
            }
        }

        this.children.length = 0;
        next._isDouble = true;
        this._isZero = true;
    }

    static readonly json: JsonContract<NodeLine> = jsonClass<NodeLine>('HarmonyDB.Common.Representations.OneShelf.NodeLine', init => new NodeLine({ children: init.children as NodeChild[] | undefined }), () => [
        jsonProperty('Children', () => jsonList(NodeChild.json)),
    ]);
}
