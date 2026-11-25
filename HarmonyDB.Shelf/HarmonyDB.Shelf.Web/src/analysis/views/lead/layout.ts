// The engraving: where everything of the lead sheet goes, for one width. Data in, data out; no DOM. Text is measured
// through the `measure` it is given.
//
//   layoutSheet(music, { width, space, measure, writing }) → Engraving
//     width     the width to fill, px
//     space     one staff space, px; every other size follows from it
//     measure   (text, px, weight) → width of the text in px
//     writing   "slashes": a chord is a rhythm slash of its length; "notes": the chord's own notes
//
// The rules:
//   - Each beat is as wide as what is written in it needs (a beat of sixteenths wider than one held), and beats alike are
//     alike; a bar is as wide as its beats, its chord symbols and its words need; nothing is squeezed or shrunk. A bar
//     wider than a line even so stands alone on its line, wider than the view.
//   - A note stands at the beat it begins on, however long it lasts.
//   - A chord symbol stands exactly at its beat. Words stand at the moment they are sung and give way to each other by
//     the least total shift.
//   - Chord symbols stand on one line above the system (two where one comes less than half a beat after the other) and
//     the words on one below it; both move away from the staff as far as the system's notes, stems, ties and fermatas
//     need.
//   - Lines are broken by the best total of: comfortable spacing, breaks on four-bar phrases, lyric lines kept whole. A
//     section always starts a line.

import { GLYPH, LINE, timeDigits, type GlyphName } from './glyphs';
import { AFTER_CLEF, AFTER_KEY, CLEF_LEFT, keySigns, signsWidth, type KeySign } from './key';
import type { Music, MusicBar, ChordEvent, Word } from './music';
import { beam, headsOf, shape, stemGoesUp, tie, tupletFigure, type HeadIn, type Mark, type Part, type Placed } from './notation';
import type { Note } from './rhythm';
import { accidentalsOf, keyAlters } from './voicing';

export type Writing = 'slashes' | 'notes';

export interface Sizes { space: number; music: number; chord: number; lyric: number; section: number; number: number; tempo: number; tempoNote: number }

function sizesFor(space: number): Sizes {
    return {
        space,
        music: 4 * space,          // font size of the music signs: the em is four spaces
        chord: 1.9 * space,
        lyric: 1.7 * space,
        section: 1.45 * space,
        number: 1.25 * space,
        tempo: 1.6 * space,
        tempoNote: 2.6 * space,
    };
}

// In staff spaces.
const SIG_LEFT = 0.5, SIG_RIGHT = 0.2;
const PAD_LEFT = 0.9;            // from a bar's left edge (or its time signature) to its first beat, at least
const MARK_LEAD = 0.8;           // least space before a bar's first note, with its accidentals
const MARK_GAP = 0.6;            // least space between two notes
const MARK_END = 0.5;            // least space between a bar's last note and its barline
const TIE_MIN = 1.6;             // a tie is never shorter
const TIE_ACROSS = 2.4;          // a tie over a barline goes at least this far before it, from its note
const TIE_STUB = 1.8;            // the part of a tie that comes over from the line before
const BARLINE_CLEAR = 0.4;       // a tie that goes on to the next line stops this far before the barline
const FERMATA_CLEAR = 0.6;       // a fermata's foot above the staff, or above the notes it holds
const CHORD_LINE = 1.0, CHORD_CLEAR = 0.5;      // the chord symbols' baseline above the staff, and above the highest ink
const CHORD_ROW = 2.3;           // the upper row of chord symbols over the lower one
const WORD_LINE = 0.9, WORD_CLEAR = 0.4;        // the words' tops below the staff, and below the lowest ink
const SIGNS: Record<number, GlyphName> = { [-3]: 'tripleFlat', [-2]: 'doubleFlat', [-1]: 'flat', 0: 'natural', 1: 'sharp', 2: 'doubleSharp', 3: 'tripleSharp' };
const BEAT_TIGHT = 3.0, BEAT_COMFORT = 7.0;
const CHORD_GAP = 1.2;           // least space between two chord symbols
const WORD_GAP = 0.3;            // added to a space of the lyric font between two words
const WORD_ROOM = 1.12;          // a comfortable bar is this much wider than its words
const MAX_BARS = 16;

export type Measure = (text: string, px: number, weight: number) => number;

/** A piece of a text: plain text in the text font, or a sign (♭ ♯ 𝄫 𝄪) from the music font. */
export interface TextRun { text: string; sign: boolean; x: number; w: number }

const CHORD_SIGNS: Record<string, GlyphName> = { '♭': 'chordFlat', '♯': 'chordSharp', '𝄫': 'chordDoubleFlat', '𝄪': 'chordDoubleSharp' };

