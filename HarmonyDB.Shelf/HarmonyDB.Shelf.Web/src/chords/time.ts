// The sheet's times, for the player (unit 15). A sheet of an analysis carries the time of its chords, words, lines and
// marks: in the rendered sheet, `data-time` (seconds from the start of the recording) on `b.chord`, on a word's
// `span`, on a `span.chords-line` and on a `span.sheet-mark`. A sheet of an old source has none; then nothing here
// does anything.
//
//   import { sheetTime } from '../../chords/time';
//   sheetTime.show(seconds);                  // what sounds now is lit in every sheet shown: the chord (class `now`)
//                                             // and its line (class `lnow`); null: nothing lit
//   const stop = sheetTime.onSeek(seconds => player.seek(seconds));
//                                             // while the sheet follows the music (a time is shown), a tap on a timed
//                                             // chord or word tells its time; before that a tap only plays the
//                                             // chord's sound. stop() when the player goes
//   sheetTime.seeking()                       // whether anyone listens (the sheet shows its timed words as tappable)

type Listener = (seconds: number) => void;

const seekListeners = new Set<Listener>();
const showListeners = new Set<(seconds: number | null) => void>();
let now: number | null = null;

export const sheetTime = {
    show(seconds: number | null): void {
        now = seconds;
        showListeners.forEach(listener => listener(seconds));
    },
    onSeek(listener: Listener): () => void {
        seekListeners.add(listener);
        return () => seekListeners.delete(listener);
    },
    seeking(): boolean {
        return seekListeners.size > 0;
    },
};

/** internal: the sheet asks to go to a time. */
export function seek(seconds: number): void {
    // the recording is gone to only when it is the thing in use: the player has shown a time
    if (now === null) return;
    seekListeners.forEach(listener => listener(seconds));
}

/** internal: a sheet follows what sounds; it is told at once of the time now. */
export function followTime(listener: (seconds: number | null) => void): () => void {
    showListeners.add(listener);
    listener(now);
    return () => showListeners.delete(listener);
}

/** internal: lights in a rendered sheet the chord sounding at this time and its line. */
export function light(element: HTMLElement, seconds: number | null): void {
    element.querySelectorAll('.now, .lnow').forEach(x => x.classList.remove('now', 'lnow'));
    if (seconds === null) return;
    let current: Element | null = null;
    for (const chord of element.querySelectorAll('b.chord[data-time]')) {
        if (Number(chord.getAttribute('data-time')) <= seconds) current = chord;
        else break;
    }
    if (!current) return;
    current.classList.add('now');
    current.closest('.chords-line')?.classList.add('lnow');
}
