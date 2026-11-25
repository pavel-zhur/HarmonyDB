// The dialog: a card over the dimmed page, where the one solid button of the design lives. A tap on the dim, or
// Escape, closes it (nothing is changed by that).

import { useEffect, type ReactNode } from 'react';
import { createPortal } from 'react-dom';

export function Dialog({ label, onClose, children }: { label: string; onClose: () => void; children: ReactNode }) {
    useEffect(() => {
        const key = (e: KeyboardEvent) => e.key === 'Escape' && onClose();
        document.addEventListener('keydown', key);
        return () => document.removeEventListener('keydown', key);
    }, [onClose]);

    return createPortal(
        <div className="scrim" onClick={onClose}>
            <div className="dialog" role="dialog" aria-modal="true" aria-label={label} onClick={e => e.stopPropagation()}>{children}</div>
        </div>,
        document.getElementById('root')!,
    );
}
