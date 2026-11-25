// The chords on the time line: the sections in a band on top, the bar numbers, the waveform with the bar lines, a block
// per chord, where the key, the meter or the tempo changes, the loop and the place that sounds.
//
// A tap goes there and plays; with the mouse, a drag over the waveform makes a loop (from beat to beat); with a finger,
// holding still for a moment and then dragging does. Two fingers, or Ctrl and the wheel, zoom. While the music plays the
// view follows it, unless it was just scrolled by hand.

import { useCallback, useEffect, useLayoutEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { player, useClock, usePlayer } from '../../player/controller';
import { chordAt, sectionAt, type Piece } from '../piece';
import { ChordLabel, useChangeWords } from '../marks';
import { useRedraw } from '../redraw';
import { fitName } from '../fit';
import { useChordsView } from '../../chords/view';

const MIN_ZOOM = 8, MAX_ZOOM = 300, DEFAULT_ZOOM = 40;    // px a second
const SECTIONS_H = 22, RULER_H = 18, WAVE_H = 84, GAP = 6, CHORDS_H = 46;
const HOLD_MS = 2500;       // after a scroll by hand, the view does not follow for this long
const PRESS_MS = 450, SLOP = 8, DRAG_PX = 4;
const zoomKey = 'shelf.analysis.zoom';

function storedZoom(): number {
    try {
        const value = Number(localStorage.getItem(zoomKey));
        return value >= MIN_ZOOM && value <= MAX_ZOOM ? value : DEFAULT_ZOOM;
    } catch {
        return DEFAULT_ZOOM;
    }
}

/** The nearest beat to a time. */
function snap(piece: Piece, t: number): number {
    let best = t, gap = Infinity;
    for (const beat of piece.beats) {
        const d = Math.abs(beat.t - t);
        if (d < gap) {
            gap = d;
            best = beat.t;
        }
    }
    for (const bar of piece.bars) if (Math.abs(bar.end - t) < gap) { gap = Math.abs(bar.end - t); best = bar.end; }
    return best;
}

export function TimeView({ piece }: { piece: Piece }) {
    const { t } = useTranslation();
    const state = usePlayer();
    const changeWords = useChangeWords();
    const [pps, setPps] = useState(storedZoom);
    const scroller = useRef<HTMLDivElement>(null);
    const canvas = useRef<HTMLCanvasElement>(null);
    const playhead = useRef<HTMLDivElement>(null);
    const chordEls = useRef<(HTMLButtonElement | null)[]>([]);
    const sectionEls = useRef<(HTMLButtonElement | null)[]>([]);
    const lit = useRef({ chord: -1, section: -1, last: -1 });
    const holdUntil = useRef(0);
    const [drawn, setDrawn] = useState<{ start: number; end: number } | null>(null);
    const width = Math.ceil(piece.duration * pps);
    const top = piece.sections.length ? SECTIONS_H : 0;

    const zoom = useCallback((value: number, anchorTime?: number, anchorX?: number) => {
        const box = scroller.current;
        const next = Math.max(MIN_ZOOM, Math.min(MAX_ZOOM, Math.round(value)));
        if (!box) return;
        const x = anchorX ?? box.clientWidth * 0.3;
        const at = anchorTime ?? (box.scrollLeft + x) / pps;
        setPps(next);
        try {
            localStorage.setItem(zoomKey, String(next));
        } catch {
            // kept until the page closes
        }
        requestAnimationFrame(() => { if (scroller.current) scroller.current.scrollLeft = Math.max(0, at * next - x); });
    }, [pps]);

    // the waveform: only the part in sight, a column a device pixel
    const draw = useCallback(() => {
        const box = scroller.current, cv = canvas.current, peaks = player.peaks;
        if (!box || !cv) return;
        const ratio = window.devicePixelRatio || 1, vw = box.clientWidth;
        const w = Math.max(1, Math.round(vw * ratio)), h = Math.round(WAVE_H * ratio);
        if (cv.width !== w || cv.height !== h) {
            cv.width = w;
            cv.height = h;
        }
        cv.style.width = `${vw}px`;
        cv.style.left = `${box.scrollLeft}px`;
        const ctx = cv.getContext('2d')!;
        ctx.clearRect(0, 0, w, h);
        const from = box.scrollLeft / pps, to = (box.scrollLeft + vw) / pps;
        const styles = getComputedStyle(box);
        ctx.fillStyle = styles.getPropertyValue('--line');
        for (const bar of piece.bars) {
            if (bar.end < from || bar.start > to) continue;
            ctx.fillRect(Math.round((bar.start - from) * pps * ratio), 0, Math.max(1, Math.round(ratio)), h);
        }
        ctx.globalAlpha = 0.5;
        peaks?.draw(ctx, { x: 0, y: 4 * ratio, width: w, height: h - 8 * ratio, from, to, color: styles.getPropertyValue('--muted') });
        ctx.globalAlpha = 1;
    }, [piece, pps]);

    useLayoutEffect(() => draw(), [draw, state.wave, width]);
    useRedraw(scroller, draw);

    // a name wider than its block is written smaller, its degree left out, a slash chord on two lines (analysis/fit.ts);
    // measured at its own size
    const view = useChordsView();
    useLayoutEffect(() => {
        const blocks = chordEls.current.filter((el): el is HTMLButtonElement => !!el);
        for (const el of blocks) {
            el.style.removeProperty('--fit');
            el.classList.remove('fit-nodeg', 'fit-two');
        }
        const sizes = blocks.map(el => ({ el, label: el.querySelector<HTMLElement>('.an-achord'), degree: el.querySelector<HTMLElement>('.an-adeg') }))
            .map(x => ({ ...x, width: x.label?.scrollWidth ?? 0, degreeWidth: x.degree?.offsetWidth ?? 0, room: x.el.clientWidth - 12 }));
        for (const { el, width, degreeWidth, room } of sizes) {
            if (!width) continue;
            const plan = fitName(width, degreeWidth, room);
            if (plan.fit < 1) el.style.setProperty('--fit', String(plan.fit));
            el.classList.toggle('fit-nodeg', !plan.degrees);
            // a slash chord that does not fit even so stands on two lines, the bass under it
            el.classList.toggle('fit-two', plan.left && (el.textContent ?? '').includes('/'));
        }
    }, [piece, pps, view]);

    // the place that sounds: the playhead, the chord and the section lit, the view following
    const onTime = useCallback((position: number, playing: boolean) => {
        const box = scroller.current;
        if (playhead.current) playhead.current.style.transform = `translateX(${position * pps}px)`;
        const chord = chordAt(piece, position), section = sectionAt(piece, position), was = lit.current;
        if (chord !== was.chord) {
            chordEls.current[was.chord]?.classList.remove('now');
            chordEls.current[chord]?.classList.add('now');
            was.chord = chord;
        }
        if (section !== was.section) {
            sectionEls.current[was.section]?.classList.remove('now');
            sectionEls.current[section]?.classList.add('now');
            was.section = section;
        }
        const jumped = Math.abs(position - was.last) > 0.5;
        was.last = position;
        if (box && ((playing && performance.now() > holdUntil.current) || (!playing && jumped))) {
            const x = position * pps, left = box.scrollLeft, vw = box.clientWidth;
            if (x < left + vw * 0.1 || x > left + vw * 0.8) box.scrollLeft = Math.max(0, x - vw * 0.3);
        }
    }, [piece, pps]);
    useClock(onTime);

    // ---- pointers: a tap goes there; a drag (the mouse) or a hold and drag (a finger) makes a loop --------------
    const timeAt = (clientX: number) => {
        const box = scroller.current!;
        return Math.max(0, Math.min(piece.duration, (clientX - box.getBoundingClientRect().left + box.scrollLeft) / pps));
    };
    const press = useRef<{ x: number; y: number; t: number; drawing: boolean; moved: boolean; timer: ReturnType<typeof setTimeout> | null; id: number; touch: boolean } | null>(null);

    const onPointerDown = (e: React.PointerEvent<HTMLDivElement>) => {
        if (e.button !== 0) return;
        holdUntil.current = performance.now() + HOLD_MS;
        const touch = e.pointerType === 'touch';
        const p = { x: e.clientX, y: e.clientY, t: snap(piece, timeAt(e.clientX)), drawing: !touch, moved: false, timer: null as ReturnType<typeof setTimeout> | null, id: e.pointerId, touch };
        press.current = p;
        if (!touch) e.currentTarget.setPointerCapture(e.pointerId);
        else p.timer = setTimeout(() => {
            if (press.current !== p || p.moved) return;
            p.drawing = true;
            setDrawn({ start: p.t, end: p.t });
            navigator.vibrate?.(12);
        }, PRESS_MS);
    };
    const onPointerMove = (e: React.PointerEvent<HTMLDivElement>) => {
        const p = press.current;
        if (!p || p.id !== e.pointerId) return;
        const far = Math.hypot(e.clientX - p.x, e.clientY - p.y);
        if (!p.drawing) {
            if (far > SLOP) {
                p.moved = true;
                if (p.timer) clearTimeout(p.timer);
            }
            return;
        }
        if (far < DRAG_PX && !p.moved) return;
        p.moved = true;
        const at = snap(piece, timeAt(e.clientX));
        setDrawn({ start: Math.min(p.t, at), end: Math.max(p.t, at) });
    };
    const onPointerUp = (e: React.PointerEvent<HTMLDivElement>) => {
        const p = press.current;
        if (!p || p.id !== e.pointerId) return;
        press.current = null;
        if (p.timer) clearTimeout(p.timer);
        setDrawn(null);
        if (p.drawing && p.moved) {
            const at = snap(piece, timeAt(e.clientX));
            const start = Math.min(p.t, at), end = Math.max(p.t, at);
            if (end - start >= 0.1) player.setLoop(start, end);
            return;
        }
        if (!p.moved && e.type === 'pointerup') player.playFrom(timeAt(e.clientX));
    };

    // two fingers zoom; Ctrl and the wheel too
    useEffect(() => {
        const box = scroller.current;
        if (!box) return;
        let pinch: { spread: number; pps: number; time: number; x: number } | null = null;
        const spread = (a: Touch, b: Touch) => Math.hypot(a.clientX - b.clientX, a.clientY - b.clientY);
        const start = (e: TouchEvent) => {
            if (e.touches.length !== 2) return;
            const [a, b] = [e.touches[0]!, e.touches[1]!];
            const x = (a.clientX + b.clientX) / 2 - box.getBoundingClientRect().left;
            pinch = { spread: Math.max(1, spread(a, b)), pps, time: (box.scrollLeft + x) / pps, x };
        };
        const move = (e: TouchEvent) => {
            if (!pinch || e.touches.length !== 2) return;
            e.preventDefault();
            zoom(pinch.pps * spread(e.touches[0]!, e.touches[1]!) / pinch.spread, pinch.time, pinch.x);
        };
        const end = (e: TouchEvent) => { if (e.touches.length < 2) pinch = null; };
        const wheel = (e: WheelEvent) => {
            holdUntil.current = performance.now() + HOLD_MS;
            if (!e.ctrlKey && !e.metaKey) return;
            e.preventDefault();
            const x = e.clientX - box.getBoundingClientRect().left;
            zoom(pps * Math.pow(1.0018, -e.deltaY), (box.scrollLeft + x) / pps, x);
        };
        box.addEventListener('touchstart', start, { passive: true });
        box.addEventListener('touchmove', move, { passive: false });
        box.addEventListener('touchend', end);
        box.addEventListener('wheel', wheel, { passive: false });
        return () => {
            box.removeEventListener('touchstart', start);
            box.removeEventListener('touchmove', move);
            box.removeEventListener('touchend', end);
            box.removeEventListener('wheel', wheel);
        };
    }, [pps, zoom]);

    const loop = drawn ?? state.loop;
    const every = (() => {
        const barPx = piece.bars.length ? piece.duration / piece.bars.length * pps : 0;
        return barPx >= 26 ? 1 : barPx >= 13 ? 2 : barPx >= 7 ? 4 : 8;
    })();
    const firstChange = piece.changes[0];

    return (
        <div className="an-tview">
            <div className="an-tvtools">
                <span className="muted small">{t('analysis.timeHint')}</span>
                <span className="step">
                    <button type="button" aria-label={t('analysis.zoomOut')} onClick={() => zoom(pps / 1.4)}>−</button>
                    <button type="button" aria-label={t('analysis.zoomIn')} onClick={() => zoom(pps * 1.4)}>+</button>
                </span>
            </div>
            <div className="an-tlv" ref={scroller} onScroll={() => draw()} role="group" aria-label={t('analysis.views.time')}>
                <div className="an-tlin" style={{ width, height: top + RULER_H + WAVE_H + GAP + CHORDS_H }}
                    onPointerDown={onPointerDown} onPointerMove={onPointerMove} onPointerUp={onPointerUp} onPointerCancel={onPointerUp}
                    onContextMenu={e => { if (press.current?.touch) e.preventDefault(); }}>
                    {top > 0 && (
                        <div className="an-tsec">
                            {piece.sections.map((s, i) => (
                                <button type="button" key={i} ref={el => { sectionEls.current[i] = el; }} className={`an-sk an-sk-${s.label ?? 'other'}`}
                                    style={{ left: s.start * pps, width: Math.max(2, (s.end - s.start) * pps - 2) }} title={s.name}
                                    onPointerDown={e => e.stopPropagation()} onClick={() => player.playFrom(s.start)}>{s.name}</button>
                            ))}
                        </div>
                    )}
                    <div className="an-truler" style={{ top }}>
                        {piece.bars.map(bar => (bar.index % every === 0
                            ? <span key={bar.index} className="an-tbar" style={{ left: bar.start * pps }}>{bar.number}</span>
                            : null))}
                    </div>
                    <canvas ref={canvas} className="an-twave" style={{ top: top + RULER_H, height: WAVE_H }} />
                    {piece.changes.map((change, i) => {
                        const bar = piece.bars[change.bar]!;
                        const words = changeWords(change, change === firstChange, bar.tempo?.beatUnit);
                        return (
                            <div key={i} className={i === 0 ? 'an-tkey first' : 'an-tkey'} style={{ left: change.time * pps, top: top + RULER_H, height: WAVE_H + GAP }}>
                                <span>{words.join(' · ')}</span>
                            </div>
                        );
                    })}
                    {loop && <div className={drawn ? 'an-tloop drawing' : 'an-tloop'} style={{ left: loop.start * pps, width: (loop.end - loop.start) * pps, top: top + RULER_H, height: WAVE_H }} />}
                    <div className="an-tchords" style={{ top: top + RULER_H + WAVE_H + GAP, height: CHORDS_H }}>
                        {piece.chords.map((c, i) => (
                            <button type="button" key={i} ref={el => { chordEls.current[i] = el; }} className={c.name ? 'an-tch' : 'an-tch nc'}
                                style={{ left: c.start * pps, width: Math.max(2, (c.end - c.start) * pps - 2) }}
                                title={c.name ?? ''} onPointerDown={e => e.stopPropagation()} onClick={() => player.playFrom(c.start)}>
                                {c.name ? <ChordLabel name={c.name} degree={c.degree} /> : c.kind === 'stop' ? '‖' : 'N.C.'}
                            </button>
                        ))}
                    </div>
                    <div className="an-phead" ref={playhead} />
                </div>
            </div>
            {!state.wave && <p className="muted small">{t('analysis.waveLoading')}</p>}
        </div>
    );
}
