// The roll itself: pitch upwards, time to the right. A keyboard at the left edge, the sections, the bar numbers and the
// chords along the top, the notes of the parts shown in their colours, the drums in a lane of their own along the
// bottom — a row for each kind of drum that occurs, named at the left, a mark for each hit — the loop and the playhead.
// It knows nothing of the app: it is given what to show and tells what the person did.
//
//   const roll = createRoll(element, options)
//   roll.setSong({ data, length, bars, beats, chords }), roll.setSections(sections), roll.setShown(ids)
//   roll.setZoom(pps), roll.setRow(px | null: fitted), roll.zoom, roll.row (wanted), roll.rowShown
//   roll.setTime(t, playing), roll.setLoop({ start, end } | null)
//   roll.setTexts(texts), roll.redraw() (the colours changed, or the size), roll.refresh() (the names changed)
//   roll.destroy()
//
// Two canvases lie over each other and stay in place while the element scrolls under them: the lower one has what
// changes only with scrolling, zooming and the data, the upper one what moves with the music — the playhead, the notes
// and keys sounding now, the chord sounding now, the note under the pointer. Both are drawn in device pixels, for the
// part in sight only.
//
// A note is a rectangle from its start to its end, a pixel shorter than its time and its row, so that a repeated note
// and a neighbouring semitone stay apart, never thinner than two pixels. Its velocity is how strongly it is drawn: four
// steps of its part's colour mixed with the surface. A part that plays one note at a time is drawn as a line, each note
// joined to the next when it follows closely.

import type { Bar, Chord, Section } from '../../piece';
import type { DrumKindId } from './drums';
import { firstFrom, noteAt, pitchRange, soundingAt, startingIn, NONE, type DrumLane, type Lane, type RollNotes } from './lanes';

export const MIN_ZOOM = 8, MAX_ZOOM = 300;          // pixels a second: the time line's limits (the zoom is shared with it)
export const MIN_ROW = 5, MAX_ROW = 28;             // pixels a semitone when the person sets it
const FIT_MIN_ROW = 6, FIT_MAX_ROW = 22;            // … and when the range is fitted to the height
const RULER_H = 20, CHORDS_H = 26, SECTIONS_H = 22;
const KEYS_W = 48;
const RANGE_MARGIN = 2;                             // semitones of air above and below what is played
const HOLD_MS = 2500;                               // after a scroll by hand, the roll does not follow for this long
const DRAG_PX = 5, PRESS_MS = 380, PRESS_SLOP = 10; // a drag; a finger held still this long draws a loop
// The parts heard in the recording's separate instruments: the lines a musician follows lie on top.
const STEM_ORDER = ['drums', 'other', 'piano', 'guitar', 'bass', 'vocals'];
// A drum's row, px; lower where the drum lane would take more than this share of the roll — never lower than the least.
// Where a row is lower than its name, the names stand on every so many rows; a hit names itself under the pointer.
const DRUM_ROW = 13, DRUM_ROW_LEAST = 3, DRUM_SHARE = 0.4;
const DRUM_FONT = 10;
const DRUM_GAP = 3;
const DRUM_FLASH = 0.12;                            // a hit is drawn as sounding for this long, seconds
const BLACK = [false, true, false, true, false, false, true, false, true, false, true, false];
const LEVELS = 4;
const ALPHA = [0.5, 0.67, 0.84, 1];
/** The parts' colours, by their place (lanes.ts, LANE_COLOURS), and the drums'. */
const LANE_TOKENS = ['--accent', '--here', '--ok', '--danger', '--chord-ink'];
const DRUMS_TOKEN = '--muted';

type Rgb = [number, number, number];

const clamp = (v: number, lo: number, hi: number) => Math.min(hi, Math.max(lo, v));

let normaliser: CanvasRenderingContext2D | null = null;

