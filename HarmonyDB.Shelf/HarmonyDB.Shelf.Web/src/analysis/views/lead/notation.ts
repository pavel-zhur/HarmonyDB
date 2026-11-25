// The engraving of one written note or rest, and of what joins notes: beams, ties, the figure of a tuplet. The same for
// both ways of writing a chord — a rhythm slash, or the chord's notes.
//
// Data in, data out; no DOM. Everything is in staff spaces: x from the mark's own origin (the left edge of its
// noteheads' column), y down from the staff's top line (2 is the middle line).
//
//   headsOf(note, slashes) → the noteheads of a note, the lowest first: one on the middle line for a slash, the chord's
//       notes otherwise
//   stemGoesUp(headsOfNotes) → whether notes that share a stem's direction (one note, or a beamed group) are stemmed up:
//       away from the head farthest from the middle line, down when two are as far
//   shape(note, heads, slashes, up, flagged) → mark
//       flagged  a note shorter than a quarter that stands alone, not under a beam
//   mark.heads   the noteheads with their ties: where a tie that leaves the head begins and one that comes to it ends, at
//                which height, and whether it bends up. The ties of a chord bend away from its middle; a slash's tie lies
//                over it, its stem being below.
//   beam(group, up)   ends the group's stems at the beam; a beam for each level of the notes' values
//   tupletFigure(number, x0, x1, y, up, bracket)
//   tie(x0, x1, y, up) → the twelve numbers of its outline (start, two control points, end, and the two control points of
//       the way back)

import { flagGlyph, GLYPH, LINE, restGlyph, tupletDigits, TUPLET_HEIGHT, type GlyphName } from './glyphs';
import type { Note } from './rhythm';

const MIDDLE = 2;                // the middle line
const STEM = 3.5;                // a stem's length from its last notehead: an octave
const DOT_GAP = 0.3, SIGN_GAP = 0.2, SIGN_COLUMN = 1.1, SIGN_CLEAR = 0.1;
const TUPLET_GAP = 0.4, BRACKET_HOOK = 0.6, BRACKET_LINE = 0.5;
const STUB = 1.1;                // a beam's stub: a sixteenth beside a longer note under one beam
const TIE_GAP = 0.1, TIE_OFF = 0.4;     // between a tie's end and a round notehead, and off the head's middle

/** A sign of the music font, a text of its signs (a tuplet's figure, a fermata), or a bar of ink (a stem, a beam). */
export type Part =
    | { type: 'glyph'; kind: string; glyph: GlyphName; x: number; y: number; name?: string | null }
    | { type: 'text'; kind: string; text: string; x: number; y: number }
    | InkPart;

export interface InkPart { type: 'ink'; kind: string; x: number; y: number; w: number; h: number }

interface Stem extends InkPart { from: number }

export interface HeadIn {
    y: number;
    name: string | null;
    /** the accidental to write, or null */
    sign: GlyphName | null;
}

export interface TiedHead { y: number; tieFrom: number; tieTo: number; tieY: number; tieUp: boolean }

export interface Mark {
    note: Note;
    parts: Part[];
    stem: Stem | null;
    up: boolean;
    /** how far the mark reaches to the left and to the right of its origin */
    left: number;
    right: number;
    /** its highest and lowest ink */
    top: number;
    bottom: number;
    heads: TiedHead[];
}

export function headsOf(note: Note, slashes: boolean): HeadIn[] {
    if (slashes) return [{ y: MIDDLE, name: null, sign: null }];
    if (!note.sound) throw new Error('A chord without its notes cannot be written as notes.');
    return note.sound.heads.map(h => ({ y: h.line / 2, name: h.name, sign: null }));
}

export function stemGoesUp(notes: HeadIn[][]): boolean {
    let above = 0, below = 0;
    for (const heads of notes) {
        above = Math.max(above, MIDDLE - heads[heads.length - 1]!.y);
        below = Math.max(below, heads[0]!.y - MIDDLE);
    }
    return below > above;
}

function rest(note: Note): Mark {
    const name = restGlyph(note.unit), g = GLYPH[name], y = note.unit === 1 ? 1 : MIDDLE;
    const parts: Part[] = [{ type: 'glyph', kind: 'rest', glyph: name, x: 0, y }];
    let right = g.w;
    if (note.dots) {
        parts.push({ type: 'glyph', kind: 'dot', glyph: 'dot', x: g.w + DOT_GAP, y: MIDDLE - 0.5 });
        right += DOT_GAP + GLYPH.dot.w;
    }
    return { note, parts, stem: null, up: false, left: 0, right, top: y - g.top, bottom: y + g.bottom, heads: [] };
}

