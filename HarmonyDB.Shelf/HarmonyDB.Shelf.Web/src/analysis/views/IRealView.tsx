// The analysis as iReal Pro writes a chart: four bars a row, the sections' marks, the chords where they begin in their
// bars, the meter and the key at the start and where they change; the bar that sounds lit, a tap plays from a bar. When
// the analysis has its iReal Pro chart, it opens in the app.

import { useCallback, useMemo, useRef } from 'react';
import { useTranslation } from 'react-i18next';
import { analysisPlayer, useAnalysisClock } from '../clock';
import { barAt, type Piece, type Bar, type Part } from '../piece';
import { partName, useChangeWords, useChordText } from '../marks';

/** A section's mark: the intro "i", the others a letter each label, in the order the labels come. */
function marksOf(piece: Piece): Map<number, string> {
    const letters = new Map<string, string>(), marks = new Map<number, string>();
    for (const section of piece.sections) {
        const label = section.label ?? section.name;
        if (!letters.has(label)) letters.set(label, label === 'intro' ? 'i' : String.fromCharCode(65 + [...letters.values()].filter(l => l !== 'i').length));
        marks.set(section.from, letters.get(label)!);
    }
    return marks;
}

/**
 * What every bar writes: the chords that begin in it, in order (where the name changes, or a chord is struck again); a
 * bar where nothing begins writes the chord that goes on, as iReal Pro does.
 */
function cellsOf(bars: Bar[]): string[][] {
    let prevName: string | null = null, prevRun: Part['run'] = null;
    return bars.map(bar => {
        const out: string[] = [];
        const held = prevName;
        bar.parts.forEach(parts => parts.forEach(part => {
            const name = partName(part);
            if (name && (name !== prevName || (!!part.run && part.run !== prevRun))) out.push(name);
            prevName = name;
            prevRun = part.run;
        }));
        return out.length || !held ? out : [held];
    });
}

export function IRealView({ piece, link, title, artists }: { piece: Piece; link: string | null; title: string; artists: string[] }) {
    const { t } = useTranslation();
    const changeWords = useChangeWords();
    const chordText = useChordText();
    const marks = useMemo(() => marksOf(piece), [piece]);
    const cells = useMemo(() => cellsOf(piece.bars), [piece]);
    const changes = useMemo(() => new Map(piece.changes.filter(c => c.bar > 0).map(c => [c.bar, c])), [piece]);
    const rows = useMemo(() => {
        const out: Bar[][] = [];
        for (let i = 0; i < piece.bars.length; i += 4) out.push(piece.bars.slice(i, i + 4));
        return out;
    }, [piece]);

    const els = useRef<(HTMLButtonElement | null)[]>([]);
    const lit = useRef(-1);
    const onTime = useCallback((position: number) => {
        const b = barAt(piece, position);
        if (b === lit.current) return;
        els.current[lit.current]?.classList.remove('now');
        els.current[b]?.classList.add('now');
        lit.current = b;
    }, [piece]);
    useAnalysisClock(onTime);

    const first = piece.changes[0];
    return (
        <div className="an-irv">
            <div className="an-irh">
                <b>{title}</b>
                <span>{artists.join(', ')}</span>
                {first && <i>{changeWords(first, true, piece.bars[0]?.tempo?.beatUnit).join(' · ')}</i>}
            </div>
            {rows.map((row, r) => (
                <div className="an-irr" key={r}>
                    {row.map(bar => {
                        const change = changes.get(bar.index);
                        return (
                            <button type="button" key={bar.index} ref={el => { els.current[bar.index] = el; }} className="an-irb" title={t('analysis.barN', { n: bar.number })}
                                onClick={() => analysisPlayer.playFrom(bar.start)}>
                                {marks.has(bar.index) && <span className="an-irm">{marks.get(bar.index)}</span>}
                                {change && <span className="an-irc">{changeWords(change, false, bar.tempo?.beatUnit).join(' ')}</span>}
                                {bar.fermatas.length > 0 && <span className="an-irf" aria-hidden="true">𝄐</span>}
                                <span className={`an-irchords n${Math.min(3, cells[bar.index]!.length)}`}>
                                    {cells[bar.index]!.map((name, i) => <b key={i}>{name.startsWith('N.C') || name === '‖' ? name : chordText(name)}</b>)}
                                </span>
                            </button>
                        );
                    })}
                </div>
            ))}
            {link && (
                <p className="muted small an-irlink">
                    {t('analysis.irealOpen')} <a href={link}>{t('analysis.irealOpenLink')}</a>
                </p>
            )}
        </div>
    );
}
