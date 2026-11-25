// The song's illustrations (of its chords, common to all, drawn by the server): each variant's, grouped by the mood
// they were drawn in; a tap opens the big one. A person who may order them orders a batch per variant, in a mood
// ("preset"); while a batch is drawn its place waits under the variant, and one may leave the page meanwhile.

import { useState } from 'react';
import { useMutation } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { errorText } from '../../api/errors';
import { drawIllustrations, drawPermission, presetName, useDrawnIllustrations, usePresets } from '../../data/illustrations';
import { useSession } from '../../data/session';
import { Icon } from '../../kit/Icon';
import { MenuItem, MenuTitle, Popover } from '../../kit/Popover';
import './illustrations.css';

export interface IllustratedVariant {
    chordsId: string;
    /** "A. Its source", or null when the song has one variant */
    label: string | null;
}

export function Illustrations({ variants, onClose }: { variants: IllustratedVariant[]; onClose: () => void }) {
    const { t } = useTranslation();
    const [big, setBig] = useState<string | null>(null);
    const { signedIn, session } = useSession();
    const may = !!session?.permissions.includes(drawPermission);
    return (
        <section className="vview">
            <div className="vhead">
                <span className="t">{t('song.illustrationsTitle')}</span>
                <button type="button" className="xbtn" aria-label={t('common.close')} title={t('common.close')} onClick={onClose}><Icon name="x" /></button>
            </div>
            <p className="muted small illnote">{t('song.illustrationsNote')}{signedIn && !may && ` ${t('ills.notOn')}`}</p>
            {big && <button type="button" className="illbig" onClick={() => setBig(null)} aria-label={t('common.close')}><img src={big} alt="" /></button>}
            {variants.map(v => <VariantIllustrations key={v.chordsId} variant={v} onBig={setBig} />)}
        </section>
    );
}

function VariantIllustrations({ variant, onBig }: { variant: IllustratedVariant; onBig: (url: string | null) => void }) {
    const { t } = useTranslation();
    const { session } = useSession();
    const illustrations = useDrawnIllustrations(variant.chordsId);
    const [anchor, setAnchor] = useState<HTMLElement | null>(null);
    const data = illustrations.data;
    const may = !!session?.permissions.includes(drawPermission);
    const images = data?.images ?? [];
    const moods = [...new Set(images.map(x => x.preset ?? ''))];
    return (
        <div className="il-var">
            {(variant.label || may) && (
                <div className="il-head">
                    {variant.label && <b>{variant.label}</b>}
                    {may && data && !data.drawing && (
                        <button type="button" className="chip" onClick={e => setAnchor(anchor ? null : e.currentTarget)}>
                            <Icon name="plus" />{t(images.length ? 'ills.orderMore' : 'ills.order')}
                        </button>
                    )}
                </div>
            )}
            {illustrations.isError && <p className="failed il-pad">{errorText(illustrations.error)}</p>}
            {data?.drawing && (
                <div className="il-drawing">
                    <Icon name="loop" className="spin" />
                    <span>{t('ills.drawing', { preset: presetName(data.drawing.preset) })}<span className="muted small">{t('ills.drawingNote')}</span></span>
                </div>
            )}
            {moods.map(mood => (
                <div key={mood}>
                    {mood && (moods.length > 1 || mood.split('-')[0] !== 'plain') && <div className="il-mood">{presetName(mood)}</div>}
                    <div className="illgrid">
                        {images.filter(x => (x.preset ?? '') === mood).map((image, i) => (
                            <button type="button" key={image.id} onClick={() => onBig(image.urls['1024'] ?? image.urls['512'] ?? null)} aria-label={t('song.illustration', { n: i + 1 })}>
                                <img src={image.urls['256']} alt="" loading="lazy" />
                            </button>
                        ))}
                    </div>
                </div>
            ))}
            {data && !images.length && !data.drawing && <p className="muted small il-pad">{t('ills.none')}</p>}
            <OrderPop anchor={anchor} onClose={() => setAnchor(null)} chordsId={variant.chordsId} />
        </div>
    );
}

/** The moods the server draws in; a mood chosen is ordered after a second, plain yes (drawing costs). */
export function OrderPop({ anchor, onClose, chordsId }: { anchor: HTMLElement | null; onClose: () => void; chordsId: string }) {
    const { t } = useTranslation();
    const presets = usePresets();
    const draw = useMutation({ mutationFn: drawIllustrations });
    const [chosen, setChosen] = useState<string | null>(null);
    const close = () => {
        setChosen(null);
        onClose();
    };
    return (
        <Popover anchor={anchor} onClose={close} width={330} label={t('ills.orderTitle')}>
            {chosen === null ? (
                <>
                    <MenuTitle>{t('ills.orderTitle')}</MenuTitle>
                    {presets.isError && <p className="note">{errorText(presets.error)}</p>}
                    {presets.data?.presets.map(preset => <MenuItem key={preset} onClick={() => setChosen(preset)}>{presetName(preset)}</MenuItem>)}
                    <p className="note">{t('ills.orderNote')}</p>
                </>
            ) : (
                <div className="il-ask">
                    <p>{t('ills.orderAsk', { preset: presetName(chosen) })}</p>
                    <div className="chips">
                        <button type="button" className="btn pri" onClick={() => { close(); draw.mutate({ chordsId, preset: chosen }); }}>{t('ills.orderYes')}</button>
                        <button type="button" className="btn" onClick={() => setChosen(null)}>{t('common.cancel')}</button>
                    </div>
                </div>
            )}
        </Popover>
    );
}