function textRuns(text: string, px: number, weight: number, measure: Measure): { runs: TextRun[]; w: number } {
    const runs: TextRun[] = [];
    let x = 0;
    for (const part of text.split(/([♭♯𝄫𝄪])/u)) {
        if (!part) continue;
        const sign = CHORD_SIGNS[part];
        if (sign) {
            const g = GLYPH[sign], w = g.w * px / 4;
            x += 0.04 * px;
            runs.push({ text: g.ch, sign: true, x, w });
            x += w + 0.04 * px;
        } else {
            const w = measure(part, px, weight);
            runs.push({ text: part, sign: false, x, w });
            x += w;
        }
    }
    return { runs, w: x };
}

/**
 * Places boxes of given widths on a line as near as possible to where each wants to start (least sum of squared shifts),
 * in order, at least `gap` apart, inside [lo, hi]. With the widths taken out this is an isotonic regression, solved by
 * pooling neighbours that are out of order.
 */
function placeInOrder(items: { ideal: number; w: number }[], lo: number, hi: number, gap: number): number[] {
    const offsets: number[] = [];
    let acc = 0;
    for (const it of items) {
        offsets.push(acc);
        acc += it.w + gap;
    }
    const total = acc - gap, blocks: { sum: number; count: number }[] = [];
    items.forEach((it, i) => {
        blocks.push({ sum: it.ideal - offsets[i]!, count: 1 });
        while (blocks.length > 1) {
            const b = blocks[blocks.length - 1]!, a = blocks[blocks.length - 2]!;
            if (a.sum / a.count <= b.sum / b.count) break;
            a.sum += b.sum;
            a.count += b.count;
            blocks.pop();
        }
    });
    const xs: number[] = [];
    let i = 0;
    for (const b of blocks) {
        const y = Math.max(lo, Math.min(hi - total, b.sum / b.count));
        for (let k = 0; k < b.count; k++, i++) xs.push(y + offsets[i]!);
    }
    return xs;
}

// ---- what comes out ------------------------------------------------------------------------------------

/** A part in px. */
export type DrawnPart =
    | { type: 'glyph'; kind: string; glyph: GlyphName; x: number; y: number }
    | { type: 'text'; kind: string; text: string; x: number; y: number }
    | { type: 'ink'; kind: string; x: number; y: number; w: number; h: number };

export type Join = (DrawnPart & { bar: number }) | { type: 'tie'; kind: 'tie'; bar: number; path: string };

export interface PlacedSign { glyph: GlyphName; x: number; y: number }

export interface TimeSig { top: string; bottom: string; topX: number; bottomX: number; topY: number; bottomY: number }

export interface LaidMark { pos: number; beats: number; parts: DrawnPart[] }

export interface LaidBar {
    index: number;
    left: number;
    right: number;
    /** where each beat begins, and where the last ends: time maps to x through the bar's beats (xIn) */
    beatX: number[];
    count: number;
    marks: LaidMark[];
    /** a key that changes with the bar, not at a line's start: its naturals and its signs */
    keySigns: PlacedSign[];
    signature: TimeSig | null;
    barline: 'final' | 'double' | 'single';
}

export interface System {
    index: number;
    width: number;
    height: number;
    top: number;
    bottom: number;
    staffTop: number;
    bars: LaidBar[];
    clef: { glyph: GlyphName; x: number; y: number };
    /** the line's key signature: the key in force at its first bar */
    keySigns: PlacedSign[];
    chords: { bar: number; x: number; y: number; runs: TextRun[]; reminder: boolean }[];
    words: { bar: number; text: string; x: number; y: number }[];
    joins: Join[];
    section: { x: number; y: number; w: number; h: number; textX: number; textY: number; runs: TextRun[] } | null;
    number: { text: string; x: number; y: number } | null;
    tempos: { bar: number; x: number; y: number; bpm: number; unit: number; dotted: boolean }[];
    /** the word over a free bar */
    texts: { bar: number; x: number; y: number }[];
    /** the next line's new time signature, repeated after this line's last barline */
    warning: (TimeSig & { bar: number }) | null;
    /** the same of the next line's new key signature, with its naturals */
    keyWarning: { bar: number; signs: PlacedSign[] } | null;
}

export interface Engraving { width: number; sizes: Sizes; systems: System[] }

// ---- the engraving ---------------------------------------------------------------------------------------

interface ChordMark { pos: number; symbol: { runs: TextRun[]; w: number }; event: ChordEvent; reminder: boolean }

interface BarInfo {
    n: number;
    sig: { top: { text: string; w: number }; bottom: { text: string; w: number }; w: number } | null;
    sigW: number;
    key: KeySign[];
    keyW: number;
    events: ChordMark[];
    reminder: ChordMark | null;
    words: { word: Word; w: number }[];
    textW: number;
    marks: Mark[];
}

interface Need { tight: number; comfort: number; beats: number[] }

