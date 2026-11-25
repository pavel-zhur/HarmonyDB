// The grid of bars: a cell a bar, split into its beats; a chord's name where it changes (a beat that chords share is
// split where each begins). A section's name, and a change of key, meter or tempo, begin a row. A chord that goes on
// from the bar before is named again, lighter, where its bar begins a row: whoever starts reading there knows what
// sounds. The bar and the beat that sound are lit; a tap on a beat plays from there.

import { useCallback, useEffect, useLayoutEffect, useMemo, useRef, useState } from 'react';
import { useChordsView } from '../../chords/view';
import { planLabels } from '../fit';
import { useTranslation } from 'react-i18next';
import { analysisPlayer, useAnalysisClock } from '../clock';
import { barAt, type Piece, type Bar, type Part } from '../piece';
import { ChordLabel, partName, useChangeWords } from '../marks';

/** Bars in a row for the width the grid has. */
function perRow(width: number): number {
    const fit = Math.floor(width / 150);
    return fit >= 8 ? 8 : fit >= 4 ? 4 : fit >= 2 ? 2 : 1;
}

interface Slot { time: number; part: Part; label: string | null; share: number; degree: string | null }

/** Every beat's parts with the name written where the chord changes (or a chord written again is struck again). */
function slotsOf(bars: Bar[]): Slot[][][] {
    let prevName: string | null = null, prevRun: Part['run'] = null;
    return bars.map(bar => bar.parts.map(parts => parts.map((part, j) => {
        const name = partName(part);
        const fresh = name !== prevName || (!!part.run && part.run !== prevRun);
        prevName = name;
        prevRun = part.run;
        const next = parts[j + 1];
        const from = part.at[0] / part.at[1], to = next ? next.at[0] / next.at[1] : 1;
        return { time: part.time, part, label: fresh ? name : null, share: to - from, degree: part.chord?.degree ?? null };
    })));
}

