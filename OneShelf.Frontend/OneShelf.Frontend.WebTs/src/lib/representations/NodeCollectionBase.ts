import { ArgumentOutOfRangeException } from '../common/dotnet/exceptions';
import type { StringBuilder } from '../common/dotnet/StringBuilder';
import type { RepresentationSettings } from './RepresentationSettings';
import type { TraversalContext } from './TraversalContext';
import type { NodeChild, NodeChord, Ref } from './internal';
import { NodeBase } from './NodeBase';
import { nodeTypes } from './nodeTypes';

/** An element of TraverseAll: the node and its parents, the nearest first. */
export interface TraversedNode {
    readonly element: NodeBase;
    readonly parentsReverse: readonly NodeCollectionBase[];
}

export abstract class NodeCollectionBase extends NodeBase {
    readonly children: NodeChild[];

    /** `Children = ...` from an initializer or the JSON; the C# default is an empty list. */
    protected constructor(children?: NodeChild[]) {
        super();
        this.children = children !== undefined ? children : [];
    }

    traverseAllChords(): NodeChord[] {
        return this.children.flatMap(x => {
            const node = x.asNode();
            return node instanceof nodeTypes.NodeChord ? [node]
                : node instanceof NodeCollectionBase ? node.traverseAllChords()
                    : [];
        });
    }

    traverseAll(parents: readonly NodeCollectionBase[] = []): TraversedNode[] {
        const result: TraversedNode[] = [{ element: this, parentsReverse: parents }];
        const parentsForChild = [this, ...parents];
        for (const x of this.children) {
            const node = x.asNode();
            if (node instanceof NodeCollectionBase) {
                result.push(...node.traverseAll(parentsForChild));
            } else {
                result.push({ element: node, parentsReverse: parentsForChild });
            }
        }
        return result;
    }

    override appendHtmlTo(builder: StringBuilder, representationSettings: RepresentationSettings,
        traversalContext: TraversalContext): void {
        this.children.forEach(x => x.asNode().appendHtmlTo(builder, representationSettings, traversalContext));
    }

    override appendChordsTo(destination: string[], representationSettings: RepresentationSettings): void {
        this.children.forEach(x => x.asNode().appendChordsTo(destination, representationSettings));
    }

    override appendLyricsTo(builder: StringBuilder): void {
        this.children.forEach(x => x.asNode().appendLyricsTo(builder));
    }

    override appendHtmlAttributeTo(builder: StringBuilder, representationSettings: RepresentationSettings): void {
        this.children.forEach(x => x.asNode().appendHtmlAttributeTo(builder, representationSettings));
    }

    override appendOneShelfTo(builder: StringBuilder, representationSettings: RepresentationSettings,
        isBold: boolean, owesWhitespace: Ref<boolean>, alreadyChordLine: boolean): void {
        for (const child of this.children) {
            child.asNode().appendOneShelfTo(builder, representationSettings, isBold, owesWhitespace, alreadyChordLine);
        }
    }

    override fix1_ExtractChordNotes(representationSettings: RepresentationSettings,
        _parent: { parent: NodeCollectionBase; myIndex: number } | null = null): void {
        for (let i = 0; i < this.children.length; i++) { // may not convert to foreach because the collection is modified inside the loop
            const node = this.children[i]!;
            node.asNode().fix1_ExtractChordNotes(representationSettings, { parent: this, myIndex: i });
        }
    }

    override fix2_ExtractLines(): void {
        const newChildren: NodeChild[] = [];
        for (const child of this.children) {
            const node = child.asNode();
            if (node instanceof nodeTypes.NodeText) {
                const split = node.text.split('\n');
                newChildren.push(...split
                    .flatMap((x, i): NodeBase[] => i === 0 ? [new nodeTypes.NodeText(x)] : [new nodeTypes.NodeLine(), new nodeTypes.NodeText(x)])
                    .map(x => x.asChild()));
            } else {
                newChildren.push(child);
            }
        }

        this.children.length = 0;
        this.children.push(...newChildren);
    }

    fix3_AssignBlockIds(blockId: number | null = null): void {
        this.blockId = blockId;
        for (const child of this.children) {
            const node = child.asNode();
            if (node instanceof NodeCollectionBase) {
                node.fix3_AssignBlockIds(node instanceof nodeTypes.NodeHtml && node.isBlock ? Math.random() : blockId);
            } else {
                node.blockId = blockId;
            }
        }
    }

    deepClone(): NodeCollectionBase {
        const clone = this.shallowClone() as NodeCollectionBase;
        for (const nodeChild of this.children) {
            const node = nodeChild.asNode();
            if (node instanceof NodeCollectionBase) {
                clone.children.push(node.deepClone().asChild());
            } else {
                clone.children.push(node.shallowClone().asChild());
            }
        }

        return clone;
    }

    cloneFixingNewLines(): { clone: NodeCollectionBase; completed: boolean } {
        const myClone = this.shallowClone() as NodeCollectionBase;

        let completedCount = 0;
        let completed = true;
        for (const child of this.children) {
            const node = child.asNode();
            if (node instanceof nodeTypes.NodeLine) {
                completedCount++;
                completed = false;
                break;
            } else if (node instanceof NodeCollectionBase) {
                const childClone = node.cloneFixingNewLines();
                myClone.children.push(childClone.clone.asChild());
                if (childClone.completed) {
                    completedCount++;
                } else {
                    completed = false;
                    break;
                }
            } else if (node instanceof nodeTypes.NodeNote || node instanceof nodeTypes.NodeText) {
                myClone.children.push(node.shallowClone().asChild());
                completedCount++;
            } else {
                throw new ArgumentOutOfRangeException('child');
            }
        }

        this.children.splice(0, completedCount);
        return { clone: myClone, completed };
    }
}
