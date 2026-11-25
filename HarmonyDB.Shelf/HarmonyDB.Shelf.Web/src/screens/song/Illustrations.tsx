// The song's illustrations (of its chords, common to all, drawn by the server): a grid; a tap opens the big one.

import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { Schemas } from '../../api/client';
import { Icon } from '../../kit/Icon';

export function Illustrations({ images, onClose }: { images: Schemas['IllustrationImage'][]; onClose: () => void }) {
    const { t } = useTranslation();
    const [big, setBig] = useState<string | null>(null);
    return (
        <section className="vview">
            <div className="vhead">
                <span className="t">{t('song.illustrationsTitle')}</span>
                <button type="button" className="xbtn" aria-label={t('common.close')} title={t('common.close')} onClick={onClose}><Icon name="x" /></button>
            </div>
            <p className="muted small illnote">{t('song.illustrationsNote')}</p>
            {big && <button type="button" className="illbig" onClick={() => setBig(null)} aria-label={t('common.close')}><img src={big} alt="" /></button>}
            <div className="illgrid">
                {images.map((image, i) => (
                    <button type="button" key={image.id} onClick={() => setBig(image.urls['1024'] ?? image.urls['512'] ?? null)} aria-label={t('song.illustration', { n: i + 1 })}>
                        <img src={image.urls['256']} alt="" loading="lazy" />
                    </button>
                ))}
            </div>
        </section>
    );
}
