// The open view follows the music: when another bar is lit, it is brought into sight — at once after a jump, smoothly
// while playing; not for a while after the person scrolls by hand (the wheel, a finger, the keys), so as not to pull
// the page from under them.
//
//   useFollowLit(bodyRef)      bodyRef: the element the views are drawn in

import { useCallback, useEffect, useRef, type RefObject } from 'react';
import { useClock } from '../player/controller';

/** After a scroll by hand, the view is not pulled back for this long. */
const HOLD_MS = 2500;
/** A change of place further than this is a jump (a tap, a seek), followed at once. */
const JUMP_SECONDS = 1;
/** What the views light as the bar that sounds. */
const LIT = '.an-gbar.now, .ls-barbg.now, .an-irb.now, .an-smeasure.now';

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
        if (now && (jumped || (playing && performance.now() > holdUntil.current)))
            now.scrollIntoView({ block: 'nearest', behavior: jumped ? 'auto' : 'smooth' });
    }, [body]);
    useClock(follow);
}
