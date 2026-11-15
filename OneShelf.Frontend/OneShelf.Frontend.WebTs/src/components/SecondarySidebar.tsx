// The original: Components/SecondarySidebar.cs — renders nothing itself: it hands its content to MainLayout, which
// shows it in the secondary sidebar (MainLayout's `@SecondarySidebarContent`, its `_secondarySidebar?.ChildContent`).
//
// MainLayout cascades itself (<CascadingValue Value="this"> around the page): here MainLayoutContext, whose value
// MainLayout provides with the three members SecondarySidebar calls. As in the original:
// - when it appears: MainLayout.SetSecondarySidebar(this) (which re-renders MainLayout);
// - at every render: MainLayout.SetSidebar(IsJust), and, from the second render on, MainLayout.Update() (from
//   ShouldRender, which Blazor does not call for the first render) — so MainLayout shows the current content;
// - when it goes: MainLayout.SetSecondarySidebar(null).

import { createContext, useContext, useLayoutEffect, useRef, type ReactNode } from 'react';

/** What MainLayout shows of a SecondarySidebar. */
export interface SecondarySidebarInstance {
    /** ChildContent, as of the SecondarySidebar's latest render. */
    readonly childContent: ReactNode;
}

/** The cascaded MainLayout, as SecondarySidebar uses it. */
export interface MainLayoutCascade {
    setSecondarySidebar(secondarySidebar: SecondarySidebarInstance | null): void;
    setSidebar(just: boolean): void;
    update(): void;
}

/** `<CascadingValue Value="this">` in MainLayout. */
export const MainLayoutContext = createContext<MainLayoutCascade | null>(null);

export interface SecondarySidebarProps {
    isJust: boolean;
    children: ReactNode;
}

export function SecondarySidebar({ isJust, children }: SecondarySidebarProps) {
    const mainLayout = useContext(MainLayoutContext)!;
    const instance = useRef<{ childContent: ReactNode }>({ childContent: children });
    instance.current.childContent = children;
    const rendered = useRef(false);

    // OnInitialized; Dispose
    useLayoutEffect(() => {
        mainLayout.setSecondarySidebar(instance.current);
        return () => mainLayout.setSecondarySidebar(null);
    }, [mainLayout]);

    // OnParametersSet; ShouldRender
    useLayoutEffect(() => {
        mainLayout.setSidebar(isJust);
        if (rendered.current) {
            mainLayout.update();
        }

        rendered.current = true;
    });

    return null;
}
