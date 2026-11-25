// The anchored menu: opens under (or over, near the bottom) the element that opened it; closes on a tap outside,
// Escape, or a resize. Inside: <MenuItem>, <MenuTitle>, <hr/>, <p className="note">.
//
//   const [anchor, setAnchor] = useState<HTMLElement | null>(null);
//   <Chip onClick={e => setAnchor(e.currentTarget)}>…</Chip>
//   <Popover anchor={anchor} onClose={() => setAnchor(null)}>
//       <MenuItem on={sort === 'title'} onClick={() => { setSort('title'); setAnchor(null); }}>{t('library.sort.title')}</MenuItem>
//   </Popover>

import { useEffect, useLayoutEffect, useRef, useState, type ReactNode } from 'react';
import { createPortal } from 'react-dom';
import { Icon } from './Icon';

/** The interface's size: the app is drawn zoomed, and positions inside it are in its own pixels. */
function zoom(): number {
    return parseFloat(getComputedStyle(document.documentElement).getPropertyValue('--ui-scale')) || 1;
}

export function Popover({ anchor, onClose, width = 330, children, label }: { anchor: HTMLElement | null; onClose: () => void; width?: number; children: ReactNode; label?: string }) {
    const box = useRef<HTMLDivElement>(null);
    const [style, setStyle] = useState<React.CSSProperties>({ visibility: 'hidden' });

    // under the anchor, or over it near the bottom; content taller than that side's room goes to the side where it fits,
    // and content that fits on neither is kept whole in the window (over the anchor) — placed again as it grows
    useLayoutEffect(() => {
        if (!anchor) return;
        const place = () => {
            const z = zoom();
            const r = anchor.getBoundingClientRect();
            const vw = innerWidth / z, vh = innerHeight / z;
            const w = Math.min(width, vw - 16);
            const x = r.left / z, top = r.top / z, bottom = r.bottom / z;
            const left = Math.max(8, Math.min(x, vw - w - 8));
            const need = box.current?.scrollHeight ?? 0;
            const roomBelow = vh - bottom - 16, roomAbove = top - 16;
            const below = vh - bottom > 320 || bottom < vh / 2;
            const next: React.CSSProperties = need <= (below ? roomBelow : roomAbove)
                ? (below ? { left, width: w, top: bottom + 6, maxHeight: roomBelow } : { left, width: w, bottom: vh - top + 6, maxHeight: roomAbove })
                : need <= roomBelow ? { left, width: w, top: bottom + 6, maxHeight: roomBelow }
                : need <= roomAbove ? { left, width: w, bottom: vh - top + 6, maxHeight: roomAbove }
                : { left, width: w, top: Math.max(8, vh - need - 8), maxHeight: vh - 16 };
            setStyle(previous => (JSON.stringify(previous) === JSON.stringify(next) ? previous : next));
        };
        place();
        const changes = new MutationObserver(place);
        if (box.current) changes.observe(box.current, { childList: true, subtree: true, characterData: true });
        return () => changes.disconnect();
    }, [anchor, width]);

    useEffect(() => {
        if (!anchor) return;
        const down = (e: PointerEvent) => {
            if (!box.current?.contains(e.target as Node) && !anchor.contains(e.target as Node)) onClose();
        };
        const key = (e: KeyboardEvent) => e.key === 'Escape' && onClose();
        document.addEventListener('pointerdown', down);
        document.addEventListener('keydown', key);
        addEventListener('resize', onClose);
        return () => {
            document.removeEventListener('pointerdown', down);
            document.removeEventListener('keydown', key);
            removeEventListener('resize', onClose);
        };
    }, [anchor, onClose]);

    if (!anchor) return null;
    return createPortal(<div ref={box} className="pop" style={style} role="menu" aria-label={label}>{children}</div>, document.getElementById('root')!);
}

/** A menu's line: a choice (its tick on the right) or, with `check`, a box to tick (on the left). */
export function MenuItem({ on, check, sub, onClick, children }: { on?: boolean; check?: boolean; sub?: ReactNode; onClick: () => void; children: ReactNode }) {
    return (
        <button type="button" className={on ? 'it on' : 'it'} role={check ? 'menuitemcheckbox' : 'menuitemradio'} aria-checked={!!on} onClick={onClick}>
            {check && <span className="ck">{on && <Icon name="check" />}</span>}
            <span style={{ flex: 1, minWidth: 0 }}>{children}{sub && <small>{sub}</small>}</span>
            {!check && on && <Icon name="check" />}
        </button>
    );
}

export function MenuTitle({ children }: { children: ReactNode }) {
    return <div className="ttl">{children}</div>;
}
