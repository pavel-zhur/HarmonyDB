import type { NodeChord } from './internal';
import type { SheetTimes } from './SheetTimes';

/** internal */
export class TraversalContext {
    isInsideChord = false;

    siblingChords: NodeChord[] | null = null;

    chordIndex = 0;

    /** The sheet's bars, when it has them: the places of its chords and marks are in them. (`init`) */
    times: SheetTimes | null = null;
}
