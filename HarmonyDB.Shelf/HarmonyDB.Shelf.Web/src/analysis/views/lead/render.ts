// Draws a laid-out sheet as SVG, one <svg> for every system. Colours come from the stylesheet (lead.css), by class.
//
//   renderSheet(engraving, music, beatLabel) → { systems, bars }
//     systems   the <svg> elements, in order
//     bars      for every bar: { background, beats: [{ background, marks }] } — marks: the written notes and rests that
//               sound through the beat (a note that lasts four beats is among the marks of each of them)
//
// Every beat has a target (ls-hit) that is pressed with the pointer, or reached with Tab and pressed with Enter or Space;
// its data-t is the beat's time.

import { GLYPH, LINE, MUSIC_FONT } from './glyphs';
import type { DrawnPart, Engraving, System, TextRun } from './layout';
import type { Music } from './music';

const SVG = 'http://www.w3.org/2000/svg';

export interface BarRef {
    background: SVGRectElement;
    beats: { background: SVGRectElement; marks: SVGGElement[] }[];
}

function r(x: number): string {
    return String(Math.round(x * 100) / 100);
}

function el<K extends keyof SVGElementTagNameMap>(name: K, attrs: Record<string, string | number>, parent?: Element): SVGElementTagNameMap[K] {
    const node = document.createElementNS(SVG, name);
    for (const [k, v] of Object.entries(attrs)) node.setAttribute(k, typeof v === 'number' ? r(v) : v);
    parent?.appendChild(node);
    return node;
}

function glyph(parent: Element, ch: string, x: number, y: number, px: number, cls?: string): SVGTextElement {
    const node = el('text', { x, y, 'font-family': MUSIC_FONT, 'font-size': px }, parent);
    if (cls) node.setAttribute('class', cls);
    node.textContent = ch;
    return node;
}

/** A text made of runs: plain text, and ♭ ♯ 𝄫 𝄪 from the music font. */
function runsText(parent: Element, runs: TextRun[], x: number, y: number, px: number, weight: number, cls?: string): SVGTextElement {
    const node = el('text', { y, 'font-size': px, 'font-weight': weight }, parent);
    if (cls) node.setAttribute('class', cls);
    for (const run of runs) {
        const span = el('tspan', { x: x + run.x }, node);
        if (run.sign) {
            span.setAttribute('font-family', MUSIC_FONT);
            span.setAttribute('font-weight', '400');
        }
        span.textContent = run.text;
    }
    return node;
}

function plainText(parent: Element, text: string, x: number, y: number, px: number, weight: number, cls?: string): SVGTextElement {
    const node = el('text', { x, y, 'font-size': px, 'font-weight': weight }, parent);
    if (cls) node.setAttribute('class', cls);
    node.textContent = text;
    return node;
}

function part(parent: Element, p: DrawnPart, music: number): void {
    if (p.type === 'glyph') glyph(parent, GLYPH[p.glyph].ch, p.x, p.y, music);
    else if (p.type === 'text') glyph(parent, p.text, p.x, p.y, music);
    else el('rect', { class: 'ls-solid', x: p.x, y: p.y, width: p.w, height: p.h }, parent);
}

