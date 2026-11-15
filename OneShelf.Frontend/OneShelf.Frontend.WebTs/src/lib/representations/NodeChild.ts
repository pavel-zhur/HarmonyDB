import { ArgumentOutOfRangeException } from '../common/dotnet/exceptions';
import { jsonClass, jsonProperty, type JsonContract } from '../common/dotnet/json/JsonSerializer';
import { NodeBase } from './NodeBase';
import { NodeChord, NodeHtml, NodeLine, NodeNote, NodeText } from './internal';

export interface NodeChildInit {
    nodeChord?: NodeChord | null;
    nodeText?: NodeText | null;
    nodeNote?: NodeNote | null;
    nodeHtml?: NodeHtml | null;
    nodeLine?: NodeLine | null;
}

/** A record: equal when the same nodes (by reference, the node classes have no Equals) are in the same members. */
export class NodeChild {
    readonly nodeChord: NodeChord | null = null;
    readonly nodeText: NodeText | null = null;
    readonly nodeNote: NodeNote | null = null;
    readonly nodeHtml: NodeHtml | null = null;
    readonly nodeLine: NodeLine | null = null;

    /** `new NodeChild(node)`, or `new NodeChild { ... }` with an init object. */
    constructor(node?: NodeBase | NodeChildInit) {
        if (node === undefined) return;
        if (!(node instanceof NodeBase)) {
            this.nodeChord = node.nodeChord ?? null;
            this.nodeText = node.nodeText ?? null;
            this.nodeNote = node.nodeNote ?? null;
            this.nodeHtml = node.nodeHtml ?? null;
            this.nodeLine = node.nodeLine ?? null;
            return;
        }

        if (node instanceof NodeChord) {
            this.nodeChord = node;
        } else if (node instanceof NodeHtml) {
            this.nodeHtml = node;
        } else if (node instanceof NodeLine) {
            this.nodeLine = node;
        } else if (node instanceof NodeNote) {
            this.nodeNote = node;
        } else if (node instanceof NodeText) {
            this.nodeText = node;
        } else {
            throw new ArgumentOutOfRangeException('node');
        }
    }

    asNode(): NodeBase {
        return (this.nodeChord ?? this.nodeText ?? this.nodeNote ?? this.nodeHtml ?? this.nodeLine)!;
    }

    equals(other: NodeChild | null | undefined): boolean {
        return other != null
            && this.nodeChord === other.nodeChord
            && this.nodeText === other.nodeText
            && this.nodeNote === other.nodeNote
            && this.nodeHtml === other.nodeHtml
            && this.nodeLine === other.nodeLine;
    }

    static readonly json: JsonContract<NodeChild> = jsonClass<NodeChild>('HarmonyDB.Common.Representations.OneShelf.NodeChild', init => new NodeChild(init as NodeChildInit), () => [
        jsonProperty('NodeChord', () => NodeChord.json, { ignoreWhenWritingNull: true }),
        jsonProperty('NodeText', () => NodeText.json, { ignoreWhenWritingNull: true }),
        jsonProperty('NodeNote', () => NodeNote.json, { ignoreWhenWritingNull: true }),
        jsonProperty('NodeHtml', () => NodeHtml.json, { ignoreWhenWritingNull: true }),
        jsonProperty('NodeLine', () => NodeLine.json, { ignoreWhenWritingNull: true }),
    ]);
}
