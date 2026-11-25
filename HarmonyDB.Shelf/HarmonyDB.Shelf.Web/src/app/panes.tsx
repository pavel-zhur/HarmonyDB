// The two shapes of a screen.
//
// <Page>: one pane of content (home, playlists, settings …).
// <ListWithSong>: a list and, when a song of it is open, the song — side by side on a wide screen (the border between
// them dragged by the mouse; the width is remembered, a double click brings back the first), the song over the list
// on a phone (back on the list where one left it, the row one came back from lit).

import { useEffect, useLayoutEffect, useRef, type ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { ToTop } from './ToTop';
import { useWide } from './layout';

export function Page({ children, wide }: { children: ReactNode; wide?: boolean }) {
    return (
        <div className="pane">
            <div className={wide ? 'pad wide' : 'pad'}>{children}</div>
            <ToTop />
        </div>
    );
}

const widthKey = 'shelf.listWidth';

export function ListWithSong({ list, song, songKey }: { list: ReactNode; song: ReactNode | null; songKey: string | null }) {
    const { t } = useTranslation();
    const wide = useWide();
    const lib = useRef<HTMLDivElement>(null);
    const songPane = useRef<HTMLDivElement>(null);
    const listScroll = useRef(0);
    const open = song !== null;

    // the list's width, as the person left it
    useLayoutEffect(() => {
        try {
            const width = localStorage.getItem(widthKey);
            if (width) lib.current?.style.setProperty('--lw', width);
        } catch {
            // no storage: the first width
        }
    }, []);

    // a phone: the song covers the list in the one column; back, the column is where it was
    useLayoutEffect(() => {
        if (wide) return;
        const column = lib.current?.closest('.mid') as HTMLElement | null;
        if (!column) return;
        if (open) column.scrollTop = 0;
        else column.scrollTop = listScroll.current;
    }, [open, wide]);

    useEffect(() => {
        if (wide || open) return;
        const column = lib.current?.closest('.mid') as HTMLElement | null;
        if (!column) return;
        const remember = () => {
            listScroll.current = column.scrollTop;
        };
        column.addEventListener('scroll', remember, { passive: true });
        return () => column.removeEventListener('scroll', remember);
    }, [wide, open]);

    // another song: its pane starts at the top
    useLayoutEffect(() => {
        if (wide && songPane.current) songPane.current.scrollTop = 0;
    }, [songKey, wide]);

    const drag = (e: React.PointerEvent<HTMLDivElement>) => {
        e.preventDefault();
        const listPane = e.currentTarget.previousElementSibling as HTMLElement;
        const container = lib.current!;
        const zoom = parseFloat(getComputedStyle(document.documentElement).getPropertyValue('--ui-scale')) || 1;
        const x0 = e.clientX, w0 = listPane.getBoundingClientRect().width / zoom;
        const max = container.getBoundingClientRect().width / zoom - 320;
        let width = w0;
        document.body.classList.add('resizing');
        const move = (m: PointerEvent) => {
            width = Math.round(Math.max(280, Math.min(max, w0 + (m.clientX - x0) / zoom)));
            container.style.setProperty('--lw', width + 'px');
        };
        const up = () => {
            document.body.classList.remove('resizing');
            removeEventListener('pointermove', move);
            removeEventListener('pointerup', up);
            try {
                localStorage.setItem(widthKey, width + 'px');
            } catch {
                // kept until the page closes
            }
        };
        addEventListener('pointermove', move);
        addEventListener('pointerup', up);
    };

    const reset = () => {
        lib.current?.style.removeProperty('--lw');
        try {
            localStorage.removeItem(widthKey);
        } catch {
            // nothing kept
        }
    };

    return (
        <div ref={lib} className={open ? 'lib open' : 'lib'}>
            <div className="pane list">
                {list}
                <ToTop />
            </div>
            <div className="split" role="separator" aria-orientation="vertical" title={t('nav.resize')} onPointerDown={drag} onDoubleClick={reset} />
            <div ref={songPane} className="pane song">
                {song}
                {/* the song carries its own "to top", above its player's bar (screens/song/SongPane.tsx) */}
            </div>
        </div>
    );
}
