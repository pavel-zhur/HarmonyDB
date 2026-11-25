// The written lengths: what sounds in a bar, from place to place, as the notes and rests that say how long each chord
// lasts, by the ordinary rules of notation. Data in, data out; no DOM.
//
//   writeBar(spans, shape) → [note]
//     spans   what fills the bar, in order and without gaps: [{ from, to, rest, tied, sound }]
//               from, to   places: { k, n, d } — n/d of the way into beat k (the bar's end is beat `count`, 0/1)
//               rest       nothing sounds
//               tied       the chord is the one sounding before (of the bar before)
//     shape   the bar's beats: { lengths, groups, triple }
//               lengths    each beat's length in sixty-fourths of a whole note (16 a quarter, 24 a dotted quarter)
//               groups     the sizes of the groups of beats the bar is read in, in order
//               triple     for each beat, whether it is three of a note (a dotted quarter of 6/8): it divides in threes
//                          without a tuplet
//
// The rules:
//   - A beat is the meter's: a quarter in x/4, a dotted quarter in 6/8 counted in two, each group of a sum (2+2+3/8: a
//     quarter, a quarter, a dotted quarter).
//   - A chord over whole beats inside a group is one note: for one, two, three or four quarter beats a quarter, a half,
//     a dotted half, a whole note. Over the border of two groups it is tied. Where no one value says a length (three
//     dotted quarters), the longest that fits and the rest, tied.
//   - A silence over whole beats is rests that show the beats: in a group of two or four, a rest of two beats on its
//     first and its third beat only; a bar of nothing is the whole-bar rest.
//   - Inside a beat the places say its division: the least common denominator of the fractions that fall in it. A beat
//     divided as its note divides (halves and quarters of a quarter; thirds, halves and sixths of a dotted quarter) is
//     written in plain values; any other division is a tuplet with its figure: in the time of the power of two just
//     under it (3 in 2, 5 in 4) for a beat of a plain note, of the number of its threes nearest to it (5 in 6) for a beat
//     of three. A length inside a beat is one value where one says it, else the longest that fits and the rest, tied.
//   - A chord that goes on from whole beats into the first half of a beat parted in two is a dotted note where that
//     keeps the beats in pairs (a dotted quarter and an eighth), else tied.
//   - What lasts less than a beat inside one beat is beamed with that beat's other short notes.

import type { Sound } from './voicing';

export const WHOLE = 64;         // lengths are counted in sixty-fourths of a whole note
const UNITS = [1, 2, 4, 8, 16, 32, 64];

export interface Place { k: number; n: number; d: number }

export interface Span {
    from: Place;
    to: Place;
    rest: boolean;
    tied: boolean;
    sound: Sound | null;
}

export interface BarShape {
    lengths: number[];
    groups: number[];
    triple: boolean[];
}

export interface Note {
    /** where it begins and how long it lasts, in beats of its bar */
    pos: number;
    beats: number;
    /** 1 a whole note, 2 a half, 4 a quarter, 8, 16, 32, 64 */
    unit: number;
    dots: number;
    /** 0, or the figure of the tuplet its beat is divided in (3 a triplet) */
    tuplet: number;
    rest: boolean;
    /** the rest that fills a whole bar */
    bar: boolean;
    /** tied to the note before it, and to the one after — also over a bar line */
    tieIn: boolean;
    tieOut: boolean;
    /** the beat whose notes it is beamed with, or null */
    beam: number | null;
    /** a moment of a free bar: a mark without a stem, of no length */
    free: boolean;
    sound: Sound | null;
}

interface Value { unit: number; dots: number }

function near(a: number, b: number): boolean {
    return Math.abs(a - b) < 1e-6;
}

function gcd(a: number, b: number): number {
    while (b) [a, b] = [b, a % b];
    return a;
}

function lcm(a: number, b: number): number {
    return a / gcd(a, b) * b;
}

/** The one note value of a length, or null when no value says it. */
export function valueOf(length: number): Value | null {
    for (const unit of UNITS) {
        if (near(length, WHOLE / unit)) return { unit, dots: 0 };
        if (near(length, 1.5 * WHOLE / unit)) return { unit, dots: 1 };
    }
    return null;
}

function mustValue(length: number): Value {
    const value = valueOf(length);
    if (!value) throw new Error(`No note value lasts ${length}/64.`);
    return value;
}