/** A colour as a token gives it (#rgb, #rrggbb, rgb(…), a name …): its red, green and blue, as a canvas reads it. */
function rgbOf(color: string): Rgb {
    normaliser ??= document.createElement('canvas').getContext('2d')!;
    normaliser.fillStyle = '#000000';
    normaliser.fillStyle = color.trim();
    const written = String(normaliser.fillStyle);
    const hex = /^#([0-9a-f]{2})([0-9a-f]{2})([0-9a-f]{2})$/i.exec(written);
    if (hex) return [parseInt(hex[1]!, 16), parseInt(hex[2]!, 16), parseInt(hex[3]!, 16)];
    const rgb = /^rgba?\((\d+),\s*(\d+),\s*(\d+)/.exec(written);
    if (rgb) return [Number(rgb[1]), Number(rgb[2]), Number(rgb[3])];
    throw new Error(`the roll's colour "${color}" cannot be read`);
}
const luminance = (c: Rgb) => {
    const f = (v: number) => {
        v /= 255;
        return v <= 0.03928 ? v / 12.92 : Math.pow((v + 0.055) / 1.055, 2.4);
    };
    return 0.2126 * f(c[0]) + 0.7152 * f(c[1]) + 0.0722 * f(c[2]);
};
const contrast = (a: Rgb, b: Rgb) => {
    const la = luminance(a), lb = luminance(b);
    return (Math.max(la, lb) + 0.05) / (Math.min(la, lb) + 0.05);
};
const mix = (a: Rgb, b: Rgb, share: number): Rgb => [0, 1, 2].map(i => Math.round(a[i]! * (1 - share) + b[i]! * share)) as Rgb;
const css = (c: Rgb, alpha = 1) => (alpha === 1 ? `rgb(${c[0]},${c[1]},${c[2]})` : `rgba(${c[0]},${c[1]},${c[2]},${alpha})`);

/** How loud a note is, as one of LEVELS steps: 0 the softest; a note without a velocity is drawn whole. */
function levelOf(velocity: number): number {
    return velocity === NONE ? LEVELS - 1 : clamp(Math.floor((velocity - 32) / ((96 - 32) / LEVELS)), 0, LEVELS - 1);
}

export interface RollTexts {
    drum: (id: DrumKindId) => string;
    drums: string;
    velocity: (value: number) => string;
    seconds: (value: string) => string;
    chords: string;
    noChord: string;
    sections: string;
    loopStart: string;
    loopEnd: string;
    loopStartTitle: string;
    loopEndTitle: string;
}

export interface RollOptions {
    /** how a pitch is written at a time (a note's: its start; the keyboard's: the playhead) */
    noteName: (midi: number, t: number) => string;
    chordText: (chord: Chord) => string;
    onSeek: (t: number) => void;
    onChord: (chord: Chord) => void;
    onSection: (section: Section) => void;
    onLoop: (start: number, end: number) => void;
    onZoom: (zoom: { pps: number; row: number | null }) => void;
    minLoop: number;
    texts: RollTexts;
}

export interface RollSong {
    data: RollNotes;
    length: number;
    bars: Bar[];
    beats: number[];
    chords: Chord[];
}

interface LaneColours { colour: Rgb; css: string; fills: string[]; text: string; line: string }

interface Palette {
    lanes: Map<string, LaneColours>;
    surface: string; ink: string;
    rowDark: string; octave: string; bar: string; beat: string;
    head: string; muted: string; border: string;
    keyWhite: string; keyBlack: string; keyText: string;
    chordBg: string; chordText: string; chordActiveBg: string; chordActiveText: string;
    playhead: string; loop: string; loopSoft: string;
    sans: string; mono: string;
}

interface Region { start: number; end: number | null }
interface Marked { lane: Lane | DrumLane; index: number }
interface At { x: number; y: number; t: number; pitch: number; drum: number; zone: 'keys' | 'sections' | 'ruler' | 'chords' | 'drums' | 'roll' }

export interface Roll {
    setSong(song: RollSong): void;
    setSections(sections: Section[]): void;
    setShown(ids: Iterable<string>): void;
    setZoom(pps: number): void;
    setRow(row: number | null): void;
    readonly zoom: number;
    readonly row: number | null;
    readonly rowShown: number;
    setTime(t: number, playing: boolean): void;
    setLoop(loop: { start: number; end: number } | null): void;
    setTexts(texts: RollTexts): void;
    redraw(): void;
    refresh(): void;
    destroy(): void;
}

export function createRoll(element: HTMLElement, options: RollOptions): Roll {
    const { noteName, chordText, onSeek, onChord, onSection, onLoop, onZoom, minLoop } = options;
    let texts = options.texts;
    let data: RollNotes | null = null, length = 0, bars: Bar[] = [], beats: number[] = [], chords: Chord[] = [];
    let shown = new Set<string>();
    let pps = 40, rowWanted: number | null = null, row = 12, low = 48, high = 72;
    let sections: Section[] = [], sectionEls: HTMLButtonElement[] = [], activeSection = -1;
    let vw = 0, vh = 0, ratio = 1, keysW = KEYS_W, topH = 0, headH = RULER_H;
    let drumsH = 0, drumRow = DRUM_ROW;
    let time = 0, playing = false, loop: Region | null = null, draft: Region | null = null;
    let hover: Marked | null = null, pinned: Marked | null = null, pinTimer: ReturnType<typeof setTimeout> | undefined;
    let holdUntil = 0, baseQueued = false, overQueued = false, destroyed = false, centred = false;
    let palette: Palette | null = null, activeChord = -1;
    const widths = new Map<string, number>();

    const base = document.createElement('canvas'), over = document.createElement('canvas');
    base.className = 'rl-canvas';
    over.className = 'rl-canvas rl-over';
    const handles = [0, 1].map(() => {
        const el = document.createElement('div');
        el.className = 'rl-handle';
        el.setAttribute('role', 'slider');
        el.tabIndex = 0;
        el.hidden = true;
        return el;
    });
    handles[1]!.classList.add('rl-handle-end');
    const tip = document.createElement('div');
    tip.className = 'rl-tip';
    tip.hidden = true;
    tip.setAttribute('role', 'status');
    const sectionsEl = document.createElement('div');
    sectionsEl.className = 'rl-sections';
    sectionsEl.setAttribute('role', 'group');
    sectionsEl.hidden = true;
    // The chord strip is drawn on the canvas, where a pointer presses a chord. For the keyboard it is one stop over the
    // strip: the arrows go to the chord before and to the next one.
    const strip = document.createElement('div');
    strip.className = 'rl-strip';
    strip.hidden = true;
    strip.tabIndex = 0;
    strip.setAttribute('role', 'slider');
    strip.setAttribute('aria-valuemin', '1');
    let went: { from: number; to: number } | null = null;
    strip.addEventListener('keydown', event => {
        const way = event.key === 'ArrowLeft' ? -1 : event.key === 'ArrowRight' ? 1 : 0;
        if (!way || !chords.length) return;
        event.preventDefault();
        event.stopPropagation();
        // on: the next chord that starts; back: the start of the one that sounds, and from there the one before (a
        // second press may come before the roll is told the new place: it goes on from where the first went)
        const now = went && went.from === time ? went.to : time;
        let to = -1;
        if (way > 0) to = chords.findIndex(chord => chord.start > now + 0.001);
        else for (let i = chords.length - 1; i >= 0 && to < 0; i--) if (chords[i]!.start < now - 0.001) to = i;
        if (to < 0) return;
        went = { from: time, to: chords[to]!.start };
        onChord(chords[to]!);
    });
    const stage = document.createElement('div');
    stage.className = 'rl-stage';
    stage.append(base, over, sectionsEl, strip, ...handles, tip);
    const track = document.createElement('div');
    track.className = 'rl-track';
    track.append(stage);
    element.classList.add('rl-scroll');
    element.tabIndex = 0;
    element.append(track);

    function applyTexts() {
        sectionsEl.setAttribute('aria-label', texts.sections);
        strip.setAttribute('aria-label', texts.chords);
        handles[0]!.setAttribute('aria-label', texts.loopStart);
        handles[0]!.title = texts.loopStartTitle;
        handles[1]!.setAttribute('aria-label', texts.loopEnd);
        handles[1]!.title = texts.loopEndTitle;
        strip.removeAttribute('aria-valuetext');
    }
    applyTexts();

    const zoomOf = () => element.currentCSSZoom || 1;
    const named = () => Math.ceil((DRUM_FONT + 2) / drumRow);
    const px = (v: number) => Math.round(v * ratio);
    const timeX = (t: number) => keysW + t * pps - element.scrollLeft;
    const pitchY = (p: number) => headH + (high - p) * row - element.scrollTop;
    const rank = (lane: Lane) => (lane.stem ? STEM_ORDER.indexOf(lane.stem) : -1);
    /** The parts shown, in the order they are drawn: those of no separate instrument first, in their order. */
    const lanesShown = (): Lane[] => (!data ? [] : data.lanes.filter(l => shown.has(l.id)).sort((a, b) => rank(a) - rank(b)));
    const drumsShown = (): DrumLane | null => (data?.drums && data.drums.count && shown.has(data.drums.id) ? data.drums : null);
    const drumsTop = () => vh - drumsH + DRUM_GAP;

    // ---- colours: the design's tokens, read at drawing ------------------------------------------------
    function readPalette(): Palette {
        const style = getComputedStyle(element);
        const v = (name: string) => style.getPropertyValue(name).trim();
        const surface = rgbOf(v('--surface')), ink = rgbOf(v('--ink')), muted = rgbOf(v('--muted')), accent = rgbOf(v('--accent'));
        const lanes = new Map<string, LaneColours>();
        const colours = (colour: Rgb): LaneColours => ({
            colour, css: css(colour),
            fills: ALPHA.map(a => css(mix(surface, colour, a))),
            // the name inside a note: in whichever of ink and surface reads better on it
            text: contrast(colour, surface) >= contrast(colour, ink) ? css(surface) : css(ink),
            line: css(colour, 0.75),
        });
        for (const lane of data?.lanes ?? []) lanes.set(lane.id, colours(rgbOf(v(LANE_TOKENS[lane.colour]!))));
        if (data?.drums) lanes.set(data.drums.id, colours(muted));
        const shade = (share: number) => css(mix(surface, muted, share));
        palette = {
            lanes, surface: css(surface), ink: css(ink),
            rowDark: shade(0.09), octave: shade(0.45), bar: shade(0.38), beat: shade(0.14),
            head: v('--surface2'), muted: css(muted), border: shade(0.35),
            keyWhite: css(surface), keyBlack: shade(0.55), keyText: css(muted),
            chordBg: v('--surface2'), chordText: css(ink), chordActiveBg: css(accent), chordActiveText: v('--on-accent'),
            playhead: css(accent), loop: css(accent), loopSoft: css(accent, 0.16),
            sans: v('--sans'), mono: v('--mono'),
        };
        widths.clear();
        return palette;
    }
    const colours = () => palette ?? readPalette();

    function textWidth(ctx: CanvasRenderingContext2D, text: string, size: number): number {
        const key = size + '|' + text;
        let w = widths.get(key);
        if (w === undefined) {
            w = ctx.measureText(text).width;
            widths.set(key, w);
        }
        return w;
    }

    // ---- layout -----------------------------------------------------------------------------------
    function range() {
        const drawn = lanesShown();
        const used = pitchRange(drawn.length ? drawn : data ? data.lanes : []);
        if (!used) return { low: 48, high: 72 };
        return { low: clamp(used.low - RANGE_MARGIN, 0, 127), high: clamp(used.high + RANGE_MARGIN, 0, 127) };
    }

    /** The middle of what the parts shown play: where the roll is put when it is first shown. */
    function middlePitch(): number {
        const counts = new Uint32Array(128);
        let total = 0;
        for (const lane of lanesShown()) {
            for (let i = 0; i < lane.count; i++) {
                counts[lane.pitch[i]!]!++;
                total++;
            }
        }
        let seen = 0;
        for (let p = 0; p < 128 && total; p++) {
            seen += counts[p]!;
            if (seen * 2 >= total) return p;
        }
        return (low + high) / 2;
    }

    function layout(keep: { pitch: number; y: number } | null = null) {
        if (destroyed) return;
        // what stays where it is: the pitch in the middle of the roll (or the one given), unless nothing was laid out yet
        const anchor = keep ?? (centred ? { pitch: high - (element.scrollTop + (vh - headH - drumsH) / 2) / row, y: headH + (vh - headH - drumsH) / 2 } : null);
        vw = element.clientWidth;
        vh = element.clientHeight;
        ratio = (window.devicePixelRatio || 1) * zoomOf();
        keysW = KEYS_W;
        topH = sections.length ? SECTIONS_H : 0;
        headH = topH + RULER_H + (chords.length ? CHORDS_H : 0);
        // the drum lane: a row for each kind of drum in it, along the bottom; the keyboard's column is as wide as its names need
        const drums = drumsShown();
        drumRow = drums ? clamp(Math.floor((vh - headH) * DRUM_SHARE / drums.rows.length), DRUM_ROW_LEAST, DRUM_ROW) : DRUM_ROW;
        drumsH = drums ? drums.rows.length * drumRow + DRUM_GAP : 0;
        if (drums) {
            const ctx = base.getContext('2d')!;
            ctx.font = `600 ${DRUM_FONT}px ${colours().sans}`;
            keysW = Math.max(keysW, Math.ceil(Math.max(...drums.rows.map(r => ctx.measureText(texts.drum(r.id)).width)) + 12));
        }
        sectionsEl.hidden = !sections.length;
        sectionsEl.style.left = `${keysW}px`;
        strip.hidden = !chords.length;
        strip.style.left = `${keysW}px`;
        strip.style.top = `${topH + RULER_H}px`;
        strip.setAttribute('aria-valuemax', String(chords.length));
        for (const handle of handles) handle.style.top = `${topH}px`;
        const r = range();
        low = r.low;
        high = r.high;
        const rows = high - low + 1, room = Math.max(1, vh - headH - drumsH);
        row = rowWanted === null ? clamp(Math.floor(room / rows * 2) / 2, FIT_MIN_ROW, FIT_MAX_ROW) : rowWanted;
        stage.style.width = `${vw}px`;
        stage.style.height = `${vh}px`;
        track.style.width = `${Math.max(vw, keysW + Math.ceil(length * pps))}px`;
        track.style.height = `${Math.max(vh, headH + Math.ceil(rows * row) + drumsH)}px`;
        for (const canvas of [base, over]) {
            const w = Math.max(1, Math.round(vw * ratio)), h = Math.max(1, Math.round(vh * ratio));
            if (canvas.width !== w || canvas.height !== h) {
                canvas.width = w;
                canvas.height = h;
            }
            canvas.style.width = `${vw}px`;
            canvas.style.height = `${vh}px`;
        }
        if (data && vh > 0) {
            // … and unless that pitch is no longer on the roll (other parts are shown now)
            const to = anchor && anchor.pitch >= low && anchor.pitch <= high + 1 ? anchor : { pitch: middlePitch() + 0.5, y: headH + room / 2 };
            element.scrollTop = Math.max(0, (high - to.pitch) * row + headH - to.y);
            centred = true;
        }
        queueBase();
    }

    function queueBase() {
        if (baseQueued || destroyed) return;
        baseQueued = true;
        requestAnimationFrame(() => {
            baseQueued = false;
            if (destroyed) return;
            drawBase();
            drawOver();
        });
    }
    function queueOver() {
        if (overQueued || baseQueued || destroyed) return;
        overQueued = true;
        requestAnimationFrame(() => {
            overQueued = false;
            if (!destroyed && !baseQueued) drawOver();
        });
    }

    // ---- where a note is, in device pixels ------------------------------------------------------------
    const box = { x: 0, y: 0, w: 0, h: 0 };
    let seen = new Int32Array(5 * 1024);    // the notes of a part in sight while it is drawn: index, x, y, width, height
    function place(start: number, end: number, pitch: number) {
        const x0 = px(timeX(start)), y0 = px(pitchY(pitch));
        box.x = x0;
        box.y = y0;
        box.w = Math.max(px(2), px(timeX(end)) - x0 - px(1));
        box.h = Math.max(px(2), px(pitchY(pitch) + row) - y0 - px(1));
        return box;
    }
    const levelAt = (velocity: Uint8Array | null, i: number) => (velocity && data?.velocity ? levelOf(velocity[i]!) : LEVELS - 1);

    // ---- the lower canvas ---------------------------------------------------------------------------
    function drawBase() {
        const pal = colours();
        const ctx = base.getContext('2d')!, W = base.width, H = base.height;
        const kx = px(keysW), hy = px(headH);
        const from = element.scrollLeft / pps, to = (element.scrollLeft + vw - keysW) / pps;
        ctx.fillStyle = pal.surface;
        ctx.fillRect(0, 0, W, H);

        // rows: the black keys' rows are shaded, an octave begins at every line (above the drum lane)
        const dy = H - px(drumsH);
        ctx.save();
        ctx.beginPath();
        ctx.rect(kx, hy, W - kx, dy - hy);
        ctx.clip();
        const top = clamp(Math.ceil(high - element.scrollTop / row), low, high), bottom = clamp(Math.floor(high - (element.scrollTop + vh - headH - drumsH) / row), low, high);
        ctx.fillStyle = pal.rowDark;
        for (let p = top; p >= bottom; p--) if (BLACK[p % 12]) ctx.fillRect(kx, px(pitchY(p)), W - kx, px(pitchY(p) + row) - px(pitchY(p)));
        ctx.fillStyle = pal.octave;
        for (let p = top; p >= bottom; p--) if (p % 12 === 0) ctx.fillRect(kx, px(pitchY(p) + row) - 1, W - kx, Math.max(1, Math.round(ratio)));
        ctx.restore();

        ctx.save();
        ctx.beginPath();
        ctx.rect(kx, hy, W - kx, H - hy);
        ctx.clip();
        if (drumsH) drumLane(ctx, kx, W);

        // time: bars and beats, or seconds when there are no bars
        const line = Math.max(1, Math.round(ratio));
        if (bars.length) {
            const beatPx = beats.length > 1 ? (beats[beats.length - 1]! - beats[0]!) / (beats.length - 1) * pps : 0;
            if (beatPx >= 7) {
                ctx.fillStyle = pal.beat;
                for (let i = lower(beats, from); i < beats.length && beats[i]! <= to; i++) ctx.fillRect(px(timeX(beats[i]!)), hy, line, H - hy);
            }
            ctx.fillStyle = pal.bar;
            for (let i = Math.max(0, lowerBy(bars, from) - 1); i < bars.length && bars[i]!.start <= to; i++) ctx.fillRect(px(timeX(bars[i]!.start)), hy, line, H - hy);
        } else {
            const step = secondsStep();
            ctx.fillStyle = pal.bar;
            for (let t = Math.ceil(from / step) * step; t <= to; t += step) ctx.fillRect(px(timeX(t)), hy, line, H - hy);
        }

        // the loop
        const region = draft ?? loop;
        if (region && region.end !== null) {
            const x0 = px(timeX(region.start)), x1 = px(timeX(region.end));
            ctx.fillStyle = pal.loopSoft;
            ctx.fillRect(x0, hy, x1 - x0, H - hy);
        }

        // the notes, above the drum lane; the drums' hits in it
        if (drumsH) drumHits(ctx, from, to);
        ctx.save();
        ctx.beginPath();
        ctx.rect(kx, hy, W - kx, dy - hy);
        ctx.clip();
        const fontSize = clamp(Math.floor(row - 3), 8, 11);
        const labels = row >= 11;
        ctx.textBaseline = 'middle';
        ctx.font = `600 ${fontSize * ratio}px ${pal.sans}`;
        for (const lane of lanesShown()) {
            const c = pal.lanes.get(lane.id)!;
            const first = firstFrom(lane, from);
            if (lane.oneVoice) {
                // the line of one voice: each note is joined to the next one that follows closely
                ctx.strokeStyle = c.line;
                ctx.lineWidth = Math.max(1, Math.round(1.5 * ratio));
                ctx.beginPath();
                for (let i = Math.max(0, first - 1); i + 1 < lane.count && lane.start[i]! <= to; i++) {
                    if (!lane.joined[i] || lane.pitch[i] === lane.pitch[i + 1]) continue;
                    const a = place(lane.start[i]!, lane.end[i]!, lane.pitch[i]!), ax = a.x + a.w, ay = a.y + a.h / 2;
                    const b = place(lane.start[i + 1]!, lane.end[i + 1]!, lane.pitch[i + 1]!);
                    ctx.moveTo(ax, ay);
                    ctx.lineTo(Math.max(ax, b.x), b.y + b.h / 2);
                }
                ctx.stroke();
            }
            // The notes in sight are found once; each step of velocity is then one colour. Small notes are plain
            // rectangles (thousands of them cost little); the larger have round corners.
            const round = row >= 8 ? Math.min(px(3), px(row) / 3) : 0;
            let found = 0;
            for (let i = first; i < lane.count && lane.start[i]! <= to; i++) {
                if (lane.end[i]! < from) continue;
                const b = place(lane.start[i]!, lane.end[i]!, lane.pitch[i]!);
                if (b.y + b.h < hy || b.y > H) continue;
                if (5 * found === seen.length) {
                    const more = new Int32Array(seen.length * 2);
                    more.set(seen);
                    seen = more;
                }
                seen[5 * found] = i;
                seen[5 * found + 1] = b.x;
                seen[5 * found + 2] = b.y;
                seen[5 * found + 3] = b.w;
                seen[5 * found + 4] = b.h;
                found++;
            }
            for (let level = 0; level < LEVELS; level++) {
                ctx.fillStyle = c.fills[level]!;
                let rounded = false;
                for (let k = 0; k < 5 * found; k += 5) {
                    if (levelAt(lane.velocity, seen[k]!) !== level) continue;
                    const x = seen[k + 1]!, y = seen[k + 2]!, w = seen[k + 3]!, h = seen[k + 4]!;
                    if (round && w >= round * 3) {
                        if (!rounded) {
                            ctx.beginPath();
                            rounded = true;
                        }
                        ctx.roundRect(x, y, w, h, round);
                    } else ctx.fillRect(x, y, w, h);
                }
                if (rounded) ctx.fill();
            }
            if (labels) {
                ctx.fillStyle = c.text;
                const least = px(7) + fontSize * ratio;          // no name is narrower than this
                for (let k = 0; k < 5 * found; k += 5) {
                    // a long note that began left of the roll keeps its name in sight, while there is room for it
                    const x = Math.max(seen[k + 1]!, kx), room = seen[k + 1]! + seen[k + 3]! - x;
                    if (room < least) continue;
                    const i = seen[k]!;
                    const text = noteName(lane.pitch[i]!, lane.start[i]!);
                    if (textWidth(ctx, text, fontSize) + px(7) > room) continue;
                    ctx.fillText(text, x + px(3.5), seen[k + 2]! + seen[k + 4]! / 2 + ratio * 0.5);
                }
            }
        }
        ctx.restore();
        ctx.fillStyle = pal.loop;
        if (region && region.end !== null) {
            for (const t of [region.start, region.end]) ctx.fillRect(px(timeX(t)) - Math.round(ratio), hy, Math.max(2, Math.round(2 * ratio)), H - hy);
        } else if (region) {
            ctx.fillRect(px(timeX(region.start)) - Math.round(ratio), hy, Math.max(2, Math.round(2 * ratio)), H - hy);
        }
        ctx.restore();

        drawHead(ctx, W, kx, hy, from, to, region);
        drawKeys(ctx, base, null);
        placeHandles();
        placeSections();
    }

    // ---- the drum lane ------------------------------------------------------------------------------
    // Along the bottom, in place while the pitches scroll: a row for each kind of drum that occurs, every other one
    // shaded, a mark for each hit — a hit has no length and no pitch.
    function drumLane(ctx: CanvasRenderingContext2D, kx: number, W: number) {
        const pal = colours(), drums = drumsShown()!;
        const y0 = px(vh - drumsH), line = Math.max(1, Math.round(ratio));
        ctx.fillStyle = pal.surface;
        ctx.fillRect(kx, y0, W - kx, base.height - y0);
        ctx.fillStyle = pal.rowDark;
        for (let r = 1; r < drums.rows.length; r += 2) ctx.fillRect(kx, px(drumsTop() + r * drumRow), W - kx, px(drumsTop() + (r + 1) * drumRow) - px(drumsTop() + r * drumRow));
        ctx.fillStyle = pal.border;
        ctx.fillRect(kx, y0, W - kx, line);
    }

    /** Where hit i is drawn: its middle, and half its height, in device pixels. */
    function hitPlace(drums: DrumLane, i: number) {
        return { x: px(timeX(drums.start[i]!)), y: px(drumsTop() + drums.row[i]! * drumRow + drumRow / 2), half: Math.max(px(1.5), px(drumRow - 4) / 2) };
    }
    function diamond(ctx: CanvasRenderingContext2D, at: { x: number; y: number; half: number }, grow = 0) {
        const s = at.half + grow, w = Math.max(px(1.5), s * 0.7);
        ctx.moveTo(at.x, at.y - s);
        ctx.lineTo(at.x + w, at.y);
        ctx.lineTo(at.x, at.y + s);
        ctx.lineTo(at.x - w, at.y);
        ctx.closePath();
    }

    function drumHits(ctx: CanvasRenderingContext2D, from: number, to: number) {
        const drums = drumsShown()!, c = colours().lanes.get(drums.id)!;
        const [first, past] = startingIn(drums, from - 0.1, to + 0.1);
        for (let level = 0; level < LEVELS; level++) {
            ctx.fillStyle = c.fills[level]!;
            ctx.beginPath();
            for (let i = first; i < past; i++) if (levelAt(drums.velocity, i) === level) diamond(ctx, hitPlace(drums, i));
            ctx.fill();
        }
    }

    /** The drums' names, in the keyboard's column beside their rows; `lit`: the rows that sound now (the upper canvas). */
    function drumNames(ctx: CanvasRenderingContext2D, lit: Set<number> | null) {
        const pal = colours(), drums = drumsShown()!, kx = px(keysW), y0 = px(vh - drumsH), line = Math.max(1, Math.round(ratio)), c = pal.lanes.get(drums.id)!;
        if (!lit) {
            ctx.fillStyle = pal.head;
            ctx.fillRect(0, y0, kx, base.height - y0);
            ctx.fillStyle = pal.border;
            ctx.fillRect(0, y0, kx, line);
            ctx.fillRect(kx - line, y0, line, base.height - y0);
        }
        ctx.font = `600 ${DRUM_FONT * ratio}px ${pal.sans}`;
        ctx.textBaseline = 'middle';
        ctx.textAlign = 'right';
        const every = named();
        drums.rows.forEach((r, i) => {
            const a = px(drumsTop() + i * drumRow), b = px(drumsTop() + (i + 1) * drumRow), on = !!lit && lit.has(i);
            if (lit && !on) return;
            if (on) {
                ctx.fillStyle = c.css;
                ctx.fillRect(0, a, kx - line, b - a);
            }
            if ((drums.rows.length - 1 - i) % every) return;      // (counted from the bottom: the kick, the lowest row, is always named)
            ctx.fillStyle = on ? c.text : pal.muted;
            ctx.fillText(texts.drum(r.id), kx - px(6), (a + b) / 2 + ratio * 0.5);
        });
        ctx.textAlign = 'left';
    }

    function secondsStep(): number {
        for (const step of [1, 2, 5, 10, 15, 30, 60, 120, 300]) if (step * pps >= 56) return step;
        return 600;
    }

    function lower(list: number[], t: number): number {
        let lo = 0, hi = list.length;
        while (lo < hi) {
            const mid = (lo + hi) >> 1;
            if (list[mid]! < t) lo = mid + 1;
            else hi = mid;
        }
        return lo;
    }
    function lowerBy(list: { start: number }[], t: number): number {
        let lo = 0, hi = list.length;
        while (lo < hi) {
            const mid = (lo + hi) >> 1;
            if (list[mid]!.start < t) lo = mid + 1;
            else hi = mid;
        }
        return lo;
    }

    function drawHead(ctx: CanvasRenderingContext2D, W: number, kx: number, hy: number, from: number, to: number, region: Region | null) {
        const pal = colours();
        ctx.save();
        ctx.beginPath();
        ctx.rect(kx, 0, W - kx, hy);
        ctx.clip();
        ctx.fillStyle = pal.head;
        ctx.fillRect(kx, 0, W - kx, hy);
        // the ruler: under the sections' band, when there is one
        const r0 = px(topH), ry = px(topH + RULER_H), line = Math.max(1, Math.round(ratio));
        ctx.font = `${10.5 * ratio}px ${pal.mono}`;
        ctx.textBaseline = 'middle';
        if (bars.length) {
            const barPx = (bars[bars.length - 1]!.end - bars[0]!.start) / bars.length * pps;
            const every = barPx >= 30 ? 1 : barPx >= 15 ? 2 : barPx >= 7 ? 4 : barPx >= 3.5 ? 8 : 16;
            if (barPx / 4 >= 7) {
                ctx.fillStyle = pal.beat;
                for (let i = lower(beats, from); i < beats.length && beats[i]! <= to; i++) ctx.fillRect(px(timeX(beats[i]!)), ry - px(4), line, px(4));
            }
            for (let i = Math.max(0, lowerBy(bars, from) - 1); i < bars.length && bars[i]!.start <= to; i++) {
                const bar = bars[i]!, x = px(timeX(bar.start));
                ctx.fillStyle = pal.bar;
                ctx.fillRect(x, r0, line, ry - r0);
                if ((bar.number - 1) % every === 0) {
                    ctx.fillStyle = pal.muted;
                    ctx.fillText(String(bar.number), x + px(4), (r0 + ry) / 2);
                }
            }
        } else {
            const step = secondsStep();
            for (let t = Math.ceil(from / step) * step; t <= to; t += step) {
                const x = px(timeX(t));
                ctx.fillStyle = pal.bar;
                ctx.fillRect(x, r0, line, ry - r0);
                ctx.fillStyle = pal.muted;
                ctx.fillText(`${Math.floor(t / 60)}:${String(Math.round(t % 60)).padStart(2, '0')}`, x + px(4), (r0 + ry) / 2);
            }
        }
        if (topH) {
            ctx.fillStyle = pal.border;
            ctx.fillRect(kx, r0 - line, W - kx, line);
        }
        if (region) {
            // the loop's place in the ruler
            ctx.fillStyle = pal.loop;
            const x0 = px(timeX(region.start)), x1 = region.end === null ? x0 + px(2) : px(timeX(region.end));
            ctx.fillRect(x0, ry - px(3), x1 - x0, px(3));
        }
        for (let i = Math.max(0, lowerBy(chords, from) - 1); i < chords.length && chords[i]!.start <= to; i++) drawChord(ctx, i, false);
        ctx.fillStyle = pal.border;
        ctx.fillRect(kx, hy - line, W - kx, line);
        ctx.restore();
    }

    function drawChord(ctx: CanvasRenderingContext2D, i: number, active: boolean) {
        const pal = colours(), chord = chords[i]!;
        const x0 = px(timeX(chord.start)), x1 = px(timeX(chord.end)), y = px(topH + RULER_H) + px(2), h = px(CHORDS_H) - px(5);
        const w = Math.max(px(2), x1 - x0 - px(2));
        ctx.fillStyle = active ? pal.chordActiveBg : pal.chordBg;
        ctx.beginPath();
        ctx.roundRect(x0, y, w, h, Math.min(px(4), w / 2));
        ctx.fill();
        if (w < px(14)) return;
        ctx.font = `600 ${12 * ratio}px ${pal.mono}`;
        ctx.textBaseline = 'middle';
        ctx.save();
        ctx.beginPath();
        ctx.rect(x0, y, w - px(2), h);
        ctx.clip();
        ctx.fillStyle = active ? pal.chordActiveText : pal.chordText;
        // a chord that began left of the roll keeps its name in sight
        ctx.fillText(chordText(chord), Math.max(x0, px(keysW)) + px(5), y + h / 2 + ratio * 0.5);
        ctx.restore();
    }

    /** The keyboard. `lit`: pitch → the colour of the part sounding it now, and the drum rows that sound (the upper
     * canvas); or null for the keys themselves. */
    function drawKeys(ctx: CanvasRenderingContext2D, canvas: HTMLCanvasElement, lit: { keys: Map<number, string>; drums: Set<number> } | null) {
        const pal = colours();
        const kx = px(keysW), hy = px(headH), H = canvas.height - px(drumsH);
        ctx.save();
        ctx.beginPath();
        ctx.rect(0, hy, kx, H - hy);
        ctx.clip();
        const top = clamp(Math.ceil(high - element.scrollTop / row), low, high), bottom = clamp(Math.floor(high - (element.scrollTop + vh - headH - drumsH) / row), low, high);
        const blackW = Math.round(kx * 0.62), line = Math.max(1, Math.round(ratio));
        if (!lit) {
            ctx.fillStyle = pal.keyWhite;
            ctx.fillRect(0, hy, kx, H - hy);
        }
        for (let p = top; p >= bottom; p--) {
            const y0 = px(pitchY(p)), y1 = px(pitchY(p) + row), black = BLACK[p % 12];
            if (lit) {
                const colour = lit.keys.get(p);
                if (!colour) continue;
                ctx.fillStyle = colour;
                ctx.fillRect(0, y0, black ? blackW : kx - line, y1 - y0 - (black ? 0 : line));
            } else if (black) {
                ctx.fillStyle = pal.keyBlack;
                ctx.fillRect(0, y0, blackW, y1 - y0);
            } else if (p % 12 === 0 || p % 12 === 5) {
                // the two places where white keys meet: B|C and E|F
                ctx.fillStyle = pal.border;
                ctx.fillRect(0, y1 - line, kx, line);
            }
        }
        if (!lit) {
            // the name of every C, and of every white key when the rows are tall enough to carry it
            const all = row >= 15, size = clamp(Math.floor(row - 2), 8, 10.5);
            ctx.font = `${all ? 500 : 600} ${size * ratio}px ${pal.sans}`;
            ctx.textBaseline = 'middle';
            ctx.textAlign = 'right';
            ctx.fillStyle = pal.keyText;
            for (let p = top; p >= bottom; p--) {
                if (p % 12 !== 0 && !(all && !BLACK[p % 12])) continue;
                // in a low row the name sits on the line under its key, not across it
                ctx.fillText(noteName(p, time), kx - px(4), row >= 11 ? px(pitchY(p) + row / 2) + ratio * 0.5 : px(pitchY(p) + row) - px(1) - size * ratio * 0.42);
            }
            ctx.textAlign = 'left';
            ctx.fillStyle = pal.border;
            ctx.fillRect(kx - line, hy, line, H - hy);
        }
        ctx.restore();
        if (!lit) {
            ctx.fillStyle = pal.head;
            ctx.fillRect(0, 0, kx, hy);
            ctx.fillStyle = pal.border;
            ctx.fillRect(kx - line, 0, line, hy);
            ctx.fillRect(0, hy - line, kx, line);
        }
        if (drumsH) drumNames(ctx, lit ? lit.drums : null);
    }

    // ---- the upper canvas ---------------------------------------------------------------------------
    function drawOver() {
        const pal = colours();
        const ctx = over.getContext('2d')!, W = over.width, H = over.height;
        const kx = px(keysW), hy = px(headH);
        ctx.clearRect(0, 0, W, H);
        const lit = { keys: new Map<number, string>(), drums: new Set<number>() };
        const ring = Math.max(1, Math.round(1.5 * ratio));
        // the hits that sound: a moment each, larger, ringed in ink
        const drums = drumsShown();
        if (drums) {
            ctx.save();
            ctx.beginPath();
            ctx.rect(kx, H - px(drumsH), W - kx, px(drumsH));
            ctx.clip();
            const [first, past] = startingIn(drums, time - DRUM_FLASH, time + 1e-6);
            for (let i = first; i < past; i++) {
                lit.drums.add(drums.row[i]!);
                const at = hitPlace(drums, i);
                ctx.fillStyle = pal.ink;
                ctx.beginPath();
                diamond(ctx, at, ring + px(1));
                ctx.fill();
                ctx.fillStyle = pal.lanes.get(drums.id)!.css;
                ctx.beginPath();
                diamond(ctx, at, px(1));
                ctx.fill();
            }
            ctx.restore();
        }
        ctx.save();
        ctx.beginPath();
        ctx.rect(kx, hy, W - kx, H - px(drumsH) - hy);
        ctx.clip();
        for (const lane of lanesShown()) {
            const c = pal.lanes.get(lane.id)!;
            for (const i of soundingAt(lane, time)) {
                const b = place(lane.start[i]!, lane.end[i]!, lane.pitch[i]!);
                lit.keys.set(lane.pitch[i]!, c.css);
                // the note that sounds: its full colour, ringed in ink
                ctx.fillStyle = pal.ink;
                ctx.fillRect(b.x - ring, b.y - ring, b.w + 2 * ring, b.h + 2 * ring);
                ctx.fillStyle = c.css;
                ctx.fillRect(b.x, b.y, b.w, b.h);
                if (row >= 11) {
                    const size = clamp(Math.floor(row - 3), 8, 11), text = noteName(lane.pitch[i]!, lane.start[i]!);
                    ctx.font = `600 ${size * ratio}px ${pal.sans}`;
                    ctx.textBaseline = 'middle';
                    const x = Math.max(b.x, kx);
                    if (ctx.measureText(text).width + px(7) <= b.x + b.w - x) {
                        ctx.fillStyle = c.text;
                        ctx.fillText(text, x + px(3.5), b.y + b.h / 2 + ratio * 0.5);
                    }
                }
            }
        }
        const marked = pinned ?? hover;
        if (marked && marked.lane !== drums) {
            const lane = marked.lane as Lane, b = place(lane.start[marked.index]!, lane.end[marked.index]!, lane.pitch[marked.index]!);
            ctx.strokeStyle = pal.ink;
            ctx.lineWidth = Math.max(1, Math.round(1.5 * ratio));
            ctx.strokeRect(b.x - ctx.lineWidth / 2, b.y - ctx.lineWidth / 2, b.w + ctx.lineWidth, b.h + ctx.lineWidth);
        }
        ctx.restore();
        if (marked && drums && marked.lane === drums) {
            ctx.strokeStyle = pal.ink;
            ctx.lineWidth = Math.max(1, Math.round(1.5 * ratio));
            ctx.beginPath();
            diamond(ctx, hitPlace(drums, marked.index), px(2));
            ctx.stroke();
        }
        drawKeys(ctx, over, lit);

        ctx.save();
        ctx.beginPath();
        ctx.rect(kx, 0, W - kx, H);
        ctx.clip();
        const sounded = activeChord;
        activeChord = chordAt(time);
        if (activeChord >= 0) drawChord(ctx, activeChord, true);
        if (activeChord !== sounded || !strip.hasAttribute('aria-valuetext')) {
            strip.setAttribute('aria-valuenow', String(activeChord + 1));
            strip.setAttribute('aria-valuetext', activeChord >= 0 ? chordText(chords[activeChord]!) : texts.noChord);
        }
        // the playhead, from the ruler down (the sections' band above it marks the section that sounds itself)
        const x = px(timeX(time)), wide = Math.max(2, Math.round(2 * ratio)), y0 = px(topH);
        ctx.fillStyle = pal.playhead;
        ctx.fillRect(x - Math.floor(wide / 2), y0, wide, H - y0);
        ctx.beginPath();
        ctx.moveTo(x - px(5), y0);
        ctx.lineTo(x + px(5), y0);
        ctx.lineTo(x, y0 + px(7));
        ctx.closePath();
        ctx.fill();
        ctx.restore();
        let section = -1;
        for (let i = 0; i < sections.length; i++) {
            if (time >= sections[i]!.start && time < sections[i]!.end) {
                section = i;
                break;
            }
        }
        if (section !== activeSection) {
            sectionEls[activeSection]?.classList.remove('now');
            sectionEls[section]?.classList.add('now');
            activeSection = section;
        }
    }

    // ---- the sections' band: each section is a button over its stretch of the music ----------------------
    function placeSections() {
        const room = vw - keysW;
        sections.forEach((section, i) => {
            const el = sectionEls[i]!;
            const x0 = section.start * pps - element.scrollLeft, x1 = section.end * pps - element.scrollLeft;
            el.hidden = x1 <= 0 || x0 >= room;
            if (el.hidden) return;
            // the part of it in sight: a section that began left of the roll keeps its name there
            const a = Math.max(0, x0), b = Math.min(room, x1);
            el.style.left = `${a}px`;
            el.style.width = `${Math.max(2, b - a - 2)}px`;
        });
    }

    function chordAt(t: number): number {
        let lo = 0, hi = chords.length - 1;
        while (lo <= hi) {
            const mid = (lo + hi) >> 1;
            if (t < chords[mid]!.start) hi = mid - 1;
            else if (t >= chords[mid]!.end) lo = mid + 1;
            else return mid;
        }
        return -1;
    }

    // ---- the loop's handles: real elements, so that they can be dragged and reached from the keyboard
    function placeHandles() {
        const region = draft ?? loop;
        handles.forEach((el, n) => {
            const t = region && region.end !== null ? (n === 0 ? region.start : region.end) : null;
            const x = t === null ? -1 : timeX(t);
            el.hidden = t === null || x < keysW - 1 || x > vw;
            if (t === null || el.hidden) return;
            el.style.transform = `translateX(${Math.round(x)}px)`;
            el.setAttribute('aria-valuemin', '0');
            el.setAttribute('aria-valuemax', String(Math.round(length)));
            el.setAttribute('aria-valuenow', String(Math.round(t * 10) / 10));
            el.setAttribute('aria-valuetext', clock(t));
        });
    }

    function clock(t: number): string {
        t = Math.max(0, Math.floor(t * 10) / 10);
        const m = Math.floor(t / 60), s = t - 60 * m;
        return `${m}:${s < 10 ? '0' : ''}${s.toFixed(1)}`;
    }

    // ---- what is under the pointer ------------------------------------------------------------------
    function locate(event: { clientX: number; clientY: number }): At {
        const rect = stage.getBoundingClientRect(), z = zoomOf();
        const x = (event.clientX - rect.left) / z, y = (event.clientY - rect.top) / z;
        return {
            x, y, t: clamp((x - keysW + element.scrollLeft) / pps, 0, length),
            pitch: high - Math.floor((y - headH + element.scrollTop) / row), drum: Math.floor((y - drumsTop()) / drumRow),
            zone: x < keysW ? 'keys' : y < topH ? 'sections' : y < topH + RULER_H ? 'ruler' : y < headH ? 'chords' : drumsH && y >= vh - drumsH ? 'drums' : 'roll',
        };
    }

    /** The note at a place of the roll, the topmost one — or the hit, in the drum lane; `reach` pixels of grace in
     * time, and with `coarse` a row either way. */
    function noteNear(at: At, reach: number, coarse: boolean): Marked | null {
        const grace = reach / pps;
        if (at.zone === 'drums') {
            const drums = drumsShown();
            if (!drums) return null;
            const wide = Math.max(grace, (drumRow / 2) / pps);
            let found = -1;
            for (const dr of coarse ? [0, 1, -1] : [0]) {
                const [first, past] = startingIn(drums, at.t - wide, at.t + wide);
                for (let i = first; i < past; i++) {
                    if (drums.row[i] === at.drum + dr && (found < 0 || Math.abs(drums.start[i]! - at.t) < Math.abs(drums.start[found]! - at.t))) found = i;
                }
                if (found >= 0) return { lane: drums, index: found };
            }
            return null;
        }
        const lanes = lanesShown().reverse();
        for (const dp of coarse ? [0, 1, -1] : [0]) {
            for (const lane of lanes) {
                const pitch = at.pitch + dp;
                let index = noteAt(lane, at.t, pitch);
                for (let i = firstFrom(lane, at.t - grace); index < 0 && i < lane.count && lane.start[i]! <= at.t + grace; i++) {
                    if (lane.pitch[i] === pitch && lane.end[i]! > at.t - grace) index = i;
                }
                if (index >= 0) return { lane, index };
            }
        }
        return null;
    }

    function showTip(note: Marked | null, at?: At) {
        if (!note || !at) {
            tip.hidden = true;
            return;
        }
        const { index } = note, drums = drumsShown(), hit = !!drums && note.lane === drums;
        const head = document.createElement('strong');
        const dot = document.createElement('span');
        dot.className = 'rl-dot';
        dot.style.background = colours().lanes.get(note.lane.id)!.css;
        const name = document.createElement('span');
        const line2 = document.createElement('div');
        let velocity = NONE;
        if (hit) {
            head.textContent = texts.drum(drums.rows[drums.row[index]!]!.id);
            name.textContent = texts.drums;
            line2.textContent = clock(drums.start[index]!);
            if (drums.velocity && data?.velocity) velocity = drums.velocity[index]!;
        } else {
            const lane = note.lane as Lane, seconds = lane.end[index]! - lane.start[index]!;
            head.textContent = noteName(lane.pitch[index]!, lane.start[index]!);
            name.textContent = lane.name;
            line2.textContent = `${clock(lane.start[index]!)} – ${clock(lane.end[index]!)} · ${texts.seconds(seconds < 10 ? seconds.toFixed(2) : seconds.toFixed(1))}`;
            if (lane.velocity && data?.velocity) velocity = lane.velocity[index]!;
        }
        const line1 = document.createElement('div');
        line1.className = 'rl-tip-head';
        line1.append(head, dot, name);
        tip.replaceChildren(line1, line2);
        if (velocity !== NONE) {
            const line3 = document.createElement('div');
            line3.textContent = texts.velocity(velocity);
            tip.append(line3);
        }
        tip.hidden = false;
        const w = tip.offsetWidth, h = tip.offsetHeight;
        tip.style.left = `${clamp(at.x + 12, keysW + 4, Math.max(keysW + 4, vw - w - 4))}px`;
        tip.style.top = `${at.y - h - 10 >= headH ? at.y - h - 10 : Math.min(vh - h - 4, at.y + 16)}px`;
    }

    // ---- pointer: a click plays from there, a drag (mouse or pen) makes a loop, the loop's handles -----
    /** The beat nearest to t — or the start or the end, when that is nearer; with Alt, t itself. */
    function snap(t: number, alt: boolean): number {
        if (alt || !beats.length) return t;
        const i = lower(beats, t);
        let best = t, distance = Infinity;
        for (const b of [beats[i - 1], beats[i], 0, length]) {
            if (b === undefined) continue;
            const d = Math.abs(b - t);
            if (d < distance) {
                distance = d;
                best = b;
            }
        }
        return best;
    }
    /** The beat after `from` (way > 0) or before it. */
    const beatBeside = (from: number, way: number): number | undefined => (way > 0 ? beats[lower(beats, from + 0.001)] : beats[lower(beats, from - 0.001) - 1]);

    let dragged = false, lastPointer = 'mouse';
    function dragLoop(event: PointerEvent, fixed: number) {
        const target = event.currentTarget as HTMLElement, startX = event.clientX;
        let region: { start: number; end: number } | null = null, moved = false;
        dragged = false;
        const move = (e: PointerEvent) => {
            if (!moved && Math.abs(e.clientX - startX) < DRAG_PX) return;
            if (!moved) {
                target.setPointerCapture(e.pointerId);
                showTip(null);
                hover = null;
            }
            moved = true;
            dragged = true;
            const t = snap(locate(e).t, e.altKey);
            region = { start: Math.min(fixed, t), end: Math.max(fixed, t) };
            draft = region;
            queueBase();
        };
        const up = (e: PointerEvent) => {
            target.removeEventListener('pointermove', move);
            target.removeEventListener('pointerup', up);
            target.removeEventListener('pointercancel', up);
            draft = null;
            if (region && e.type === 'pointerup' && region.end - region.start >= minLoop) onLoop(region.start, region.end);
            queueBase();
        };
        target.addEventListener('pointermove', move);
        target.addEventListener('pointerup', up);
        target.addEventListener('pointercancel', up);
    }

    function onPointerDown(event: PointerEvent) {
        hold();
        lastPointer = event.pointerType;
        dragged = false;
        if (event.button !== 0 || event.target !== over || event.pointerType === 'touch') return;      // fingers have their own ways, below
        const at = locate(event);
        if (at.zone === 'roll' || at.zone === 'drums' || at.zone === 'ruler') dragLoop(event, snap(at.t, event.altKey));
    }

    function onClick(event: MouseEvent) {
        if (event.target !== over || dragged) {
            dragged = false;
            return;
        }
        const at = locate(event);
        if (at.zone === 'chords') {
            const i = chordAt(at.t);
            if (i >= 0) {
                onChord(chords[i]!);
                return;
            }
        }
        if (at.zone === 'keys' || at.zone === 'sections') return;
        if ((at.zone === 'roll' || at.zone === 'drums') && lastPointer !== 'mouse') {
            // a tap names the note under the finger for a moment
            pinned = noteNear(at, 10, true);
            clearTimeout(pinTimer);
            showTip(pinned, at);
            if (pinned) {
                pinTimer = setTimeout(() => {
                    pinned = null;
                    showTip(null);
                    queueOver();
                }, 2500);
            }
            queueOver();
        }
        onSeek(at.t);
    }

    function onPointerMove(event: PointerEvent) {
        lastPointer = event.pointerType;
        if (event.pointerType !== 'mouse' || event.buttons) return;
        const at = locate(event);
        const next = event.target === over && (at.zone === 'roll' || at.zone === 'drums') ? noteNear(at, 2, false) : null;
        const same = (a: Marked | null, b: Marked | null) => (a === null ? b === null : b !== null && a.lane === b.lane && a.index === b.index);
        stage.style.cursor = event.target !== over ? '' : at.zone === 'chords' && chordAt(at.t) >= 0 ? 'pointer' : at.zone === 'keys' ? 'default' : '';
        if (!same(next, hover)) {
            hover = next;
            queueOver();
        }
        showTip(pinned ?? hover, at);
    }
    function onPointerLeave() {
        if (hover) {
            hover = null;
            queueOver();
        }
        if (!pinned) showTip(null);
    }

    handles.forEach((el, n) => {
        el.addEventListener('pointerdown', event => {
            if (event.button !== 0 || !loop || loop.end === null) return;
            event.preventDefault();
            event.stopPropagation();
            hold();
            el.setPointerCapture(event.pointerId);
            dragLoop(event, n === 0 ? loop.end : loop.start);
        });
        // the arrows move the end by a beat (with Alt, by a tenth of a second)
        el.addEventListener('keydown', event => {
            const way = event.key === 'ArrowLeft' ? -1 : event.key === 'ArrowRight' ? 1 : 0;
            if (!way || !loop || loop.end === null) return;
            event.preventDefault();
            event.stopPropagation();
            const at = n === 0 ? loop.start : loop.end;
            const next = !event.altKey && beats.length ? beatBeside(at, way) : at + way * 0.1;
            if (next === undefined) return;
            const [a, b] = n === 0 ? [next, loop.end] : [loop.start, next];
            if (b - a >= minLoop && a >= 0 && b <= length) onLoop(a, b);
        });
    });

    // ---- scrolling and zooming ----------------------------------------------------------------------
    const hold = () => { holdUntil = performance.now() + HOLD_MS; };

    function setZoom(value: number, anchorX: number | null = null): boolean {
        value = clamp(Math.round(value), MIN_ZOOM, MAX_ZOOM);
        if (value === pps) return false;
        const x = anchorX ?? keysW + (vw - keysW) * 0.3;
        const t = (element.scrollLeft + x - keysW) / pps;
        pps = value;
        layout();
        element.scrollLeft = Math.max(0, t * pps - (x - keysW));
        return true;
    }

    function setRow(value: number | null, anchorY: number | null = null): boolean {
        const next = value === null ? null : clamp(Math.round(value * 2) / 2, MIN_ROW, MAX_ROW);
        if (next === rowWanted) return false;
        const y = anchorY ?? headH + (vh - headH - drumsH) / 2;
        const pitch = high - (element.scrollTop + y - headH) / row;
        rowWanted = next;
        layout({ pitch, y });
        return true;
    }

    function onWheel(event: WheelEvent) {
        hold();
        const rect = stage.getBoundingClientRect(), z = zoomOf();
        if (event.ctrlKey || event.metaKey) {
            event.preventDefault();
            if (setZoom(pps * Math.pow(1.0018, -event.deltaY), (event.clientX - rect.left) / z)) onZoom({ pps, row: rowWanted });
        } else if (event.altKey) {
            event.preventDefault();
            if (setRow(row * Math.pow(1.0018, -event.deltaY), (event.clientY - rect.top) / z)) onZoom({ pps, row: rowWanted });
        } else if (!event.shiftKey && event.deltaX === 0 && element.scrollHeight <= element.clientHeight) {
            // nothing to scroll up and down: the wheel moves along the music
            event.preventDefault();
            element.scrollLeft += event.deltaY;
        }
    }

    // Fingers. One: a tap plays from there (the click above); a drag scrolls (the browser does that); held still for a
    // moment and then dragged, it draws a loop from beat to beat. Two: spread sideways to zoom the time, up and down
    // to zoom the pitch.
    let pinch: { sideways: boolean; span: number; pps: number; row: number } | null = null;
    let finger: { x: number; y: number; moved: boolean; drawing: boolean; fixed: number; region: { start: number; end: number } | null; timer: ReturnType<typeof setTimeout> | undefined } | null = null;
    function endFinger() {
        if (!finger) return;
        clearTimeout(finger.timer);
        finger = null;
    }
    function fingers(event: TouchEvent) {
        const a = event.touches[0]!, b = event.touches[1]!, rect = stage.getBoundingClientRect(), z = zoomOf();
        return { dx: Math.abs(a.clientX - b.clientX), dy: Math.abs(a.clientY - b.clientY), x: ((a.clientX + b.clientX) / 2 - rect.left) / z, y: ((a.clientY + b.clientY) / 2 - rect.top) / z };
    }
    function onTouchStart(event: TouchEvent) {
        hold();
        if (event.touches.length === 2) {
            if (finger?.drawing) {
                draft = null;
                queueBase();
            }
            endFinger();
            const f = fingers(event);
            pinch = { sideways: f.dx >= f.dy, span: Math.max(24, f.dx >= f.dy ? f.dx : f.dy), pps, row };
            return;
        }
        pinch = null;
        endFinger();
        const touch = event.touches[0];
        if (!touch || event.touches.length !== 1 || event.target !== over) return;
        const at = locate(touch);
        if (at.zone !== 'roll' && at.zone !== 'drums' && at.zone !== 'ruler') return;
        const mine: NonNullable<typeof finger> = { x: touch.clientX, y: touch.clientY, moved: false, drawing: false, fixed: 0, region: null, timer: undefined };
        finger = mine;
        mine.timer = setTimeout(() => {
            if (finger !== mine || mine.moved) return;
            mine.drawing = true;
            mine.fixed = snap(at.t, false);
            draft = { start: mine.fixed, end: null };
            queueBase();
            navigator.vibrate?.(12);
        }, PRESS_MS);
    }
    function onTouchMove(event: TouchEvent) {
        if (pinch && event.touches.length === 2) {
            if (event.cancelable) event.preventDefault();
            const f = fingers(event);
            const changed = pinch.sideways ? setZoom(pinch.pps * Math.max(24, f.dx) / pinch.span, f.x) : setRow(pinch.row * Math.max(24, f.dy) / pinch.span, f.y);
            if (changed) onZoom({ pps, row: rowWanted });
            return;
        }
        const touch = event.touches[0];
        if (!finger || !touch || event.touches.length !== 1) return;
        if (!finger.drawing) {
            if (Math.hypot(touch.clientX - finger.x, touch.clientY - finger.y) > PRESS_SLOP) {
                finger.moved = true;
                clearTimeout(finger.timer);
            }
            return;
        }
        if (event.cancelable) event.preventDefault();       // the finger draws; the roll stays where it is
        const t = snap(locate(touch).t, false);
        finger.region = { start: Math.min(finger.fixed, t), end: Math.max(finger.fixed, t) };
        draft = finger.region.end - finger.region.start >= minLoop ? finger.region : { start: finger.fixed, end: null };
        queueBase();
    }
    function onTouchEnd(event: TouchEvent) {
        if (pinch) {
            if (event.touches.length < 2) pinch = null;
            return;
        }
        if (!finger || event.touches.length) return;
        const done = finger;
        endFinger();
        if (!done.drawing) return;
        if (event.cancelable) event.preventDefault();       // no click follows a loop drawn
        draft = null;
        if (event.type === 'touchend' && done.region && done.region.end - done.region.start >= minLoop) onLoop(done.region.start, done.region.end);
        queueBase();
    }
    const onContextMenu = (event: Event) => { if (finger) event.preventDefault(); };      // a held finger is the roll's, not the menu's

    element.addEventListener('scroll', queueBase, { passive: true });
    element.addEventListener('wheel', onWheel, { passive: false });
    element.addEventListener('touchstart', onTouchStart, { passive: true });
    element.addEventListener('touchmove', onTouchMove, { passive: false });
    element.addEventListener('touchend', onTouchEnd, { passive: false });
    element.addEventListener('touchcancel', onTouchEnd, { passive: true });
    over.addEventListener('contextmenu', onContextMenu);
    stage.addEventListener('pointerdown', onPointerDown);
    stage.addEventListener('pointermove', onPointerMove);
    stage.addEventListener('pointerleave', onPointerLeave);
    stage.addEventListener('click', onClick);

    /** Scrolls so that moment t is in sight, a third of the way in, when it is near an edge or out of sight. */
    function bringIntoSight(t: number) {
        const x = t * pps, left = element.scrollLeft, room = vw - keysW;
        if (x < left + room * 0.1 || x > left + room * 0.8) element.scrollLeft = Math.max(0, x - room * 0.3);
    }

    return {
        setSong(song) {
            const first = !data;
            // (other notes have other parts, and their colours are read anew)
            if (song.data !== data) palette = null;
            data = song.data;
            length = song.length;
            bars = song.bars;
            beats = song.beats;
            chords = song.chords;
            hover = null;
            pinned = null;
            showTip(null);
            layout();
            // (the place may have been given before there were notes to scroll over: it is brought into sight with the first)
            if (first) bringIntoSight(time);
        },
        setSections(list) {
            sections = list;
            activeSection = -1;
            sectionEls = list.map(section => {
                const el = document.createElement('button');
                el.type = 'button';
                el.className = `sk sk-${section.label ?? 'other'}`;
                el.textContent = section.name;
                el.title = section.name;
                el.addEventListener('click', () => onSection(section));
                return el;
            });
            sectionsEl.replaceChildren(...sectionEls);
            layout();
        },
        setShown(ids) {
            shown = new Set(ids);
            hover = null;
            pinned = null;
            showTip(null);
            layout();
        },
        setZoom(value) { setZoom(value); },
        setRow(value) { setRow(value); },
        get zoom() { return pps; },
        get row() { return rowWanted; },
        get rowShown() { return row; },
        setTime(t, isPlaying) {
            const jumped = Math.abs(t - time) > 0.5;
            if (t === time && isPlaying === playing) return;
            time = t;
            playing = isPlaying;
            if ((playing && performance.now() > holdUntil) || (!playing && jumped)) bringIntoSight(t);
            queueOver();
        },
        setLoop(value) {
            loop = value;
            queueBase();
        },
        setTexts(value) {
            texts = value;
            applyTexts();
            layout();
        },
        redraw() {
            palette = null;
            strip.removeAttribute('aria-valuetext');
            layout();
        },
        refresh() { queueBase(); },
        destroy() {
            destroyed = true;
            clearTimeout(pinTimer);
            endFinger();
            element.removeEventListener('scroll', queueBase);
            element.removeEventListener('wheel', onWheel);
            element.removeEventListener('touchstart', onTouchStart);
            element.removeEventListener('touchmove', onTouchMove);
            element.removeEventListener('touchend', onTouchEnd);
            element.removeEventListener('touchcancel', onTouchEnd);
            track.remove();
            element.classList.remove('rl-scroll');
        },
    };
}
