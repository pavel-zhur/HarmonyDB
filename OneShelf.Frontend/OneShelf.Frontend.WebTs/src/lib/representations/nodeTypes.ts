// The derived node classes as the two base classes (NodeBase, NodeCollectionBase) reach them: through this object,
// filled by internal.ts, and only inside methods. A base class module then imports nothing that extends it, so it can
// be evaluated first whichever node module is loaded first (in ES modules a class can't extend one not yet evaluated).

import type { NodeChild, NodeChord, NodeHtml, NodeLine, NodeNote, NodeText } from './internal';

export const nodeTypes = {} as {
    readonly NodeChild: typeof NodeChild;
    readonly NodeChord: typeof NodeChord;
    readonly NodeHtml: typeof NodeHtml;
    readonly NodeLine: typeof NodeLine;
    readonly NodeNote: typeof NodeNote;
    readonly NodeText: typeof NodeText;
};
