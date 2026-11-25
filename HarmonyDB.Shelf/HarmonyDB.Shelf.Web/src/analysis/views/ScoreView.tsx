// The score (MusicXML), engraved: its parts chosen to show (the first at first), the bar that sounds lit by the score's
// own times on the recording, a tap on a bar plays from there. A score without times is drawn only, and says so.

import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { OpenSheetMusicDisplay } from 'opensheetmusicdisplay';
import type { Score } from '../../data/analysis';
import { analysisPlayer, useAnalysisClock } from '../clock';
import { useThemeChange } from '../redraw';
import { numberRepeats, usePartName, type PartIdentity } from '../partNames';

interface Box { left: number; top: number; width: number; height: number }

/**
 * The score with its parts named as the person reads them (partNames.ts): in the part list, where the chips read them,
 * and as the engraving writes them. A part's General MIDI program is in its <midi-instrument> (1–128 there).
 */
function namedScore(xml: string, partName: (part: PartIdentity) => string): { xml: string; names: string[] } {
    const doc = new DOMParser().parseFromString(xml, 'application/xml');
    const parts = [...doc.querySelectorAll('part-list > score-part')];
    const names = numberRepeats(parts.map((part, i) => {
        const program = Number(part.querySelector('midi-instrument > midi-program')?.textContent);
        return partName({ name: part.querySelector('part-name')?.textContent?.trim() || `${i + 1}`, program: program >= 1 && program <= 128 ? program - 1 : null });
    }));
    parts.forEach((part, i) => {
        const el = part.querySelector('part-name');
        if (el) el.textContent = names[i]!;
        part.querySelector('part-abbreviation')?.remove();     // (an English short name would stay on later rows)
    });
    return { xml: new XMLSerializer().serializeToString(doc), names };
}

export default function ScoreView({ score }: { score: Score }) {
    const { t } = useTranslation();
    const host = useRef<HTMLDivElement>(null);
    const osmd = useRef<OpenSheetMusicDisplay | null>(null);
    const partName = usePartName();
    const { xml, names } = useMemo(() => namedScore(score.musicXml, partName), [score, partName]);
    const [shown, setShown] = useState<Set<number>>(() => new Set([0]));
    const [boxes, setBoxes] = useState<Box[]>([]);
    const [state, setState] = useState<'loading' | 'ready' | 'failed'>('loading');
    const times = score.times;

    // engraved anew when other parts are chosen, and when the width changes
    const engrave = useCallback(() => {
        const engraver = osmd.current, el = host.current;
        if (!engraver || !el) return;
        engraver.Sheet.Instruments.forEach((instrument, i) => { instrument.Visible = shown.has(i); });
        // in the theme's ink; smaller on a narrow screen, so that a row holds more than a bar
        const ink = getComputedStyle(el).getPropertyValue('--ink').trim();
        engraver.setOptions({
            defaultColorMusic: ink, defaultColorNotehead: ink, defaultColorStem: ink, defaultColorRest: ink, defaultColorLabel: ink, defaultColorTitle: ink,
        });
        engraver.Zoom = el.clientWidth < 600 ? 0.62 : 0.9;
        engraver.render();
        // where each bar stands: the measures of every staff shown, together
        const unit = 10 * engraver.Zoom, list = engraver.GraphicSheet.MeasureList;
        setBoxes(list.map(staves => {
            // (the measures of the parts not shown are in the list too, not laid out)
            const drawn = staves.filter(m => m?.PositionAndShape && m.ParentStaff?.ParentInstrument?.Visible && m.PositionAndShape.Size.width > 0);
            if (!drawn.length) return { left: 0, top: 0, width: 0, height: 0 };
            // across its staves: from the top line of the first to the bottom line of the last (a staff is 4 units high)
            const first = drawn[0]!.PositionAndShape;
            const top = Math.min(...drawn.map(m => m!.PositionAndShape.AbsolutePosition.y));
            const bottom = Math.max(...drawn.map(m => m!.PositionAndShape.AbsolutePosition.y)) + 4;
            return { left: first.AbsolutePosition.x * unit, top: (top - 2) * unit, width: first.Size.width * unit, height: (bottom - top + 4) * unit };
        }));
    }, [shown]);

    useEffect(() => {
        const el = host.current;
        if (!el) return;
        // every bar drawn as a measure of its own (no rests of many bars gathered into one): measure i is bar i. (The
        // engraver writes its styles through the style object: build/engraverStyles.ts.)
        const engraver = new OpenSheetMusicDisplay(el, {
            autoResize: false, backend: 'svg', drawTitle: false, drawComposer: false, drawCredits: false, drawPartNames: true,
            autoGenerateMultipleRestMeasuresFromRestMeasures: false,
        });
        osmd.current = engraver;
        let alive = true;
        engraver.load(xml).then(() => {
            if (!alive) return;
            setState('ready');
        }, () => alive && setState('failed'));
        return () => {
            alive = false;
            engraver.clear();
            osmd.current = null;
        };
    }, [xml]);

    useEffect(() => {
        if (state !== 'ready') return;
        engrave();
        const el = host.current!;
        let width = el.clientWidth;
        const observer = new ResizeObserver(() => {
            if (Math.abs(el.clientWidth - width) < 24) return;
            width = el.clientWidth;
            engrave();
        });
        observer.observe(el);
        return () => observer.disconnect();
    }, [state, engrave]);
    useThemeChange(useCallback(() => { if (state === 'ready') engrave(); }, [state, engrave]));

    // the bar that sounds, by the score's times
    const marks = useRef<(HTMLDivElement | null)[]>([]);
    const lit = useRef(-1);
    const onTime = useCallback((position: number) => {
        if (!times) return;
        const bars = times.bars;
        let b = -1;
        if (bars.length && position >= bars[0]!.beats[0]! - 0.001 && position < times.end) {
            b = 0;
            while (b + 1 < bars.length && bars[b + 1]!.beats[0]! <= position + 0.001) b++;
        }
        if (b === lit.current) return;
        marks.current[lit.current]?.classList.remove('now');
        marks.current[b]?.classList.add('now');
        lit.current = b;
    }, [times]);
    useAnalysisClock(onTime);

    const toggle = (i: number) => setShown(prev => {
        const next = new Set(prev);
        if (next.has(i) && next.size > 1) next.delete(i);
        else next.add(i);
        return next;
    });

    return (
        <div className="an-scorev">
            {names.length > 1 && (
                <div className="chips">
                    {names.map((name, i) => <button type="button" key={i} className={shown.has(i) ? 'chip on' : 'chip'} aria-pressed={shown.has(i)} onClick={() => toggle(i)}>{name}</button>)}
                </div>
            )}
            {!times && <p className="muted small">{t('analysis.scoreNoTimes')}</p>}
            {state === 'loading' && <p className="loading">{t('common.loading')}</p>}
            {state === 'failed' && <p className="failed">{t('analysis.scoreFailed')}</p>}
            <div className="an-scorebox">
                <div ref={host} className="an-scorehost" />
                {times && boxes.map((box, i) => (i < times.bars.length && box.width > 0 ? (
                    <div key={i} ref={el => { marks.current[i] = el; }} className="an-smeasure" role="button" tabIndex={-1} title={t('analysis.barN', { n: i + 1 })}
                        style={{ left: box.left, top: box.top, width: box.width, height: box.height }}
                        onClick={() => analysisPlayer.playFrom(times.bars[i]!.beats[0]!)} />
                ) : null))}
            </div>
        </div>
    );
}