export function writeBar(spans: Span[], shape: BarShape): Note[] {
    const L = shape.lengths, count = L.length, only = spans[0];
    if (spans.length === 1 && only?.rest) {
        return [{ pos: 0, beats: count, unit: 1, dots: 0, tuplet: 0, rest: true, bar: true, tieIn: false, tieOut: false, beam: null, free: false, sound: null }];
    }
    const borders = [0];
    for (const size of shape.groups) borders.push(borders[borders.length - 1]! + size);
    const groupAt = (k: number) => {
        let g = 0;
        while (borders[g + 1]! <= k) g++;
        return g;
    };
    const length = (a: number, b: number) => {
        let sum = 0;
        for (let k = a; k < b; k++) sum += L[k]!;
        return sum;
    };

    // each beat's division: the least common denominator of the places inside it
    const div = L.map(() => 1);
    for (const span of spans) {
        for (const p of [span.from, span.to]) if (p.n && p.k < count) div[p.k] = lcm(div[p.k]!, p.d / gcd(p.n, p.d));
    }
    // the tuplet a beat is written in, or null: its figure, and the length of one of its notes as written
    const tupletOf = (k: number): { figure: number; unit: number } | null => {
        const d = div[k]!;
        let odd = d;
        while (odd % 2 === 0) odd /= 2;
        if (odd === 1 || (shape.triple[k] && odd === 3)) return null;
        if (!shape.triple[k]) {
            let p = 1;
            while (p * 2 <= d) p *= 2;
            return { figure: d, unit: L[k]! / p };
        }
        let best = 3;
        for (let q = 6; q <= 2 * d; q *= 2) if (Math.abs(q - d) < Math.abs(best - d)) best = q;
        return { figure: d, unit: L[k]! / best };
    };

    const notes: Note[] = [];
    const note = (span: Span, pos: number, beats: number, value: Value, first: boolean, extra?: { tuplet: number; beam: number | null }) => {
        notes.push({
            pos, beats, unit: value.unit, dots: value.dots, tuplet: extra?.tuplet ?? 0, rest: span.rest, bar: false,
            tieIn: !span.rest && (first ? span.tied : true), tieOut: false, beam: extra?.beam ?? null, free: false, sound: span.sound,
        });
    };
    // a length that no one value says: the longest that fits first, and the rest
    const split = (len: number) => {
        const out: { len: number; value: Value }[] = [];
        while (len > 1e-6) {
            let best: { len: number; value: Value } | null = null;
            for (let i = 0; i < UNITS.length && !best; i++) {
                for (const f of [1.5, 1]) {
                    const l = f * WHOLE / UNITS[i]!;
                    if (!best && l <= len + 1e-6) best = { len: l, value: { unit: UNITS[i]!, dots: f > 1 ? 1 : 0 } };
                }
            }
            if (!best) throw new Error(`No note value fits ${len}/64.`);
            out.push(best);
            len -= best.len;
        }
        return out;
    };
    // whole beats a … b of one group as held notes: the longest value first
    const held = (a: number, b: number, out: { pos: number; beats: number }[]): void => {
        for (let n = b - a; n > 0; n--) {
            if (!valueOf(length(a, a + n))) continue;
            out.push({ pos: a, beats: n });
            if (a + n < b) held(a + n, b, out);
            return;
        }
        out.push({ pos: a, beats: 1 });          // (a beat no one value says: split when it is written)
        if (a + 1 < b) held(a + 1, b, out);
    };
    // whole beats a … b of the group that begins at `start` and has `size` beats, as rests
    const rests = (a: number, b: number, start: number, size: number, out: { pos: number; beats: number }[]) => {
        for (let k = a; k < b; k++) {
            const pairs = (size === 2 || size === 4) && k + 1 < count && valueOf(L[k]! + L[k + 1]!);
            if (pairs && (k - start) % 2 === 0 && k + 2 <= b) {
                out.push({ pos: k, beats: 2 });
                k++;
            } else out.push({ pos: k, beats: 1 });
        }
    };
    // a stretch of beat k, from its u0-th to its u1-th division
    const within = (span: Span, k: number, u0: number, u1: number, first: boolean) => {
        const D = div[k]!, t = tupletOf(k), unit = t ? t.unit : L[k]! / D;
        for (let u = u0; u < u1;) {
            let n = u1 - u, value: Value | null = null;
            for (; n > 0 && !value; n--) value = valueOf(n * unit);
            n++;
            if (!value) throw new Error(`No note value lasts a part of a beat of ${L[k]}/64.`);
            note(span, k + u / D, n / D, value, first, { tuplet: t ? t.figure : 0, beam: !span.rest && value.unit >= 8 ? k : null });
            first = false;
            u += n;
        }
    };

    for (const span of spans) {
        let a = span.from, first = true;
        const b = span.to;
        // what begins inside a beat: to the beat's end, or to where the span ends in it
        if (a.n) {
            const D = div[a.k]!, inOne = b.k === a.k;
            within(span, a.k, a.n * D / a.d, inOne ? b.n * D / b.d : D, true);
            first = false;
            if (inOne) continue;
            a = { k: a.k + 1, n: 0, d: 1 };
        }
        let end = b.k;
        // whole beats that go on into the first half of a beat parted in two: a dotted note, where the beats stay in pairs
        const last = end - 1;
        const dotted = !span.rest && !!b.n && 2 * b.n === b.d && div[end] === 2 && last >= a.k && !shape.triple[last] && !shape.triple[end]
            && L[last] === L[end] && !!valueOf(1.5 * L[end]!) && (last - borders[groupAt(last)]!) % 2 === 0;
        if (dotted) end = last;
        const pieces: { pos: number; beats: number }[] = [];
        for (let g = groupAt(a.k); a.k < end && g < borders.length - 1 && borders[g]! < end; g++) {
            const x = Math.max(a.k, borders[g]!), y = Math.min(end, borders[g + 1]!);
            if (span.rest) rests(x, y, borders[g]!, borders[g + 1]! - borders[g]!, pieces);
            else held(x, y, pieces);
        }
        for (const p of pieces) {
            const len = length(p.pos, p.pos + p.beats), value = valueOf(len);
            if (value) {
                note(span, p.pos, p.beats, value, first);
                first = false;
                continue;
            }
            let at = p.pos;
            for (const part of split(len)) {
                note(span, at, part.len / L[p.pos]!, part.value, first);
                first = false;
                at += part.len / L[p.pos]!;
            }
        }
        if (dotted) {
            note(span, last, 1.5, mustValue(1.5 * L[last]!), first);
            continue;
        }
        if (b.n && b.k < count) within(span, b.k, 0, b.n * div[b.k]! / b.d, first);
    }
    return notes;
}
