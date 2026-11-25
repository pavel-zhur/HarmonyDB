// An analysis and its piece, read once for everything on the page that shows it (the views, the player).

import { useAnalysis, type Timeline } from '../data/analysis';
import { pieceOf, type Piece } from './piece';

const pieces = new WeakMap<Timeline, Piece>();

function pieceFor(timeline: Timeline): Piece {
    let piece = pieces.get(timeline);
    if (!piece) {
        piece = pieceOf(timeline);
        pieces.set(timeline, piece);
    }
    return piece;
}

export function useAnalysisPiece(chordsId: string | null, share?: string | null) {
    const analysis = useAnalysis(chordsId, share);
    const timeline = analysis.data?.content?.timeline ?? null;
    return { analysis, piece: timeline ? pieceFor(timeline) : null };
}
