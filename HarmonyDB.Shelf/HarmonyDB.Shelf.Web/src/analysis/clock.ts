// The views' clock and their taps. On a song's page another recording of the song may be chosen to listen to while an
// analysis is open: then its views light nothing (their times are the analysis's recording's) and say so, with the way
// back; a tap in them — "play from here", a loop — chooses the analysis's recording again and goes there once it is
// open. Coming back otherwise, the analysis's recording goes on from where it was left.
//
//   useAnalysisClock(onTime)            useClock for a view: (position, playing), or (-1, false) while another plays
//   analysisPlayer.playFrom(t) / setLoop(a, b)
//   analysisPlayer.back(chordsId)       the analysis's recording again, from where it was left
//   useAnotherPlays()                   whether another recording of the song plays now, over the open analysis
//   offerAnalysis(choose | null)        the song's player, while another recording plays: how to choose the analysis
//   takePending()                       the song's player, once the analysis is open: what a tap asked for meanwhile
//   keepPlace(chordsId, t) / keptPlace(chordsId)   where an analysis's recording was left (0: never)

import { useCallback, useSyncExternalStore } from 'react';
import { player, useClock, usePlayer } from '../player/controller';

let chooseAnalysis: (() => void) | null = null;
let pending: (() => void) | null = null;
const offerListeners = new Set<() => void>();
const places = new Map<string, number>();

export function keepPlace(chordsId: string, at: number): void {
    places.set(chordsId, at);
}

export function keptPlace(chordsId: string | null): number {
    return chordsId ? places.get(chordsId) ?? 0 : 0;
}

export function useAnotherPlays(): boolean {
    const offered = useSyncExternalStore(listener => {
        offerListeners.add(listener);
        return () => offerListeners.delete(listener);
    }, () => chooseAnalysis !== null);
    const source = usePlayer().source;
    return offered && !!source && source.kind !== 'analysis';
}

/** Whether what plays is an analysis's recording (the page's: there is one at a time). */
const analysisPlays = () => player.getState().source?.kind === 'analysis';

export function useAnalysisClock(onTime: (position: number, playing: boolean) => void): void {
    useClock(useCallback((position: number, playing: boolean) => {
        if (analysisPlays()) onTime(position, playing);
        else onTime(-1, false);
    }, [onTime]));
}

function act(run: () => void): void {
    if (analysisPlays() || !chooseAnalysis) {
        run();
        return;
    }
    pending = run;
    chooseAnalysis();
}

export const analysisPlayer = {
    playFrom: (time: number) => act(() => player.playFrom(time)),
    setLoop: (start: number, end: number) => act(() => player.setLoop(start, end)),
    back: (chordsId: string) => {
        if (analysisPlays() || !chooseAnalysis) return;
        player.playChosen(keptPlace(chordsId));
        chooseAnalysis();
    },
};

export function offerAnalysis(choose: (() => void) | null): void {
    chooseAnalysis = choose;
    for (const listener of offerListeners) listener();
}

export function takePending(): (() => void) | null {
    const run = pending;
    pending = null;
    return run;
}
