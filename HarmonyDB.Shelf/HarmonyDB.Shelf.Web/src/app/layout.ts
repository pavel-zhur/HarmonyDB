// The two layouts: a phone (one scrolling column, the header scrolls away, bottom tabs) and a wide screen (from
// 960px: the header stays, the side menu, panes side by side, each scrolling on its own).

import { useSyncExternalStore } from 'react';

const wideQuery = '(min-width: 960px)';

export function useWide(): boolean {
    return useSyncExternalStore(listener => {
        const query = matchMedia(wideQuery);
        query.addEventListener('change', listener);
        return () => query.removeEventListener('change', listener);
    }, () => matchMedia(wideQuery).matches);
}

/** The element that scrolls an element's content: its pane on a wide screen, the one column on a phone. */
export function scrollerOf(element: Element | null): HTMLElement | null {
    if (!element) return null;
    return (matchMedia(wideQuery).matches ? element.closest('.pane') : element.closest('.mid')) as HTMLElement | null;
}
