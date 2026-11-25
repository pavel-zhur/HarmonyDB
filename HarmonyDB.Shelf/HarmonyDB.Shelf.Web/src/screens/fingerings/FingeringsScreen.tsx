// The fingerings: every chord the fingerings know, on a root chosen here, major and minor. A tap plays the chord and
// opens the same window as a tap on a chord of a song, with all its fingerings (chords/FingeringWindow.tsx; each
// chord's pictures are loaded when it is tapped). A search by the chord's name ("Am7", "sus4").

import { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Page } from '../../app/panes';
import { chordDataParser } from '../../chords/analysis';
import { fingeringTypes } from '../../chords/fingerings';
import { FingeringWindow } from '../../chords/FingeringWindow';
import { playChord } from '../../chords/sound';
import { chordNameHtml } from '../../chords/view';
import { RepresentationSettings } from '../../lib/representations';
import { SearchField } from '../../kit/controls';
import '../../chords/chords.css';
import './fingerings.css';

/** The roots as the chords' data numbers them (A is 0), and how they are written. */
const roots = ['A', 'Bb', 'B', 'C', 'C#', 'D', 'Eb', 'E', 'F', 'F#', 'G', 'Ab'];
const rootShown = (i: number) => roots[i]!.replace('b', '♭').replace('#', '♯');

export function FingeringsScreen() {
    const { t } = useTranslation();
    const [types, setTypes] = useState<string[] | 'failed' | null>(null);
    const [root, setRoot] = useState(3);
    const [q, setQ] = useState('');

    useEffect(() => {
        let gone = false;
        fingeringTypes().then(x => !gone && setTypes(x)).catch(() => !gone && setTypes('failed'));
        return () => {
            gone = true;
        };
    }, []);

    // the types' endings ("", "7", "m7" …) the chords can be played as
    const suffixes = useMemo(() => (Array.isArray(types) ? types.map(x => x.slice(1)).filter(s => chordDataParser.getNotes(`[!3!]${s}`) !== null) : []), [types]);
    // a search names a root too ("F#m7"): the root is chosen, the rest is looked for
    const query = q.trim();
    const searched = /^([A-Ha-h])([#♯b♭]?)(.*)$/.exec(query);
    const wanted = (searched ? searched[3]! : query).toLowerCase();
    useEffect(() => {
        if (!searched) return;
        const letter = searched[1]!.toUpperCase().replace('H', 'B');
        const sign = searched[2]!.replace('♯', '#').replace('♭', 'b');
        const index = roots.indexOf(letter + sign);
        if (index >= 0) setRoot(index);
    }, [query]);
    const shown = suffixes.filter(s => !wanted || s.toLowerCase().includes(wanted));
    const minor = (s: string) => s.startsWith('m') && !s.startsWith('maj');
    const view = useMemo(() => new RepresentationSettings(), []);

    const chord = (s: string) => {
        const text = roots[root]! + s;
        return (
            <button type="button" key={s} className="fi-chord" onClick={() => playChord(`[!${root}!]${s}`, rootShown(root) + s)}
                aria-label={rootShown(root) + s}>
                <b className="chord" dangerouslySetInnerHTML={{ __html: chordNameHtml(text, view) }} />
            </button>
        );
    };

    return (
        <Page>
            <h1>{t('tools.fingerings')}</h1>
            <p className="lead">{t('fingerings.lead')}</p>
            <div className="fi-search"><SearchField value={q} onChange={setQ} placeholder={t('fingerings.search')} /></div>
            <div className="fi-roots" role="radiogroup" aria-label={t('fingerings.root')}>
                {[3, 4, 5, 6, 7, 8, 9, 10, 11, 0, 1, 2].map(i => (
                    <button type="button" key={i} role="radio" aria-checked={i === root} className={i === root ? 'chip on' : 'chip'} onClick={() => setRoot(i)}>{rootShown(i)}</button>
                ))}
            </div>
            {types === null && <p className="loading">{t('common.loading')}</p>}
            {types === 'failed' && <p className="failed">{t('chords.fingerings.failed')}</p>}
            {Array.isArray(types) && (
                <div className="sheet fi-sheet">
                    <h2>{t('fingerings.major')}</h2>
                    <div className="fi-grid">{shown.filter(s => !minor(s)).map(chord)}</div>
                    <h2>{t('fingerings.minor')}</h2>
                    <div className="fi-grid">{shown.filter(minor).map(chord)}</div>
                    {!shown.length && <p className="muted">{t('fingerings.nothing')}</p>}
                </div>
            )}
            <FingeringWindow />
        </Page>
    );
}
