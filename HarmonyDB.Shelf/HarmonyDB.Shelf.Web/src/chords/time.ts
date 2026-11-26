// The sheet's times, for the player (unit 15). A sheet of an analysis carries the time of its chords, words, lines and
// marks: in the rendered sheet, `data-time` (seconds from the start of the recording) on `b.chord`, on a word's
// `span`, on a `span.chords-line` and on a `span.sheet-mark`. A sheet of an old source has none; then nothing here
// does anything.
//
//   import { sheetTime } from '../../chords/time';
//   sheetTime.show(seconds);                  // the time now, told at every frame while the player follows: what
//                                             // sounds is lit in every sheet shown, by the sheet's own times — the
//                                             // chord (class `now`), its line (class `lnow`), the word being sung (the
//                                             // highlight `sung`); null: nothing lit
//   const stop = sheetTime.onSeek(seconds => player.seek(seconds));
//                                             // while the sheet follows the music (a time is shown), a tap on a timed
//                                             // chord or word tells its time; before that a tap only plays the
//                                             // chord's sound. stop() when the player goes
//   sheetTime.seeking()                       // whether anyone listens (the sheet shows its timed words as tappable)
//   const stop = sheetTime.onPlayFrom(seconds => …)
//                                             // a timed chord's "play from here" (its fingerings' window, playFrom
//                                             // below): offered while someone listens; stop() when the player goes

type Listener = (seconds: number) => void;

const seekListeners = new Set<Listener>();
const playFromListeners = new Set<Listener>();
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
    onPlayFrom(listener: Listener): () => void {
        playFromListeners.add(listener);
        return () => playFromListeners.delete(listener);
    },
};

/** Whether a timed chord can be played from (someone listens): its fingerings' window then offers "play from here". */
export function playsFrom(): boolean {
    return playFromListeners.size > 0;
}

/** internal: "play from here" on a timed chord. */
export function playFrom(seconds: number): void {
    playFromListeners.forEach(listener => listener(seconds));
}

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

/** The last of times (ascending) at or before t: its index, −1 when none. */
function lastAt(times: readonly number[], t: number): number {
    let lo = 0, hi = times.length - 1, found = -1;
    while (lo <= hi) {
        const m = (lo + hi) >> 1;
        if (times[m]! <= t) {
            found = m;
            lo = m + 1;
        } else hi = m - 1;
    }
    return found;
}

/**
 * internal: what sounds, lit in a rendered sheet. Its timed chords and words are read once; at each time told, only
 * what changed is touched. A word (a timed span, with the rest of it that a chord over it cut off, up to its space) is
 * lit as a highlight of its text, which leaves the sheet's own markup and wrapping as they are; where the browser has
 * no highlights, the words are not lit.
 */
export class SheetLight {
    private readonly chords: Element[];
    private readonly chordTimes: number[];
    private readonly words: Element[];
    private readonly wordTimes: number[];
    private chord = -1;
    private word = -1;

    constructor(private readonly element: HTMLElement) {
        this.chords = [...element.querySelectorAll('b.chord[data-time]')];
        this.chordTimes = this.chords.map(x => Number(x.getAttribute('data-time')));
        this.words = [...element.querySelectorAll('span[data-time]:not(.chords-line):not(.sheet-mark)')];
        this.wordTimes = this.words.map(x => Number(x.getAttribute('data-time')));
    }

    set(seconds: number | null): void {
        const chord = seconds === null ? -1 : lastAt(this.chordTimes, seconds);
        if (chord !== this.chord) {
            this.element.querySelectorAll('.now, .lnow').forEach(x => x.classList.remove('now', 'lnow'));
            const current = this.chords[chord];
            current?.classList.add('now');
            current?.closest('.chords-line')?.classList.add('lnow');
            this.chord = chord;
        }
        const word = seconds === null ? -1 : lastAt(this.wordTimes, seconds);
        if (word !== this.word) {
            this.word = word;
            sing(this.words[word] ?? null);
        }
    }
}

const sungName = 'sung';
const highlights = (globalThis as { CSS?: { highlights?: Map<string, unknown> } }).CSS?.highlights;
const HighlightOf = (globalThis as { Highlight?: new (...ranges: Range[]) => unknown }).Highlight;

/** The word being sung, as a highlight: its timed span and what follows it of the same word (outside the chords). */
function sing(word: Element | null): void {
    if (!highlights || !HighlightOf) return;
    if (!word) {
        highlights.delete(sungName);
        return;
    }
    const ranges: Range[] = [];
    const whole = document.createRange();
    whole.selectNodeContents(word);
    ranges.push(whole);
    // the rest of the word: the text after the span up to a space, stepping over a chord flying over it
    for (let node = word.nextSibling; node; node = node.nextSibling) {
        if (node.nodeType === Node.TEXT_NODE) {
            const text = node.textContent ?? '';
            const space = text.search(/\s/);
            const range = document.createRange();
            range.setStart(node, 0);
            range.setEnd(node, space < 0 ? text.length : space);
            if (!range.collapsed) ranges.push(range);
            if (space >= 0) break;
        } else if (node instanceof Element) {
            if (node.classList.contains('flying')) continue;
            break;
        }
    }
    highlights.set(sungName, new HighlightOf(...ranges));
}
