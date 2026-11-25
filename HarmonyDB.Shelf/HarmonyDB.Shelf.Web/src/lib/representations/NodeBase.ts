import type { StringBuilder } from '../common/dotnet/StringBuilder';
import type { RepresentationSettings } from './RepresentationSettings';
import type { SheetPlace, SheetTimes } from './SheetTimes';
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

    /**
     * internal: the moment of this node, in seconds: its time, or its place in the sheet's bars; none (null) when it has neither,
     * or when its place is not in the bars (or the sheet has none). Every time the rendering reads is read here.
     */
    moment(_times: SheetTimes | null): number | null {
        return null;
    }

    /** internal: the moment of a node that has a time or a place (moment). A node with both has no time: the two may disagree. */
    static momentOf(time: number | null, place: SheetPlace | null, times: SheetTimes | null): number | null {
        if (time !== null && place !== null) return null;

        return time ?? (place !== null ? times?.moment(place) ?? null : null);
    }

    asChild(): NodeChild {
        return new nodeTypes.NodeChild(this);
    }
}
