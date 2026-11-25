import type { NodeChord } from './internal';

/** internal */
export class TraversalContext {
    isInsideChord = false;

    siblingChords: NodeChord[] | null = null;

    chordIndex = 0;
}
