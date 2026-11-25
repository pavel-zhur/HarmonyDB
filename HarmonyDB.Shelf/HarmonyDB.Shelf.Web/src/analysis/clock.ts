// The views' clock and their taps. On a song's page another recording of the song may be chosen to listen to while an
// analysis is open: then its views light nothing (their times are the analysis's recording's), and a tap in them —
// "play from here", a loop — chooses the analysis's recording again and goes there once it is open.
//
//   useAnalysisClock(onTime)            useClock for a view: (position, playing), or (-1, false) while another plays
//   analysisPlayer.playFrom(t) / setLoop(a, b)
//   offerAnalysis(choose | null)        the song's player, while another recording plays: how to choose the analysis
//   takePending()                       the song's player, once the analysis is open: what a tap asked for meanwhile

import { useCallback } from 'react';
import { player, useClock } from '../player/controller';

let chooseAnalysis: (() => void) | null = null;
let pending: (() => void) | null = null;

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
};

export function offerAnalysis(choose: (() => void) | null): void {
    chooseAnalysis = choose;
}

export function takePending(): (() => void) | null {
    const run = pending;
    pending = null;
    return run;
}