export function shape(note: Note, heads: HeadIn[] | null, slashes: boolean, up: boolean, flagged: boolean): Mark {
    if (note.rest || !heads) return rest(note);
    const name: GlyphName = slashes ? (note.unit <= 2 ? 'diamond' : 'slash') : note.unit === 1 ? 'noteWhole' : note.unit === 2 ? 'noteHalf' : 'noteBlack';
    const g = GLYPH[name], n = heads.length, stemmed = note.unit > 1 && !note.free, parts: Part[] = [];
    if (!stemmed) up = true;       // (a whole note, or a free bar's mark, has no stem: its seconds are set as under a stem that goes up)
    const head = (i: number) => heads[i]!;

    // Two notes a second apart cannot share a column: the upper one stands to the right of the stem, the lower one to
    // the left.
    const xs: number[] = [], aside: boolean[] = [];
    for (let i = up ? 0 : n - 1; i >= 0 && i < n; i += up ? 1 : -1) {
        const near = up ? i - 1 : i + 1;
        aside[i] = near >= 0 && near < n && !aside[near] && Math.abs(head(i).y - head(near).y) <= 0.5;
        xs[i] = !aside[i] ? 0 : up ? g.w - (stemmed ? LINE.stem : 0) : LINE.stem - g.w;
    }
    let left = Math.min(...xs), right = Math.max(...xs) + g.w;
    let top = head(n - 1).y - g.top, bottom = head(0).y + g.bottom;

    // ledger lines, each as wide as the noteheads on it and beyond it
    let inkLeft = left;
    const ledger = (line: number, beyond: (y: number, line: number) => boolean) => {
        let x0 = Infinity, x1 = -Infinity;
        heads.forEach((h, k) => {
            if (beyond(h.y, line)) {
                x0 = Math.min(x0, xs[k]!);
                x1 = Math.max(x1, xs[k]! + g.w);
            }
        });
        parts.push({ type: 'ink', kind: 'ledger', x: x0 - LINE.ledgerOut, y: line - LINE.ledger / 2, w: x1 - x0 + 2 * LINE.ledgerOut, h: LINE.ledger });
        inkLeft = Math.min(inkLeft, x0 - LINE.ledgerOut);
        right = Math.max(right, x1 + LINE.ledgerOut);
    };
    if (!slashes) {
        for (let above = -1; above >= head(n - 1).y; above--) ledger(above, (y, line) => y <= line);
        for (let below = 5; below <= head(0).y; below++) ledger(below, (y, line) => y >= line);
    }

    // accidentals, from the top note down, each in the column nearest the notes that is free at its height
    const columns: number[] = [];
    for (let i = n - 1; i >= 0; i--) {
        const signName = head(i).sign;
        if (!signName) continue;
        const sign = GLYPH[signName];
        let c = 0;
        while (c < columns.length && columns[c]! + SIGN_CLEAR > head(i).y - sign.top) c++;
        columns[c] = head(i).y + sign.bottom;
        const sx = inkLeft - SIGN_GAP - c * SIGN_COLUMN - sign.w;
        parts.push({ type: 'glyph', kind: 'accidental', glyph: signName, x: sx, y: head(i).y });
        left = Math.min(left, sx);
        top = Math.min(top, head(i).y - sign.top);
        bottom = Math.max(bottom, head(i).y + sign.bottom);
    }
    left = Math.min(left, inkLeft);

    heads.forEach((h, k) => parts.push({ type: 'glyph', kind: slashes ? 'slash' : 'note', glyph: name, x: xs[k]!, y: h.y, name: h.name }));

    // dots, each in a space: a note on a line has its dot in the space above, or below when that is taken
    let after = right;
    if (note.dots) {
        const spaces = new Set<number>();
        for (let i = n - 1; i >= 0; i--) {
            const y = head(i).y, onLine = y === Math.round(y);
            let dy = onLine ? y - 0.5 : y;
            if (spaces.has(dy) && onLine) dy = y + 0.5;
            if (spaces.has(dy)) continue;
            spaces.add(dy);
            parts.push({ type: 'glyph', kind: 'dot', glyph: 'dot', x: right + DOT_GAP, y: dy });
        }
        after = right + DOT_GAP + GLYPH.dot.w;
    }
    let reach = after, stem: Stem | null = null;
    if (stemmed) {
        const offset = g.stem ?? 0;
        const from = up ? head(0).y - offset : head(n - 1).y + offset;
        const to = up ? Math.min(head(n - 1).y - STEM, MIDDLE) : Math.max(head(0).y + STEM, MIDDLE);
        stem = { type: 'ink', kind: 'stem', x: up ? g.w - LINE.stem : 0, y: Math.min(from, to), w: LINE.stem, h: Math.abs(to - from), from };
        parts.push(stem);
        top = Math.min(top, to);
        bottom = Math.max(bottom, to);
        if (flagged) {
            const flag = flagGlyph(note.unit, up);
            parts.push({ type: 'glyph', kind: 'flag', glyph: flag, x: stem.x, y: to });
            reach = Math.max(reach, stem.x + GLYPH[flag].w);
        }
    }
    const tieOf = g.tie;
    return {
        note, parts, stem, up, left: -left, right: reach, top, bottom,
        heads: heads.map((h, k) => {
            if (tieOf) return { y: h.y, tieFrom: tieOf.from, tieTo: tieOf.to, tieY: h.y - tieOf.over, tieUp: true };
            const bendsUp = n === 1 ? !up : k >= n / 2;
            // a tie that bends towards the end of the stem begins clear of its flag (an eighth tied on, as on an "and")
            const from = stem && flagged && bendsUp === up ? reach : after;
            return { y: h.y, tieFrom: from + TIE_GAP, tieTo: left - TIE_GAP, tieY: h.y + (bendsUp ? -TIE_OFF : TIE_OFF), tieUp: bendsUp };
        }),
    };
}