function renderSystem(system: System, sheet: Engraving, music: Music, bars: BarRef[], words: SheetWords): SVGSVGElement {
    const sizes = sheet.sizes, S = sizes.space;
    const svg = el('svg', { class: 'ls-sys', width: system.width, height: system.height, viewBox: `0 0 ${r(system.width)} ${r(system.height)}` });
    const rowH = system.bottom - system.top;
    const backgrounds = el('g', {}, svg), drawing = el('g', {}, svg), hits = el('g', {}, svg);

    for (let i = 0; i < 5; i++) {
        const y = system.staffTop + i * S;
        el('line', { class: 'ls-staff', x1: 0, x2: system.width, y1: y, y2: y, 'stroke-width': LINE.staff * S }, drawing);
    }
    glyph(drawing, GLYPH[system.clef.glyph].ch, system.clef.x, system.clef.y, sizes.music);
    for (const k of system.keySigns) glyph(drawing, GLYPH[k.glyph].ch, k.x, k.y, sizes.music);

    const barline = (x: number, w: number) => el('rect', { class: 'ls-solid', x, y: system.staffTop - LINE.staff * S / 2, width: w, height: 4 * S + LINE.staff * S }, drawing);
    const thin = LINE.thin * S, thick = LINE.thick * S, gap = LINE.separation * S;

    system.bars.forEach((bar, bi) => {
        const mbar = music.bars[bar.index]!, lastInSystem = bi === system.bars.length - 1;
        const ref: BarRef = {
            background: el('rect', { class: 'ls-barbg', x: bar.left, y: system.top, width: bar.right - bar.left, height: rowH }, backgrounds),
            beats: [],
        };
        for (let k = 0; k < bar.count; k++) {
            const x0 = k === 0 ? bar.left : bar.beatX[k]!, x1 = k === bar.count - 1 ? bar.right : bar.beatX[k + 1]!;
            const box = { x: x0, y: system.top, width: x1 - x0, height: rowH };
            ref.beats.push({ background: el('rect', { class: 'ls-beatbg', ...box }, backgrounds), marks: [] });
            el('rect', { class: 'ls-hit', 'data-t': String(mbar.beats[k]!), tabindex: '0', role: 'button', 'aria-label': words.beat(mbar.number, k + 1), ...box }, hits);
        }
        // a key that changes with the bar: the naturals of the old signature, and the new one
        for (const k of bar.keySigns) glyph(drawing, GLYPH[k.glyph].ch, k.x, k.y, sizes.music);
        if (bar.signature) {
            glyph(drawing, bar.signature.top, bar.signature.topX, bar.signature.topY, sizes.music);
            glyph(drawing, bar.signature.bottom, bar.signature.bottomX, bar.signature.bottomY, sizes.music);
        }
        for (const m of bar.marks) {
            const group = el('g', { class: 'ls-mark' }, drawing);
            for (const p of m.parts) part(group, p, sizes.music);
            ref.beats.forEach((beat, k) => {
                if (m.pos < k + 1 - 1e-6 && m.pos + m.beats > k + 1e-6) beat.marks.push(group);
            });
        }
        if (bar.barline === 'final') {
            barline(bar.right - thick, thick);
            barline(bar.right - thick - gap - thin, thin);
        } else if (bar.barline === 'double') {
            barline(bar.right - thin, thin);
            barline(bar.right - thin - gap - thin, thin);
        } else barline(lastInSystem ? bar.right - thin : bar.right - thin / 2, thin);
        bars[bar.index] = ref;
    });
    for (const join of system.joins) {
        if (join.type === 'tie') el('path', { class: 'ls-solid', d: join.path }, drawing);
        else part(drawing, join, sizes.music);
    }
    for (const k of system.keyWarning?.signs ?? []) glyph(drawing, GLYPH[k.glyph].ch, k.x, k.y, sizes.music);
    if (system.warning) {
        const warn = system.warning;
        glyph(drawing, warn.top, warn.topX, warn.topY, sizes.music);
        glyph(drawing, warn.bottom, warn.bottomX, warn.bottomY, sizes.music);
    }

    for (const c of system.chords) runsText(drawing, c.runs, c.x, c.y, sizes.chord, 600, c.reminder ? 'ls-soft' : undefined);
    for (const w of system.words) plainText(drawing, w.text, w.x, w.y, sizes.lyric, 400);
    if (system.section) {
        const s = system.section;
        el('rect', { class: 'ls-section', x: s.x + 0.5, y: s.y + 0.5, width: s.w, height: s.h, rx: 0.3 * S, 'stroke-width': LINE.thin * S }, drawing);
        runsText(drawing, s.runs, s.textX, s.textY, sizes.section, 700);
    }
    if (system.number) plainText(drawing, system.number.text, system.number.x, system.number.y, sizes.number, 400, 'ls-soft');
    // a tempo mark: the note the tempo counts, and the number
    for (const t of system.tempos) {
        const note = t.unit === 2 ? GLYPH.metHalf : t.unit === 8 ? GLYPH.metEighth : GLYPH.metQuarter;
        const unit = sizes.tempoNote / 4;
        let tx = t.x + note.w * unit;
        glyph(drawing, note.ch, t.x, t.y, sizes.tempoNote);
        if (t.dotted) {
            glyph(drawing, GLYPH.metDot.ch, tx + 0.25 * unit, t.y, sizes.tempoNote);
            tx += (0.25 + GLYPH.metDot.w) * unit;
        }
        plainText(drawing, `= ${t.bpm}`, tx + 0.5 * S, t.y, sizes.tempo, 400);
    }
    for (const t of system.texts) plainText(drawing, words.free, t.x, t.y, sizes.tempo, 400).setAttribute('font-style', 'italic');
    return svg;
}

/** The words the sheet writes: over a free bar, and the name of a beat's target. */
export interface SheetWords {
    free: string;
    beat: (bar: number, beat: number) => string;
}

export function renderSheet(sheet: Engraving, music: Music, words: SheetWords): { systems: SVGSVGElement[]; bars: BarRef[] } {
    const bars: BarRef[] = [];
    const systems = sheet.systems.map(system => renderSystem(system, sheet, music, bars, words));
    return { systems, bars };
}
