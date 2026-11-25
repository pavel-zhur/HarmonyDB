import { jsonClass, jsonList, jsonProperty, jsonString, type JsonContract } from '../common/dotnet/json/JsonSerializer';
import { NewLine, StringBuilder } from '../common/dotnet/StringBuilder';
import { isNullOrWhiteSpace, split, StringSplitOptions } from '../common/dotnet/text';
import { RepresentationSettings } from './RepresentationSettings';
import { TraversalContext } from './TraversalContext';
import type { NodeBase, Ref } from './NodeBase';
import { NodeCollectionBase } from './NodeCollectionBase';
import { SheetKey } from './SheetKey';
import { SheetTimes } from './SheetTimes';
import { NodeChild, NodeChord, NodeLine, NodeMark, NodeNote, NodeText } from './internal';

export interface NodeHtmlInit {
    name: string;
    classes?: string | null;
    keys?: readonly SheetKey[] | null;
    times?: SheetTimes | null;
    children?: NodeChild[];
}

export class NodeHtml extends NodeCollectionBase {
    static readonly PreVariableWidthClass = 'pre-variable-width';

    readonly name: string;

    readonly classes: string | null;

    /**
     * On the sheet's root: the song's keys, when known, in the order they come (the first is the key the song begins in).
     * The places where the key changes are marks in the text (NodeMark).
     */
    readonly keys: readonly SheetKey[] | null;

    /**
     * On the sheet's root: the sheet's own bars, when it has them. Its chords and marks may then stand at places in them
     * (NodeChord.place, NodeMark.place) instead of at seconds.
     */
    readonly times: SheetTimes | null;

    /** [JsonIgnore] */
    isBlock = false;

    /** `new NodeHtml { Name = ..., Classes = ..., Children = ... }` */
    constructor(init: NodeHtmlInit) {
        super(init.children);
        this.name = init.name;
        this.classes = init.classes ?? null;
        this.keys = init.keys ?? null;
        this.times = init.times ?? null;
    }

    /** [JsonIgnore] */
    get isVariableWidth(): boolean {
        return this.classes === NodeHtml.PreVariableWidthClass;
    }

    getAllChords(representationSettings: RepresentationSettings): NodeChord[] {
        return this.createFinal(representationSettings).traverseAllChords();
    }

