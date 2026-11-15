import type { StringBuilder } from '../common/dotnet/StringBuilder';
import type { RepresentationSettings } from './RepresentationSettings';
import type { TraversalContext } from './TraversalContext';
import type { NodeChild, NodeCollectionBase } from './internal';
import { nodeTypes } from './nodeTypes';

/** `ref bool` */
export interface Ref<T> {
    value: T;
}

export abstract class NodeBase {
    /** [JsonIgnore] */
    blockId: number | null = null;

    /** internal */
    abstract appendHtmlTo(builder: StringBuilder, representationSettings: RepresentationSettings, traversalContext: TraversalContext): void;

    /** internal */
    abstract appendChordsTo(destination: string[], representationSettings: RepresentationSettings): void;

    /** internal */
    abstract appendLyricsTo(builder: StringBuilder): void;

    /** internal */
    abstract appendHtmlAttributeTo(builder: StringBuilder, representationSettings: RepresentationSettings): void;

    /** internal */
    abstract appendOneShelfTo(builder: StringBuilder, representationSettings: RepresentationSettings,
        isBold: boolean, owesWhitespace: Ref<boolean>, alreadyChordLine: boolean): void;

    fix1_ExtractChordNotes(_representationSettings: RepresentationSettings,
        _parent: { parent: NodeCollectionBase; myIndex: number } | null = null): void {
    }

    fix2_ExtractLines(): void {
    }

    abstract shallowClone(): NodeBase;

    asChild(): NodeChild {
        return new nodeTypes.NodeChild(this);
    }
}
