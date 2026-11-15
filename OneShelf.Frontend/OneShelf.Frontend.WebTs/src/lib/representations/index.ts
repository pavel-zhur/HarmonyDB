// HarmonyDB.Common.Representations.OneShelf
export {
    NodeBase, type Ref,
    NodeChild, type NodeChildInit,
    NodeCollectionBase, type TraversedNode,
    NodeText,
    NodeNote,
    NodeChord,
    NodeLine,
    NodeHtml, type NodeHtmlInit,
} from './internal';
export { Note } from './Note';
export { RepresentationSettings, type RepresentationSettingsConstructorArguments, type RepresentationSettingsMembers } from './RepresentationSettings';
export { SignFlat, SignSharp } from './SignsConstants';
export { SimplificationMode, hasFlag } from './SimplificationMode';
