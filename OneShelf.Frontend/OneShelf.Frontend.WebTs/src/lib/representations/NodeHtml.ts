import { jsonClass, jsonList, jsonProperty, jsonString, type JsonContract } from '../common/dotnet/json/JsonSerializer';
import { NewLine, StringBuilder } from '../common/dotnet/StringBuilder';
import { isNullOrWhiteSpace, split, StringSplitOptions } from '../common/dotnet/text';
import { toChunks } from '../common/OneShelf.Common/Extensions';
import { RepresentationSettings } from './RepresentationSettings';
import { TraversalContext } from './TraversalContext';
import type { NodeBase, Ref } from './NodeBase';
import { NodeCollectionBase } from './NodeCollectionBase';
import { NodeChild, NodeChord, NodeLine, NodeNote, NodeText } from './internal';

export interface NodeHtmlInit {
    name: string;
    classes?: string | null;
    children?: NodeChild[];
}

export class NodeHtml extends NodeCollectionBase {
    static readonly PreVariableWidthClass = 'pre-variable-width';

    readonly name: string;

    readonly classes: string | null;

    /** [JsonIgnore] */
    isBlock = false;

    /** `new NodeHtml { Name = ..., Classes = ..., Children = ... }` */
    constructor(init: NodeHtmlInit) {
        super(init.children);
        this.name = init.name;
        this.classes = init.classes ?? null;
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
        final.appendHtmlTo(builder, representationSettings, new TraversalContext());
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
        });
    }

    getChordsCount(): number | null {
        return this.traverseAll().filter(x => x.element instanceof NodeChord).length;
    }

    getNodesCount(): number | null {
        return this.traverseAll().length;
    }

    fix0_Compress(): void {
        for (const nodeCollectionBase of this.traverseAll().map(x => x.element).filter((x): x is NodeCollectionBase => x instanceof NodeCollectionBase)) {
            const newChildren = toChunks(nodeCollectionBase.children, x => x.nodeText != null)
                .flatMap(c =>
                    !c.criterium
                        ? c.chunk
                        : [new NodeText(c.chunk.map(x => x.nodeText!.text).join('')).asChild()]);

            if (newChildren.length !== nodeCollectionBase.children.length) {
                nodeCollectionBase.children.length = 0;
                nodeCollectionBase.children.push(...newChildren);
            }
        }
    }

    static readonly json: JsonContract<NodeHtml> = jsonClass<NodeHtml>('HarmonyDB.Common.Representations.OneShelf.NodeHtml', init => new NodeHtml({
        name: init.name as string,
        classes: init.classes !== undefined ? init.classes as string | null : null,
        children: init.children as NodeChild[] | undefined,
    }), () => [
        jsonProperty('Name', jsonString, { required: true }),
        jsonProperty('Classes', jsonString),
        jsonProperty('Children', () => jsonList(NodeChild.json)),
    ]);
}
