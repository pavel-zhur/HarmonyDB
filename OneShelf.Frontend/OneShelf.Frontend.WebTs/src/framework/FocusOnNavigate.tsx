// Blazor's FocusOnNavigate (.NET 9, WebAssembly): after a render in which the page (the page component) changed, and
// after the first render, focuses the first element matching the selector, making it focusable (tabindex -1) when it
// has no tabindex, without scrolling to it.
//
// Blazor calls OnAfterRenderAsync after the whole render batch, which includes the renders queued while it was being
// rendered (the layout's, when the old page's SecondarySidebar goes and the layout stops hiding the content). React
// commits those as separate, synchronous renders after this effect; the focus waits for them (a microtask runs once
// they are done, before anything is painted), or the element would still be hidden and could not take the focus.

import { useEffect } from 'react';

export function FocusOnNavigate({ page, selector }: { page: unknown; selector: string }) {
    useEffect(() => {
        let current = true;
        queueMicrotask(() => {
            if (!current) return;
            const element = document.querySelector<HTMLElement>(selector);
            if (element) {
                if (!element.hasAttribute('tabindex')) {
                    element.tabIndex = -1;
                }

                element.focus({ preventScroll: true });
            }
        });

        return () => {
            current = false;
        };
    }, [page, selector]);

    return null;
}