/** Where a place of a bar is drawn (in beats from its start), given where its beats begin and the last ends. */
export function xIn(beatX: number[], pos: number): number {
    const k = Math.min(beatX.length - 2, Math.max(0, Math.floor(pos + 1e-9)));
    return beatX[k]! + (pos - k) * (beatX[k + 1]! - beatX[k]!);
}

/** The notes that share one beam: in a beat, its short notes one after another — a rest under the beam does not part
 * them, a longer note does. → for each note the indices of its beam's notes (null: none). */
function beamsOf(notes: Note[]): (number[] | null)[] {
    const of: (number[] | null)[] = notes.map(() => null);
    let run: number[] | null = null, beat: number | null = null;
    notes.forEach((n, i) => {
        if (n.rest) {
            if (run && Math.floor(n.pos + 1e-6) !== beat) run = null;
            return;
        }
        if (n.beam === null || n.beam !== beat) run = null;
        if (n.beam === null) {
            beat = null;
            return;
        }
        if (!run) {
            run = [];
            beat = n.beam;
        }
        run.push(i);
        of[i] = run;
    });
    return of;
}

export function layoutSheet(music: Music, o: { width: number; space: number; measure: Measure; writing: Writing }): Engraving {
    const S = o.space, sizes = sizesFor(S), measure = o.measure, bars = music.bars, N = bars.length;
    const sheet: Engraving = { width: o.width, sizes, systems: [] };
    if (!N) return sheet;
    const bar = (b: number): MusicBar => bars[b]!;

    // --- what starts every line: clef and key signature — the key in force at the line's first bar. Written notes stand
    // under a treble clef with an 8 below: as guitar music is written, an octave up.
    const slashes = o.writing !== 'notes';
    const clefName: GlyphName = slashes ? 'gClef' : 'gClef8vb', clef = GLYPH[clefName];
    const clefRight = (CLEF_LEFT + clef.w) * S;
    const signatureOf = (b: number) => bar(b).key?.signature ?? 0;
    const prefixOf = (j: number) => {
        const signs = keySigns(0, signatureOf(j));
        return clefRight + (signs.length ? (AFTER_CLEF + signsWidth(signs) + AFTER_KEY) * S : AFTER_CLEF * S);
    };
    const finalBarline = (LINE.thin + LINE.separation + LINE.thick) * S;
    const wordGap = measure(' ', sizes.lyric, 400) + WORD_GAP * S;

    // What is written on the staff for a bar: every note and rest of it, engraved; the accidentals against the key
    // signature in force in it.
    const marksOf = (mb: MusicBar): Mark[] => {
        const notes = mb.notes, heads: (HeadIn[] | null)[] = notes.map(n => (n.rest ? null : headsOf(n, slashes)));
        if (!slashes) {
            const alters = keyAlters(mb.key?.signature ?? 0);
            const sounding = notes.filter(n => !n.rest && n.sound);
            accidentalsOf(sounding.map(n => ({ heads: n.sound!.heads, tied: n.tieIn })), alters).forEach((shown, i) => {
                const note = sounding[i]!, own = heads[notes.indexOf(note)]!;
                own.forEach((h, k) => {
                    h.sign = shown[k] ? SIGNS[note.sound!.heads[k]!.alter] ?? null : null;
                });
            });
        }
        // the notes under one beam share their stems' direction
        const runs = beamsOf(notes);
        return notes.map((n, i) => {
            const own = heads[i] ?? null;
            if (n.rest || !own) return shape(n, null, slashes, false, false);
            const beamed = runs[i] ?? [i];
            return shape(n, own, slashes, stemGoesUp(beamed.map(k => heads[k]!)), n.beam !== null && beamed.length === 1);
        });
    };

    // --- every bar, measured
    const info: BarInfo[] = bars.map(mb => {
        let sig: BarInfo['sig'] = null, sigW = 0;
        if (mb.signature) {
            const top = timeDigits(mb.signature.top), bottom = timeDigits(mb.signature.bottom);
            sig = { top, bottom, w: Math.max(top.w, bottom.w) * S };
            sigW = SIG_LEFT * S + sig.w + SIG_RIGHT * S;
        }
        // a key that changes with the bar: its signature (and the naturals of the old one) after the barline, before the
        // time signature — on a line's first bar the line's own signature says it
        const key = mb.keyChange ? keySigns(mb.keyChange.from, mb.keyChange.to) : [];
        const keyW = key.length ? (SIG_LEFT + signsWidth(key)) * S : 0;
        const events: ChordMark[] = mb.events.map(e => ({ pos: e.pos, symbol: textRuns(e.text, sizes.chord, 600, measure), event: e, reminder: false }));
        const reminder: ChordMark | null = mb.sounding && !(events.length && events[0]!.pos === 0)
            ? { pos: 0, symbol: textRuns(mb.sounding.text, sizes.chord, 600, measure), event: mb.sounding, reminder: true }
            : null;
        let textW = 0;
        const words = mb.words.map(w => {
            const ww = measure(w.text, sizes.lyric, 400);
            textW += ww + wordGap;
            return { word: w, w: ww };
        });
        return { n: mb.beats.length, sig, sigW, key, keyW, events, reminder, words, textW, marks: marksOf(mb) };
    });
    const at = (b: number): BarInfo => info[b]!;
    // what stands between a bar's left edge and its beats' room: the key's change (not at a line's start) and the time
    // signature
    const headW = (b: number, first: boolean) => (first ? 0 : at(b).keyW) + at(b).sigW;

    // From a bar's left edge (or its time signature) to its first beat: room for the first note's accidentals, and at
    // the start of a line for the tie that comes over from the line before.
    const leadOf = (b: number, first: boolean) => {
        const m = at(b).marks[0];
        if (!m || m.note.bar) return PAD_LEFT * S;
        return Math.max(PAD_LEFT, m.left + MARK_LEAD, first && m.note.tieIn && m.heads[0] ? TIE_STUB - m.heads[0].tieTo + MARK_LEAD : 0) * S;
    };

    // When the meter changes with the first bar of the next line, the new time signature is also written at the end of
    // this one, after its last barline, as a warning: the room it takes after bar b when b ends a line. A key that
    // changes with the next line's first bar is announced the same way, before the time signature.
    const warningWidth = (b: number) => {
        if (b + 1 >= N) return 0;
        const next = at(b + 1);
        return next.keyW + (next.sig ? SIG_LEFT * S + next.sig.w + SIG_RIGHT * S : next.keyW ? SIG_RIGHT * S : 0);
    };
    const eventsOf = (b: number, first: boolean): ChordMark[] => {
        const own = at(b);
        return first && own.reminder ? [own.reminder, ...own.events] : own.events;
    };
    // The row each chord symbol of a bar stands on: one that begins less than half a beat after the symbol before it, on
    // the lower row, goes up a row (a sixteenth's push, a triplet), so that two names a fraction of a beat apart do not
    // push the beats apart; the next goes down again.
    const rowsOf = (ev: ChordMark[]) => {
        const rows: number[] = [];
        ev.forEach((a, i) => rows.push(i > 0 && rows[i - 1] === 0 && a.pos - ev[i - 1]!.pos < 0.5 - 1e-6 ? 1 : 0));
        return rows;
    };
    // The widths a bar needs, beat by beat: what stands in a stretch of it (two notes and the room between them, a chord
    // symbol and the next one on its row) asks that much room of each beat the stretch covers, in the share of the beat
    // it covers. `beats` is the least width of each beat, `tight` and `comfort` the bar's. They depend on whether it
    // starts a line (the chord still sounding is repeated there) and whether it ends one (its last chord symbol may not
    // run past the line's end, but may run over a barline into free space before the next bar's first chord).
    const need = (b: number, first: boolean, last: boolean): Need => {
        const I = at(b), ev = eventsOf(b, first), lead = leadOf(b, first), beats: number[] = [];
        for (let k = 0; k < I.n; k++) beats.push(BEAT_TIGHT * S);
        const room = (a: number, z: number, width: number) => {
            for (let k = Math.floor(a + 1e-9); k < I.n && k < z - 1e-9; k++) beats[k] = Math.max(beats[k]!, width / (z - a));
        };
        I.marks.forEach((m, i) => {
            if (m.note.bar) return;
            const next = I.marks[i + 1];
            // (the last: room to the barline — and where it is tied over it, room for the tie to cross it plainly)
            if (!next) {
                room(m.note.pos, I.n, (m.note.tieOut && m.heads[0] ? Math.max(m.right + MARK_END, m.heads[0].tieFrom + TIE_ACROSS) : m.right + MARK_END) * S);
                return;
            }
            const apart = Math.max(m.right + MARK_GAP + next.left, next.note.tieIn && m.heads[0] && next.heads[0] ? m.heads[0].tieFrom + TIE_MIN - next.heads[0].tieTo : 0);
            room(m.note.pos, next.note.pos, apart * S);
        });
        const rows = rowsOf(ev);
        ev.forEach((a, i) => {
            const w = a.symbol.w + CHORD_GAP * S;
            let k = i + 1;
            while (k < ev.length && rows[k] !== rows[i]) k++;
            const later = ev[k];
            if (later) {
                room(a.pos, later.pos, w);
                return;
            }
            let over = 0;
            if (!last && b + 1 < N) {
                const X = at(b + 1);
                over = headW(b + 1, false) + leadOf(b + 1, false) + (X.events[0]?.pos ?? X.n) * BEAT_TIGHT * S;
            }
            room(a.pos, I.n, w - over);
        });
        const fixed = headW(b, first) + lead;
        let sum = 0, comfortable = 0;
        for (const w of beats) {
            sum += w;
            comfortable += Math.max(w, BEAT_COMFORT * S);
        }
        const tail = b === N - 1 ? finalBarline : last ? warningWidth(b) : 0;
        const tight = fixed + sum + tail, comfort = fixed + comfortable + tail;
        return { tight: Math.max(tight, I.textW), comfort: Math.max(comfort, I.textW * WORD_ROOM), beats };
    };
    // The beats' widths in a room of `width`: each at least its least width, the rest shared in proportion to their
    // comfortable widths — evenly where every beat is comfortable alike.
    const spread = (beats: number[], width: number) => {
        const comfort = beats.map(w => Math.max(w, BEAT_COMFORT * S));
        const atScale = (s: number) => beats.map((w, k) => Math.max(w, comfort[k]! * s));
        const total = (s: number) => atScale(s).reduce((sum, w) => sum + w, 0);
        let lo = 0, hi = 1;
        while (total(hi) < width) hi *= 2;
        for (let it = 0; it < 50; it++) {
            const mid = (lo + hi) / 2;
            if (total(mid) < width) lo = mid;
            else hi = mid;
        }
        const widths = atScale(hi), extra = width - total(hi);
        return widths.map(w => w + extra / widths.length);
    };

    // What joins the notes that share a beat: their beam, and the figure of a tuplet. Gives the heights of what it added,
    // and ends the beamed notes' stems at the beam.
    const joinBeat = (items: Placed[], b: number, joins: (Part & { bar: number })[]) => {
        const added: { top: number; bottom: number }[] = [], runs = beamsOf(items.map(it => it.mark.note)), done = new Set<number[]>();
        const beams = new Map<number, { y: number; up: boolean; all: number[] }>();
        const add = (parts: Part[]) => parts.forEach(part => joins.push({ ...part, bar: b }));
        const stemOf = (it: Placed) => it.x + (it.mark.stem ? it.mark.stem.x : 0);
        for (const run of runs) {
            if (!run || run.length < 2 || done.has(run)) continue;
            done.add(run);
            const group = run.map(i => items[i]!), made = beam(group, group[0]!.mark.up);
            add(made.parts);
            beams.set(Math.floor(group[0]!.mark.note.pos + 1e-6), { y: made.y, up: group[0]!.mark.up, all: run });
        }
        // the figure of a tuplet, over its beat: at its beam where one beam holds all its notes, else in a bracket
        const cells = new Map<number, number[]>();
        items.forEach((it, i) => {
            const note = it.mark.note;
            if (!note.tuplet) return;
            const k = Math.floor(note.pos + 1e-6);
            cells.set(k, [...cells.get(k) ?? [], i]);
        });
        for (const [k, indices] of cells) {
            const cell = indices.map(i => items[i]!), sounding = cell.filter(it => !it.mark.note.rest);
            const held = beams.get(k), up = sounding.length ? sounding[0]!.mark.up : true;
            // (a rest between the beamed notes stands under the beam: the beam still holds the tuplet)
            const whole = !!held && held.all.includes(indices[0]!) && held.all.includes(indices[indices.length - 1]!);
            const marks = (sounding.length ? sounding : cell).map(it => it.mark);
            const edge = held && whole ? held.y : up ? Math.min(...marks.map(m => m.top)) : Math.max(...marks.map(m => m.bottom));
            const figure = tupletFigure(cell[0]!.mark.note.tuplet, stemOf(cell[0]!), stemOf(cell[cell.length - 1]!) + LINE.stem, edge, held && whole ? held.up : up, !whole);
            add(figure.parts);
            added.push(figure);
        }
        return added;
    };

    // --- where the lines break
    // How badly a break before bar i cuts a lyric line: 0 for none, up to 1 for a line cut in the middle (a pickup word
    // left on the line above costs little).
    const lineBars = new Map<number, number[]>();
    for (const mb of bars) for (const w of mb.words) lineBars.set(w.line, [...lineBars.get(w.line) ?? [], mb.index]);
    const cut = bars.map(() => 0);
    for (const own of lineBars.values()) {
        for (let i = own[0]! + 1; i <= own[own.length - 1]!; i++) {
            const before = own.filter(b => b < i).length;
            cut[i] = cut[i]! + 2 * Math.min(before, own.length - before) / own.length;
        }
    }
    const PHRASE_COST = [0, 1, 0.6, 1];
    const endsRagged = (i: number) => i === N || !!bar(i).section;
    const lines = new Map<number, { tight: number; comfort: number }>();
    const lineOf = (j: number, i: number) => {
        const key = j * (N + 1) + i;
        let line = lines.get(key);
        if (!line) {
            line = { tight: 0, comfort: 0 };
            for (let b = j; b < i; b++) {
                const nd = need(b, b === j, b === i - 1);
                line.tight += nd.tight;
                line.comfort += nd.comfort;
            }
            lines.set(key, line);
        }
        return line;
    };
    // (a line begins with its clef and the key signature in force at its first bar)
    const roomOf = (j: number) => o.width - prefixOf(j);
    const lineCost = (j: number, i: number) => {
        const line = lineOf(j, i), room = roomOf(j);
        if (line.tight > room && i - j > 1) return Infinity;
        const r = room / line.comfort;
        let cost: number;
        if (r >= 1) cost = endsRagged(i) && r > 1.5 ? 0.05 : 0.5 * (r - 1) * (r - 1);
        else cost = 2 * (1 / r - 1) * (1 / r - 1);
        if (i < N && !bar(i).section) cost += PHRASE_COST[bar(i).phrase % 4]! + 0.5 * cut[i]!;
        return cost;
    };
    // A bar stands alone on a line only when it cannot share one: next to a one-bar line, the two lines together must not
    // fit on one. The last bar of a section or of the piece may stand alone; so may the bar before a section.
    const ALONE_COST = 1000;
    const aloneCost = (h: number, j: number, i: number) => {
        if (bar(j).section || !(j - h === 1 || (i - j === 1 && !endsRagged(i)))) return 0;
        return i - h <= MAX_BARS && lineOf(h, i).tight <= roomOf(h) ? ALONE_COST : 0;
    };
    // best[i]: for each j, the least cost of bars 0 … i−1 in lines, the last of them j … i−1; from[i][j] the line before.
    const best: Map<number, number>[] = [new Map()], from: Map<number, number>[] = [new Map()];
    for (let i = 1; i <= N; i++) {
        const bi = new Map<number, number>(), fi = new Map<number, number>();
        best[i] = bi;
        from[i] = fi;
        for (let j = i - 1; j >= Math.max(0, i - MAX_BARS); j--) {
            const own = lineCost(j, i);
            let least = j === 0 ? 0 : Infinity, via = -1;
            if (j > 0 && own < Infinity) {
                for (const [h, c0] of best[j]!) {
                    const c = c0 + aloneCost(h, j, i);
                    if (c < least) {
                        least = c;
                        via = h;
                    }
                }
            }
            if (own + least < Infinity) {
                bi.set(j, own + least);
                fi.set(j, via);
            }
            if (bar(j).section) break;       // a section starts a line: no line reaches back over its start
        }
    }
    const breaks: [number, number][] = [];
    let end = N, start = -1;
    for (const [k, c] of best[N]!) if (start < 0 || c < best[N]!.get(start)!) start = k;
    while (end > 0) {
        breaks.unshift([start, end]);
        const before = from[end]!.get(start)!;
        end = start;
        start = before;
    }

    // --- the lines themselves
    let lastScale = 1.25;
    breaks.forEach(([j, lineEndBar], si) => {
        const needs: Need[] = [], line = lineOf(j, lineEndBar), room = roomOf(j);
        for (let b = j; b < lineEndBar; b++) needs.push(need(b, b === j, b === lineEndBar - 1));
        // one scale for the line: every bar gets its comfortable width times the scale, but never less than its tight width
        let scale: number;
        const filled = (s: number) => needs.reduce((sum, nd) => sum + Math.max(nd.tight, nd.comfort * s), 0);
        if (line.tight >= room) scale = 0;
        else {
            let lo = 0, hi = 1;
            while (filled(hi) < room) hi *= 2;
            for (let it = 0; it < 40; it++) {
                const mid = (lo + hi) / 2;
                if (filled(mid) < room) lo = mid;
                else hi = mid;
            }
            scale = hi;
            // the last line of a section or of the piece is not stretched to the margin when it is short: it keeps the
            // spacing of the line before it
            const ragged = Math.max(1, Math.min(1.5, lastScale));
            if (endsRagged(lineEndBar) && scale > 1.5) scale = ragged;
            else lastScale = scale;
        }

        // Where every bar of the line stands and what is written in it. The notes are kept in staff spaces, their height
        // counted from the staff's top line, until the line's highest and lowest ink say where its chord symbols and its
        // words go.
        let x = prefixOf(j);
        const places: { x: number; w: number; warning: number; x0: number; beatX: number[]; marks: (Placed & { bar: number })[] }[] = [];
        const written: (Placed & { bar: number })[] = [], joins: (Part & { bar: number })[] = [], ties: { bar: number; curve: number[] }[] = [];
        const ink = { top: 0, bottom: 4 };
        const reach = (r: { top: number; bottom: number }) => {
            ink.top = Math.min(ink.top, r.top);
            ink.bottom = Math.max(ink.bottom, r.bottom);
        };
        for (let b = j; b < lineEndBar; b++) {
            const I = at(b), nd = needs[b - j]!;
            const w = Math.max(nd.tight, nd.comfort * scale), warning = b === lineEndBar - 1 ? warningWidth(b) : 0, lead = leadOf(b, b === j);
            const head = headW(b, b === j), x0 = x + head + lead;
            const widths = spread(nd.beats, w - head - lead - (b === N - 1 ? finalBarline : 0) - warning);
            const beatX = [x0];
            for (const bw of widths) beatX.push(beatX[beatX.length - 1]! + bw);
            const marks = I.marks.map(m => {
                // (the rest of a whole bar stands in the bar's middle)
                const px = m.note.bar ? (x + head + x + w) / 2 - m.right * S / 2 : xIn(beatX, m.note.pos);
                return { mark: m, bar: b, x: px / S };
            });
            joinBeat(marks, b, joins).forEach(reach);
            for (const it of marks) {
                written.push(it);
                reach(it.mark);
            }
            // a held beat: the fermata over what sounds on it, above the staff and the notes
            for (const k of bar(b).fermata) {
                const it = marks.find(p => p.mark.note.bar || (p.mark.note.pos <= k + 1e-6 && p.mark.note.pos + p.mark.note.beats > k + 1e-6));
                if (!it) continue;
                const headPart = it.mark.parts.find(part => part.kind === 'slash' || part.kind === 'note' || part.kind === 'rest');
                if (!headPart || headPart.type !== 'glyph') continue;
                const middle = it.x + headPart.x + GLYPH[headPart.glyph].w / 2, y = Math.min(0, it.mark.top) - FERMATA_CLEAR;
                joins.push({ type: 'text', kind: 'fermata', bar: b, text: GLYPH.fermata.ch, x: middle - GLYPH.fermata.w / 2, y });
                reach({ top: y - GLYPH.fermata.top, bottom: y });
            }
            places.push({ x, w, warning, x0, beatX, marks });
            x += w;
        }
        // Ties: between a note and the next; over the end of the line a tie stops before the last barline, and its other
        // part begins the next line.
        const lineEnd = (x - places[places.length - 1]!.warning) / S - BARLINE_CLEAR;
        written.forEach((it, i) => {
            const after = written[i + 1] ?? null, spans: [Placed & { bar: number } | null, Placed & { bar: number } | null][] = [];
            if (i === 0 && it.mark.note.tieIn) spans.push([null, it]);
            if (it.mark.note.tieOut) spans.push([it, after]);
            for (const [a, z] of spans) {
                const owner = (a ?? z)!;
                owner.mark.heads.forEach((h, k) => {
                    const x1 = z ? z.x + z.mark.heads[k]!.tieTo : lineEnd;
                    const made = tie(a ? a.x + h.tieFrom : x1 - TIE_STUB, x1, h.tieY, h.tieUp);
                    ties.push({ bar: (z ?? a)!.bar, curve: made.curve });
                    reach(made);
                });
            }
        });

        // Over the line: a tempo mark where the tempo is said — the beat's note and the number —, and the word over a
        // free bar; at the line's start, or over the bar.
        let top = 0;
        const tempos: System['tempos'] = [], texts: System['texts'] = [];
        let section: System['section'] = null;
        places.forEach((place, pi) => {
            const mb = bar(j + pi), tx = pi === 0 ? 0.2 * S : place.x + 0.4 * S;
            if (mb.tempo) tempos.push({ bar: j + pi, x: tx, y: top + 2.6 * S, bpm: mb.tempo.bpm, unit: mb.tempo.unit, dotted: mb.tempo.dotted });
            if (mb.free) texts.push({ bar: j + pi, x: tx, y: top + 2.6 * S });
        });
        if (tempos.length || texts.length) top += 3.4 * S;
        const sectionName = bar(j).section;
        if (sectionName) {
            const label = textRuns(sectionName, sizes.section, 700, measure);
            section = { x: 0, y: top, w: label.w + 1.2 * S, h: 2.3 * S, textX: 0.6 * S, textY: top + 1.65 * S, runs: label.runs };
            top += 3.0 * S;
        }
        const chordLine = Math.max(CHORD_LINE, CHORD_CLEAR - ink.top);
        // (a line with symbols on the upper row is that row higher)
        const upper = places.some((_, pi) => rowsOf(eventsOf(j + pi, pi === 0)).includes(1)) ? CHORD_ROW : 0;
        const staffTop = top + (1.75 + upper + chordLine) * S, staffBottom = staffTop + 4 * S, chordY = staffTop - chordLine * S;
        const px = (part: Part, atX: number): DrawnPart => {
            const x = (atX + part.x) * S, y = staffTop + part.y * S;
            if (part.type === 'glyph') return { type: 'glyph', kind: part.kind, glyph: part.glyph, x, y };
            if (part.type === 'text') return { type: 'text', kind: part.kind, text: part.text, x, y };
            return { type: 'ink', kind: part.kind, x, y, w: part.w * S, h: part.h * S };
        };
        const round = (v: number) => Math.round(v * 100) / 100;
        // key signs from x on
        const signsAt = (signs: KeySign[], from: number): PlacedSign[] => {
            let sx = from;
            return signs.map(s => {
                const placed = { glyph: s.glyph, x: sx, y: staffTop + s.step * S / 2 };
                sx += s.room * S;
                return placed;
            });
        };
        const system: System = {
            index: si, width: 0, height: 0, top, bottom: 0, staffTop, bars: [], chords: [], words: [], section, tempos, texts,
            clef: { glyph: clefName, x: CLEF_LEFT * S, y: staffTop + 3 * S },
            keySigns: signsAt(keySigns(0, signatureOf(j)), clefRight + AFTER_CLEF * S),
            joins: [
                ...joins.map(join => ({ ...px(join, 0), bar: join.bar })),
                ...ties.map(t => {
                    const c = t.curve.map((v, i) => round(i % 2 ? staffTop + v * S : v * S));
                    return { type: 'tie' as const, kind: 'tie' as const, bar: t.bar, path: `M${c[0]} ${c[1]}C${c.slice(2, 8).join(' ')}C${c.slice(8).join(' ')} ${c[0]} ${c[1]}Z` };
                }),
            ],
            // (the piece's first bar is not numbered; a line that begins with any other is)
            number: bar(j).number > 1 ? { text: String(bar(j).number), x: 0, y: staffTop - 1.75 * S } : null,
            warning: null,
            keyWarning: null,
        };

        const wordItems: { ideal: number; w: number; word: Word; bar: number }[] = [];
        places.forEach((place, pi) => {
            const b = j + pi, I = at(b), keyW = pi === 0 ? 0 : I.keyW;
            system.bars.push({
                index: b, left: place.x, right: place.x + place.w - place.warning, beatX: place.beatX, count: I.n,
                marks: place.marks.map(it => ({ pos: it.mark.note.pos, beats: it.mark.note.beats, parts: it.mark.parts.map(part => px(part, it.x)) })),
                keySigns: keyW ? signsAt(I.key, place.x + SIG_LEFT * S) : [],
                signature: I.sig ? {
                    top: I.sig.top.text, bottom: I.sig.bottom.text,
                    topX: place.x + keyW + SIG_LEFT * S + (I.sig.w - I.sig.top.w * S) / 2, bottomX: place.x + keyW + SIG_LEFT * S + (I.sig.w - I.sig.bottom.w * S) / 2,
                    topY: staffTop + S, bottomY: staffTop + 3 * S,
                } : null,
                barline: b === N - 1 ? 'final' : bar(b + 1).section ? 'double' : 'single',
            });
            const evs = eventsOf(b, b === j), rows = rowsOf(evs);
            evs.forEach((ev, ei) => {
                system.chords.push({ bar: b, x: xIn(place.beatX, ev.pos), y: chordY - rows[ei]! * CHORD_ROW * S, runs: ev.symbol.runs, reminder: ev.reminder });
            });
            for (const it of I.words) wordItems.push({ ideal: xIn(place.beatX, it.word.pos), w: it.w, word: it.word, bar: b });
            if (place.warning) {
                const next = at(b + 1);
                let wx = place.x + place.w - place.warning;
                if (next.keyW) {
                    system.keyWarning = { bar: b + 1, signs: signsAt(next.key, wx + SIG_LEFT * S) };
                    wx += next.keyW;
                }
                if (next.sig) {
                    wx += SIG_LEFT * S;
                    system.warning = {
                        bar: b + 1, top: next.sig.top.text, bottom: next.sig.bottom.text,
                        topX: wx + (next.sig.w - next.sig.top.w * S) / 2, bottomX: wx + (next.sig.w - next.sig.bottom.w * S) / 2,
                        topY: staffTop + S, bottomY: staffTop + 3 * S,
                    };
                }
            }
        });
        system.width = x;

        // (the clef reaches 1.63 spaces below the staff; with its 8, 2.56)
        let bottom = Math.max(staffBottom + (slashes ? 1.75 : 2.7) * S, staffTop + (ink.bottom + 0.2) * S);
        if (wordItems.length) {
            const wordY = staffBottom + Math.max(WORD_LINE, ink.bottom - 4 + WORD_CLEAR) * S + sizes.lyric * 0.78;
            // words keep clear of the clef's tail, which hangs below the staff
            const xs = placeInOrder(wordItems, clefRight + 0.4 * S, system.width, wordGap);
            wordItems.forEach((it, k) => system.words.push({ bar: it.bar, text: it.word.text, x: xs[k]!, y: wordY }));
            bottom = Math.max(bottom, wordY + sizes.lyric * 0.3);
        }
        system.bottom = bottom;
        system.height = bottom + 0.25 * S;
        sheet.systems.push(system);
    });
    return sheet;
}