export function GridView({ piece }: { piece: Piece }) {
    const { t } = useTranslation();
    const changeWords = useChangeWords();
    const box = useRef<HTMLDivElement>(null);
    const [width, setWidth] = useState(0);
    useEffect(() => {
        const el = box.current;
        if (!el) return;
        const observer = new ResizeObserver(() => setWidth(el.clientWidth));
        observer.observe(el);
        return () => observer.disconnect();
    }, []);
    const n = perRow(width || 600);
    const slots = useMemo(() => slotsOf(piece.bars), [piece]);

    // rows: a section's start and a change begin a row; a row holds n bars
    const rows = useMemo(() => {
        const sectionAtBar = new Map(piece.sections.map(s => [s.from, s]));
        const changeAtBar = new Map(piece.changes.map(c => [c.bar, c]));
        const out: { heading?: string; change?: string[]; bars: Bar[] }[] = [];
        let row: Bar[] = [];
        piece.bars.forEach(bar => {
            const section = sectionAtBar.get(bar.index), change = changeAtBar.get(bar.index);
            if (section || change || row.length === n) {
                if (row.length) out.push({ bars: row });
                row = [];
                if (section || change) out.push({
                    heading: section?.name,
                    change: change ? changeWords(change, bar.index === 0, bar.tempo?.beatUnit) : undefined,
                    bars: [],
                });
            }
            row.push(bar);
        });
        if (row.length) out.push({ bars: row });
        return out;
    }, [piece, n, changeWords]);

    // the bar and the beat that sound
    const barEls = useRef<(HTMLDivElement | null)[]>([]);
    const beatEls = useRef<(HTMLButtonElement | null)[][]>([]);
    const heads = useRef<(HTMLElement | null)[]>([]);
    const lit = useRef({ bar: -1, beat: null as HTMLButtonElement | null });
    const onTime = useCallback((position: number) => {
        const b = barAt(piece, position), was = lit.current;
        if (b !== was.bar) {
            barEls.current[was.bar]?.classList.remove('now');
            heads.current[was.bar]?.classList.remove('on');
            barEls.current[b]?.classList.add('now');
            heads.current[b]?.classList.add('on');
            was.bar = b;
        }
        let beat: HTMLButtonElement | null = null;
        if (b >= 0) {
            const bar = piece.bars[b]!, lead = bar.lacks?.end === 'start' ? bar.lacks.beats : 0, cells = bar.beats.length + (bar.lacks?.beats ?? 0);
            let k = 0;
            while (k + 1 < bar.beats.length && bar.beats[k + 1]! <= position) k++;
            beat = beatEls.current[b]?.[k] ?? null;
            const end = k + 1 < bar.beats.length ? bar.beats[k + 1]! : bar.end, start = bar.beats[k]!;
            const head = heads.current[b];
            if (head) head.style.left = `${(lead + k + Math.min(1, Math.max(0, (position - start) / Math.max(0.001, end - start)))) / cells * 100}%`;
        }
        if (beat !== was.beat) {
            was.beat?.classList.remove('now');
            beat?.classList.add('now');
            was.beat = beat;
        }
    }, [piece]);
    useAnalysisClock(onTime);

    // the names of a crowded bar: pulled back from its end, smaller, on two lines, without degrees (analysis/fit.ts)
    const view = useChordsView();
    useLayoutEffect(() => {
        for (const bar of barEls.current) {
            if (!bar) continue;
            bar.style.removeProperty('--glabel-fit');
            bar.classList.remove('labels-two', 'labels-nodeg');
            const labels = [...bar.querySelectorAll<HTMLElement>('.an-gpart .an-achord')];
            for (const label of labels) label.style.removeProperty('transform');
            if (!labels.length) continue;
            const box = bar.getBoundingClientRect();
            const names = labels.map(label => ({
                left: label.getBoundingClientRect().left - box.left,
                width: label.scrollWidth,
                degree: label.querySelector<HTMLElement>('.an-adeg')?.offsetWidth ?? 0,
            }));
            const plan = planLabels(names, bar.clientWidth);
            if (plan.fit < 1) bar.style.setProperty('--glabel-fit', String(plan.fit));
            bar.classList.toggle('labels-two', plan.two);
            bar.classList.toggle('labels-nodeg', !plan.degrees);
            labels.forEach((label, i) => {
                const dx = plan.lefts[i]! - names[i]!.left, up = plan.two && i % 2 === 0 ? -18 : 0;
                if (dx || up) label.style.transform = `translate(${dx}px, ${up}px)`;
            });
        }
    }, [rows, slots, view, width]);

    if (!piece.bars.length) return <p className="empty">{t('analysis.noBars')}</p>;
    return (
        <div className="an-gridv" ref={box} style={{ ['--per-row' as string]: n }}>
            {rows.map((row, r) => {
                if (!row.bars.length) return (
                    <div className="an-grow head" key={r}>
                        {row.heading && <b>{row.heading}</b>}
                        {row.change && <span className="an-gchange">{row.change.join(' · ')}</span>}
                    </div>
                );
                return (
                    <div className="an-grow" key={r}>
                        {row.bars.map((bar, i) => {
                            const lead = bar.lacks?.end === 'start' ? bar.lacks.beats : 0, trail = bar.lacks?.end === 'end' ? bar.lacks.beats : 0;
                            const first = slots[bar.index]![0]?.[0];
                            const held = i === 0 && first && !first.label ? partName(first.part) : null;
                            return (
                                <div className="an-gbar" key={bar.index} ref={el => { barEls.current[bar.index] = el; }}>
                                    <i className="an-gno">{bar.number}</i>
                                    {Array.from({ length: lead }, (_, e) => <span className="an-gempty" key={`l${e}`} />)}
                                    {slots[bar.index]!.map((parts, k) => (
                                        <button type="button" className="an-gbeat" key={k} ref={el => { (beatEls.current[bar.index] ??= [])[k] = el; }}
                                            onClick={() => analysisPlayer.playFrom(parts[0]!.time)} title={bar.fermatas.includes(k + 1) ? t('analysis.fermata') : undefined}>
                                            {bar.fermatas.includes(k + 1) && <span className="an-gferm" aria-hidden="true">𝄐</span>}
                                            {parts.map((slot, j) => (
                                                <span className="an-gpart" key={j} style={{ flexGrow: slot.share }}
                                                    onClick={j > 0 ? e => { e.stopPropagation(); analysisPlayer.playFrom(slot.time); } : undefined}>
                                                    {slot.label ? <ChordLabel name={slot.label} degree={slot.degree} />
                                                        : k === 0 && j === 0 && held ? <span className="an-gheld"><ChordLabel name={held} degree={slot.degree} /></span> : null}
                                                </span>
                                            ))}
                                        </button>
                                    ))}
                                    {Array.from({ length: trail }, (_, e) => <span className="an-gempty" key={`t${e}`} />)}
                                    <span className="an-ghead" ref={el => { heads.current[bar.index] = el; }} />
                                </div>
                            );
                        })}
                    </div>
                );
            })}
        </div>
    );
}
