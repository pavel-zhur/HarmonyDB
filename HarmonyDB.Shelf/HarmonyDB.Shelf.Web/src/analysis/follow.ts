// The open view follows the music: when what is lit leaves the part of the page that is free to read — below the
// header, above the player at the bottom and its "to top" — it is brought back, a quarter of the way down that part, so
// that what comes next is in sight too. At once after a jump, smoothly while playing; not for a while after the person
// scrolls by hand (the wheel, a finger, the keys), so as not to pull the page from under them. In the text with chords
// the line that sounds is followed; in the other views, the bar.
//
//   useFollowLit(bodyRef)      bodyRef: the element the views are drawn in

import { useCallback, useEffect, useRef, type RefObject } from 'react';
import { useAnalysisClock } from './clock';

/** After a scroll by hand, the view is not pulled back for this long. */
const HOLD_MS = 2500;
/** A change of place further than this is a jump (a tap, a seek), followed at once. */
const JUMP_SECONDS = 1;
/** What the views light as what sounds: the text's line, a bar. */
const LIT = '.chords-line.lnow, .an-gbar.now, .ls-barbg.now, .an-irb.now, .an-smeasure.now';
/** Room kept free above the player for its "to top" button, and below the top edge. */
const ROOM_BELOW = 56, ROOM_ABOVE = 12;

/** The element that scrolls the view: the nearest ancestor that scrolls up and down, else the page. */
function scrollerOf(element: Element): Element {
    for (let e = element.parentElement; e; e = e.parentElement) {
        const y = getComputedStyle(e).overflowY;
        if ((y === 'auto' || y === 'scroll') && e.scrollHeight > e.clientHeight) return e;
    }
    return document.scrollingElement ?? document.documentElement;
}

/** The part of the screen free to read, top and bottom, in the viewport's pixels. */
function freeBand(scroller: Element): { top: number; bottom: number } {
    const page = scroller === document.scrollingElement;
    const box = page ? { top: 0, bottom: window.innerHeight } : scroller.getBoundingClientRect();
    let bottom = box.bottom;
    // the player stands at the bottom of the page over what scrolls
    const bar = document.querySelector('.pl-bar');
    if (bar) {
        const r = bar.getBoundingClientRect();
        if (r.height && r.top < bottom && r.bottom > box.top) bottom = Math.min(bottom, r.top);
    }
    return { top: box.top + ROOM_ABOVE, bottom: bottom - ROOM_BELOW };
}

export function useFollowLit(body: RefObject<HTMLElement | null>): void {
    const followed = useRef<Element | null>(null);
    const holdUntil = useRef(0);
    const last = useRef(0);

    useEffect(() => {
        const hold = () => { holdUntil.current = performance.now() + HOLD_MS; };
        const key = (event: KeyboardEvent) => { if (/^(Arrow|Page|Home|End|Space)/.test(event.code)) hold(); };
        window.addEventListener('wheel', hold, { passive: true });
        window.addEventListener('touchmove', hold, { passive: true });
        window.addEventListener('keydown', key);
        return () => {
            window.removeEventListener('wheel', hold);
            window.removeEventListener('touchmove', hold);
            window.removeEventListener('keydown', key);
        };
    }, []);

    const follow = useCallback((position: number, playing: boolean) => {
        const jumped = Math.abs(position - last.current) > JUMP_SECONDS;
        last.current = position;
        const now = body.current?.querySelector(LIT) ?? null;
        if (now === followed.current) return;
        followed.current = now;
        if (!now || !(jumped || (playing && performance.now() > holdUntil.current))) return;
        const scroller = scrollerOf(now), band = freeBand(scroller), r = now.getBoundingClientRect();
        if (r.top >= band.top && r.bottom <= band.bottom) return;
        const by = r.top - (band.top + (band.bottom - band.top) / 4);
        scroller.scrollBy({ top: by, behavior: jumped ? 'auto' : 'smooth' });
    }, [body]);
    useAnalysisClock(follow);
}