    asLyrics(trim = true): string {
        const final = this.createFinal(new RepresentationSettings());
        const builder = new StringBuilder();
        final.appendLyricsTo(builder);
        if (trim) {
            return split(builder.toString(), ['\r', '\n'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).join(NewLine);
        }

        return builder.toString();
    }

    asHtml(representationSettings: RepresentationSettings): string {
        const final = this.createFinal(representationSettings);
        const builder = new StringBuilder();
        const traversalContext = new TraversalContext();
        traversalContext.times = this.times;
        final.appendHtmlTo(builder, representationSettings, traversalContext);
        return builder.toString();
    }

    asChords(representationSettings: RepresentationSettings): string[] {
        const final = this.createFinal(representationSettings, false, false);
        const destination: string[] = [];
        final.appendChordsTo(destination, representationSettings);
        return destination;
    }

    asOneShelf(representationSettings: RepresentationSettings): string {
        const final = this.createFinal(representationSettings, false);
        const builder = new StringBuilder();

        for (const line of final.children.map(c => c.nodeLine!)) {
            // A line of marks alone is not in the text form.
            if (line.traverseAll().some(x => x.element instanceof NodeMark)
                && line.traverseAll().slice(1).every(x => x.element instanceof NodeMark || x.element instanceof NodeText && isNullOrWhiteSpace(x.element.text)))
                continue;

            const owesWhitespace: Ref<boolean> = { value: false };
            if (line.traverseAll().slice(1).every(x =>
                    x.element instanceof NodeText && (isNullOrWhiteSpace(x.element.text) || x.parentsReverse.some(p => p instanceof NodeChord))
                    || x.element instanceof NodeChord
                    || x.element instanceof NodeNote)
                && line.traverseAll().some(x => x.element instanceof NodeChord)) {
                builder.append('>');
                owesWhitespace.value = true;
                line.appendOneShelfTo(builder, representationSettings, false, owesWhitespace, true);
            } else if (line.traverseAll().slice(1).every(x => !(x.element instanceof NodeChord || x.element instanceof NodeLine))
                && line.children.length === 1
                && line.children[0]!.nodeHtml != null
                && (line.children[0]!.nodeHtml.name === 'b'
                    || line.children[0]!.nodeHtml.name === 'div' && line.children[0]!.nodeHtml.classes === 'bold-div')) {
                builder.append('[');
                owesWhitespace.value = true;
                for (const lineChild of line.children) {
                    lineChild.asNode().appendOneShelfTo(builder, representationSettings, false, owesWhitespace, false);
                    builder.appendLine(']');
                }
            } else {
                line.appendOneShelfTo(builder, representationSettings, false, owesWhitespace, false);
            }
        }

        return builder.toString();
    }

    private createFinal(representationSettings: RepresentationSettings, create5DoubleLines = true, create234 = true): NodeHtml {
        let final = this.deepClone() as NodeHtml;
        final.fix1_ExtractChordNotes(representationSettings);

        if (create234) {
            final.fix2_ExtractLines();
            final.fix3_AssignBlockIds();
            final = final.fix4_NewLines();

            if (create5DoubleLines) {
                final.fix5_CreateDoubleLines(representationSettings);
            }
        }

        return final;
    }

    private fix4_NewLines(): NodeHtml {
        const result = this.shallowClone() as NodeHtml;

        while (true) {
            const { clone, completed } = this.cloneFixingNewLines();
            const nodeLine = new NodeLine();
            nodeLine.children.push(...clone.children);
            result.children.push(nodeLine.asChild());

            if (completed) break;
        }

        return result;
    }

    private fix5_CreateDoubleLines(representationSettings: RepresentationSettings): void {
        const lines = this.children.map(x => x.asNode() as NodeLine);
        for (let i = 0; i < lines.length - 1; i++) {
            lines[i]!.tryInjectTo(lines[i + 1]!, representationSettings);
        }
    }

    override appendHtmlTo(builder: StringBuilder, representationSettings: RepresentationSettings,
        traversalContext: TraversalContext): void {
        if (!isNullOrWhiteSpace(this.classes)) {
            builder.append('<');
            builder.append(this.name);
            builder.append(' class=\'');
            builder.append(this.classes);
            builder.append('\'>');
        } else {
            builder.append('<');
            builder.append(this.name);
            builder.append('>');
        }

        super.appendHtmlTo(builder, representationSettings, traversalContext);

        builder.append('</');
        builder.append(this.name);
        builder.append('>');
    }

    override shallowClone(): NodeBase {
        return new NodeHtml({
            name: this.name,
            classes: this.classes,
            keys: this.keys,
            times: this.times,
        });
    }

    /** The song's keys as the sheet is shown: transposed with it, named as its chords are (SheetKey.shown). */
    getKeys(representationSettings: RepresentationSettings): SheetKey[] | null {
        return this.keys?.map(x => x.shown(representationSettings)) ?? null;
    }

    getChordsCount(): number | null {
        return this.traverseAll().filter(x => x.element instanceof NodeChord).length;
    }

    getNodesCount(): number | null {
        return this.traverseAll().length;
    }

    fix0_Compress(): void {
        for (const nodeCollectionBase of this.traverseAll().map(x => x.element).filter((x): x is NodeCollectionBase => x instanceof NodeCollectionBase)) {
            // The neighbouring texts are joined, unless the later one has its own time (a word heard at its time).
            const newChildren: NodeChild[] = [];
            for (const child of nodeCollectionBase.children) {
                const text = child.nodeText;
                const previous = newChildren.length > 0 ? newChildren[newChildren.length - 1]!.nodeText : null;
                if (text !== null && text.time === null && previous !== null) {
                    newChildren[newChildren.length - 1] = new NodeText(previous.text + text.text, previous.time).asChild();
                } else {
                    newChildren.push(child);
                }
            }

            if (newChildren.length !== nodeCollectionBase.children.length) {
                nodeCollectionBase.children.length = 0;
                nodeCollectionBase.children.push(...newChildren);
            }
        }
    }

    static readonly json: JsonContract<NodeHtml> = jsonClass<NodeHtml>('HarmonyDB.Common.Representations.OneShelf.NodeHtml', init => new NodeHtml({
        name: init.name as string,
        classes: init.classes !== undefined ? init.classes as string | null : null,
        keys: init.keys as SheetKey[] | null | undefined,
        times: init.times as SheetTimes | null | undefined,
        children: init.children as NodeChild[] | undefined,
    }), () => [
        jsonProperty('Name', jsonString, { required: true }),
        jsonProperty('Classes', jsonString),
        jsonProperty('Keys', () => jsonList(SheetKey.json), { ignoreWhenWritingNull: true }),
        jsonProperty('Times', () => SheetTimes.json, { ignoreWhenWritingNull: true }),
        jsonProperty('Children', () => jsonList(NodeChild.json)),
    ]);
}
