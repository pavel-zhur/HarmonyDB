// The node classes refer to each other. A derived class imports its base class directly and its siblings (used only
// inside methods) through this file; the base classes reach the derived ones through `nodeTypes`, filled here, so a
// base module never imports what extends it. Any node module may then be imported first, in ES modules and in
// Vitest's module runner alike.

import { NodeBase, type Ref } from './NodeBase';
import { NodeChild, type NodeChildInit } from './NodeChild';
import { NodeCollectionBase, type TraversedNode } from './NodeCollectionBase';
import { NodeText } from './NodeText';
import { NodeNote } from './NodeNote';
import { NodeChord } from './NodeChord';
import { NodeLine } from './NodeLine';
import { NodeHtml, type NodeHtmlInit } from './NodeHtml';
import { nodeTypes } from './nodeTypes';

export { NodeBase, type Ref, NodeChild, type NodeChildInit, NodeCollectionBase, type TraversedNode, NodeText, NodeNote, NodeChord, NodeLine, NodeHtml, type NodeHtmlInit };

// getters: when a derived module is the first one loaded, it is not yet evaluated at this point
Object.defineProperties(nodeTypes, {
    NodeChild: { get: () => NodeChild },
    NodeChord: { get: () => NodeChord },
    NodeHtml: { get: () => NodeHtml },
    NodeLine: { get: () => NodeLine },
    NodeNote: { get: () => NodeNote },
    NodeText: { get: () => NodeText },
});
