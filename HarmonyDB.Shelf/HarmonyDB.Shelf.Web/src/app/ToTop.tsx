import { useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Icon } from '../kit/Icon';
import { scrollerOf, useWide } from './layout';

/**
 * "To the top", at the end of a pane (a list, a song: one each, as in the old app). Hidden while already near the
 * top.
 */
export function ToTop() {
    const { t } = useTranslation();
    const button = useRef<HTMLButtonElement>(null);
    const [show, setShow] = useState(false);
    const wide = useWide();

    useEffect(() => {
        const scroller = scrollerOf(button.current);
        if (!scroller) return;
        const update = () => setShow(scroller.scrollTop > 250);
        update();
        scroller.addEventListener('scroll', update, { passive: true });
        return () => scroller.removeEventListener('scroll', update);
    }, [wide]);

    return (
        <button ref={button} type="button" className={show ? 'totop show' : 'totop'} aria-label={t('nav.toTop')} title={t('nav.toTop')}
            onClick={() => scrollerOf(button.current)?.scrollTo({ top: 0, behavior: 'smooth' })}>
            <Icon name="up" />
        </button>
    );
}