export interface Placed { mark: Mark; x: number }

function stemOf(it: Placed): Stem {
    if (!it.mark.stem) throw new Error('A beamed note without a stem.');
    return it.mark.stem;
}

export function beam(group: Placed[], up: boolean): { parts: Part[]; y: number } {
    let y = up ? Infinity : -Infinity;
    for (const it of group) {
        const heads = it.mark.heads;
        y = up ? Math.min(y, heads[heads.length - 1]!.y - STEM) : Math.max(y, heads[0]!.y + STEM);
    }
    for (const it of group) {
        const stem = stemOf(it);
        stem.y = Math.min(stem.from, y);
        stem.h = Math.abs(y - stem.from);
        it.mark.top = Math.min(it.mark.top, y);
        it.mark.bottom = Math.max(it.mark.bottom, y);
    }
    // The first beam joins them all; each further one joins the notes that short (a sixteenth's second), and a note alone
    // at its level has a stub of it, towards the notes after it when it is the first, else towards those before.
    const parts: Part[] = [], levels = group.map(it => Math.log2(it.mark.note.unit) - 2);
    const stemX = (i: number) => group[i]!.x + stemOf(group[i]!).x;
    const deepest = Math.max(...levels);
    for (let level = 0; level < deepest; level++) {
        const y0 = up ? y + level * (LINE.beam + LINE.beamGap) : y - LINE.beam - level * (LINE.beam + LINE.beamGap);
        for (let a = 0; a < group.length;) {
            if (levels[a]! <= level) {
                a++;
                continue;
            }
            let b = a;
            while (b + 1 < group.length && levels[b + 1]! > level) b++;
            let x0 = stemX(a), x1 = stemX(b) + LINE.stem;
            if (a === b) {
                const reach = Math.min(STUB, 0.5 * Math.abs(stemX(a === 0 ? 1 : a - 1) - stemX(a)));
                if (a === 0) x1 = x0 + reach;
                else {
                    x1 = x0 + LINE.stem;
                    x0 = x1 - reach;
                }
            }
            parts.push({ type: 'ink', kind: 'beam', x: x0, y: y0, w: x1 - x0, h: LINE.beam });
            a = b + 1;
        }
    }
    return { parts, y };
}

export function tupletFigure(number: number, x0: number, x1: number, y: number, up: boolean, bracket: boolean): { parts: Part[]; top: number; bottom: number } {
    const digits = tupletDigits(number), middle = (x0 + x1) / 2;
    const base = up ? y - TUPLET_GAP : y + TUPLET_GAP + TUPLET_HEIGHT, line = base - TUPLET_HEIGHT / 2;
    const parts: Part[] = [{ type: 'text', kind: 'tuplet', text: digits.text, x: middle - digits.w / 2, y: base }];
    if (bracket) {
        const gap = digits.w / 2 + 0.3, t = LINE.bracket;
        // (over notes closer together than the figure is wide, the bracket reaches past them: its lines are never
        // shorter than BRACKET_LINE)
        if (middle - gap - x0 < BRACKET_LINE) {
            x0 = middle - gap - BRACKET_LINE;
            x1 = middle + gap + BRACKET_LINE;
        }
        parts.push(
            { type: 'ink', kind: 'bracket', x: x0, y: line - t / 2, w: middle - gap - x0, h: t },
            { type: 'ink', kind: 'bracket', x: middle + gap, y: line - t / 2, w: x1 - middle - gap, h: t },
        );
        for (const x of [x0, x1 - t]) parts.push({ type: 'ink', kind: 'bracket', x, y: up ? line : line - BRACKET_HOOK, w: t, h: BRACKET_HOOK });
    }
    return { parts, top: base - TUPLET_HEIGHT, bottom: base };
}

export function tie(x0: number, x1: number, y: number, up: boolean): { curve: number[]; top: number; bottom: number } {
    const length = x1 - x0, rise = Math.min(0.55, 0.25 + 0.03 * length) * (up ? -1 : 1);
    const outer = y + rise / 0.75, inner = y + (rise - LINE.tieMiddle * (up ? -1 : 1)) / 0.75;
    const a = x0 + length / 4, b = x1 - length / 4;
    return { curve: [x0, y, a, outer, b, outer, x1, y, b, inner, a, inner], top: Math.min(y, y + rise), bottom: Math.max(y, y + rise) };
}
