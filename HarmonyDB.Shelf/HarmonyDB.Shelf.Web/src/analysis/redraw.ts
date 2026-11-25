// What is drawn with the design's colours read at drawing (a canvas, the score) is drawn again when the theme changes —
// the system's, or the one chosen in the profile — and, on a canvas, when its box changes size.

import { useEffect, type RefObject } from 'react';

export function useThemeChange(onChange: () => void): void {
    useEffect(() => {
        const scheme = matchMedia('(prefers-color-scheme: dark)');
        scheme.addEventListener('change', onChange);
        const chosen = new MutationObserver(onChange);
        chosen.observe(document.documentElement, { attributes: true, attributeFilter: ['data-theme'] });
        return () => {
            scheme.removeEventListener('change', onChange);
            chosen.disconnect();
        };
    }, [onChange]);
}

export function useRedraw(box: RefObject<HTMLElement | null>, draw: () => void): void {
    useThemeChange(draw);
    useEffect(() => {
        const el = box.current;
        if (!el) return;
        const resized = new ResizeObserver(() => draw());
        resized.observe(el);
        return () => resized.disconnect();
    }, [box, draw]);
}
