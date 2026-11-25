// A tapped chord's fingerings (the sound has played already): a window with all of them; with "Less" (the profile's
// setting) a small plate with the chord's name and its first fingering, which opens the window when tapped. The tick
// "Less" changes only the size.

import { useEffect, useState } from 'react';
import { createPortal } from 'react-dom';
import { useTranslation } from 'react-i18next';
import { updateSettings, useSettings } from '../data/settings';
import { Icon } from '../kit/Icon';
import { fingerings } from './fingerings';
import { forgetPlayedChord, playChord, usePlayedChord } from './sound';

export function FingeringWindow() {
    const { t } = useTranslation();
    const chord = usePlayedChord();
    const { lessMode } = useSettings();
    // null: loading; 'failed': could not be loaded (offline, a piece not kept yet)
    const [pictures, setPictures] = useState<string[] | 'failed' | null>(null);
    const [opened, setOpened] = useState(false);

    useEffect(() => {
        setPictures(null);
        setOpened(false);
        if (!chord) return;
        let gone = false;
        fingerings(chord.note, chord.fingering)
            .then(found => !gone && setPictures(found))
            .catch(() => !gone && setPictures('failed'));
        return () => {
            gone = true;
        };
    }, [chord]);

    // the window goes with the page; Escape closes it
    useEffect(() => forgetPlayedChord, []);
    useEffect(() => {
        if (!chord) return;
        const key = (e: KeyboardEvent) => e.key === 'Escape' && forgetPlayedChord();
        document.addEventListener('keydown', key);
        return () => document.removeEventListener('keydown', key);
    }, [chord]);

    if (!chord) return null;
    const root = document.getElementById('root')!;

    if (lessMode && !opened) {
        return createPortal(
            <button type="button" className="fing plate" aria-label={t('chords.fingerings.all')} onClick={() => setOpened(true)}>
                <b>{chord.name}</b>
                {Array.isArray(pictures) && pictures[0] && <span className="card"><img src={pictures[0]} alt="" /></span>}
            </button>, root);
    }

    return createPortal(
        <div className="fing" role="dialog" aria-label={t('chords.fingerings.title', { chord: chord.name })}>
            <div className="fh">
                <b>{chord.name}</b>
                <button type="button" className="xbtn" aria-label={t('chords.fingerings.again')} title={t('chords.fingerings.again')} onClick={() => playChord(chord.data, chord.name)}>
                    <Icon name="vol" />
                </button>
                <button type="button" className="xbtn" aria-label={t('common.close')} title={t('common.close')} onClick={forgetPlayedChord}><Icon name="x" /></button>
            </div>
            {pictures === null && <p className="muted small fstate">{t('common.loading')}</p>}
            {pictures === 'failed' && <p className="muted small fstate">{t('chords.fingerings.failed')}</p>}
            {Array.isArray(pictures) && pictures.length === 0 && <p className="muted small fstate">{t('chords.fingerings.none')}</p>}
            {Array.isArray(pictures) && pictures.length > 0 && (
                <div className="pics">{pictures.map((picture, i) => <img key={i} src={picture} alt="" />)}</div>
            )}
            <button type="button" className={lessMode ? 'less on' : 'less'} role="checkbox" aria-checked={lessMode} onClick={() => updateSettings({ lessMode: !lessMode })}>
                <span className="ck">{lessMode && <Icon name="check" />}</span>
                <span>{t('chords.fingerings.less')}<small>{t('chords.fingerings.lessSub')}</small></span>
            </button>
        </div>, root);
}
