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
    NodeMark,
} from './internal';
export { SheetKey } from './SheetKey';
export { SheetTempo } from './SheetTempo';
export { SheetTimeSignature } from './SheetTimeSignature';
export { SheetBar, SheetPlace, SheetTimes } from './SheetTimes';
export { Note } from './Note';
export { RepresentationSettings, type RepresentationSettingsConstructorArguments, type RepresentationSettingsInit, type RepresentationSettingsMembers } from './RepresentationSettings';
export { SignFlat, SignSharp } from './SignsConstants';
export { SimplificationMode, hasFlag } from './SimplificationMode';
